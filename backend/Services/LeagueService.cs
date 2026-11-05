using DailyChallenges.Data;
using DailyChallenges.Exceptions;
using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services.Contracts;
using DailyChallenges.Services.Ranking;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Services
{
    public class LeagueService : ILeagueService
    {
        /// <summary>How long after a declined invitation the same user cannot be invited to the league again.</summary>
        public static readonly TimeSpan ReinviteCooldown = TimeSpan.FromHours(24);

        private readonly TimeProvider _time;
        private readonly ILeagueRepository _leagues;
        private readonly IUserProfileRepository _users;
        private readonly INotificationRepository _notifications;
        private readonly IGameRepository _games;
        private readonly IScoreVisibilityService _visibility;
        private readonly IScreenshotUrlSigner _signer;

        public LeagueService(
            ILeagueRepository leagues,
            IUserProfileRepository users,
            INotificationRepository notifications,
            IGameRepository games, TimeProvider time,
            IScoreVisibilityService visibility,
            IScreenshotUrlSigner signer)
        {
            _visibility = visibility;
            _signer = signer;
            _leagues = leagues;
            _users = users;
            _notifications = notifications;
            _games = games;
            _time = time;
        }

        // ── Create / rename / delete ──────────────────────────────────────────

        public async Task<LeagueDto> CreateLeagueAsync(int ownerId, string name)
        {
            name = name.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > 100)
                throw new ArgumentException("League name must be between 1 and 100 characters.");

            var owner = await _users.GetByIdAsync(ownerId)
                ?? throw new KeyNotFoundException("User not found.");

            var now = _time.GetUtcNow().UtcDateTime;
            // The league and the owner's membership are inserted together (one transaction).
            var league = await _leagues.CreateAsync(new League
            {
                Name = name,
                OwnerId = ownerId,
                CreatedAt = now,
                Members = new List<LeagueMember>
                {
                    new LeagueMember { UserId = ownerId, Role = LeagueRole.Owner, JoinedAt = now }
                }
            });

            return LeagueToDto(league, 1, owner.Username);
        }

        public async Task<LeagueDto> RenameLeagueAsync(int leagueId, int requestingUserId, string newName)
        {
            newName = newName.Trim();
            if (string.IsNullOrEmpty(newName) || newName.Length > 100)
                throw new ArgumentException("League name must be between 1 and 100 characters.");

            var league = await _leagues.GetByIdWithMembersAsync(leagueId)
                ?? throw new KeyNotFoundException("League not found.");

            RequireOwner(league, requestingUserId);

            if (!await _leagues.RenameAsync(leagueId, newName))
                throw new KeyNotFoundException("League not found.");
            league.Name = newName;

            return LeagueToDto(league, league.Members.Count, league.Owner?.Username ?? string.Empty);
        }

        public async Task DeleteLeagueAsync(int leagueId, int requestingUserId)
        {
            var league = await _leagues.GetByIdAsync(leagueId)
                ?? throw new KeyNotFoundException("League not found.");

            RequireOwner(league, requestingUserId);

            if (!await _leagues.DeleteAsync(leagueId))
                throw new KeyNotFoundException("League not found.");
        }

        // ── Queries ───────────────────────────────────────────────────────────

        public async Task<List<LeagueDto>> GetMyLeaguesAsync(int userId)
        {
            var leagues = await _leagues.GetByUserAsync(userId);
            return leagues.Select(l => LeagueToDto(l, l.Members.Count, l.Owner?.Username ?? string.Empty)).ToList();
        }

        public async Task<LeagueDetailDto> GetLeagueDetailAsync(int leagueId, int requestingUserId)
        {
            var league = await _leagues.GetByIdWithMembersAsync(leagueId)
                ?? throw new KeyNotFoundException("League not found.");

            RequireMember(league, requestingUserId);

            var pendingInvites = league.Invitations
                .Where(i => i.Status == LeagueInvitationStatus.Pending)
                .ToList();

            // Only expose token/invitee info to the owner
            var isOwner = league.OwnerId == requestingUserId;

            return new LeagueDetailDto
            {
                Id = league.Id,
                Name = league.Name,
                OwnerId = league.OwnerId,
                OwnerUsername = league.Owner?.Username ?? string.Empty,
                CreatedAt = league.CreatedAt,
                Members = league.Members.Select(m => new LeagueMemberDto
                {
                    UserId = m.UserId,
                    Username = m.User?.Username ?? string.Empty,
                    Level = m.User?.Level ?? 1,
                    Streak = m.User != null ? StreakHelper.Effective(m.User, _time.GetUtcNow().UtcDateTime) : 0,
                    Role = m.Role.ToString().ToLowerInvariant(),
                    JoinedAt = m.JoinedAt
                }).ToList(),
                PendingInvitations = isOwner
                    ? pendingInvites.Select(i => InvitationToDto(i)).ToList()
                    : new List<LeagueInvitationDto>()
            };
        }

        // ── Invitations ───────────────────────────────────────────────────────

        public async Task<LeagueInvitationDto> InviteByUsernameAsync(int leagueId, int inviterId, string username)
        {
            var league = await _leagues.GetByIdWithMembersAsync(leagueId)
                ?? throw new KeyNotFoundException("League not found.");

            RequireOwner(league, inviterId);

            var invitee = await _users.GetByUsernameAsync(username)
                ?? throw new KeyNotFoundException($"User '{username}' not found.");

            if (invitee.Id == inviterId)
                throw new ValidationException("You cannot invite yourself.");

            if (league.Members.Any(m => m.UserId == invitee.Id))
                throw new ConflictException($"{username} is already a member of this league.");

            var existing = await _leagues.GetPendingInvitationAsync(leagueId, invitee.Id);
            if (existing != null)
                throw new ConflictException($"A pending invitation to {invitee.Username} already exists.");

            var now = _time.GetUtcNow().UtcDateTime;
            var lastDeclinedAt = await _leagues.GetLastDeclinedAtAsync(leagueId, invitee.Id);
            if (lastDeclinedAt.HasValue && lastDeclinedAt.Value > now - ReinviteCooldown)
                throw new ConflictException($"{invitee.Username} declined an invitation to this league recently. Try again after 24 hours.");

            var inviter = await _users.GetByIdAsync(inviterId)
                ?? throw new KeyNotFoundException("Inviter not found.");

            LeagueInvitation invitation;
            try
            {
                invitation = await _leagues.CreateInvitationAsync(new LeagueInvitation
                {
                    LeagueId = leagueId,
                    InviterId = inviterId,
                    InviteeId = invitee.Id,
                    Status = LeagueInvitationStatus.Pending,
                    CreatedAt = now
                });
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                // A concurrent request created the pending invite first (filtered unique index).
                throw new ConflictException($"A pending invitation to {invitee.Username} already exists.");
            }

            // Notify the invitee
            await _notifications.CreateBatchAsync(new List<Notification>
            {
                new Notification
                {
                    UserId = invitee.Id,
                    GameId = null,
                    ScoringDay = _time.GetUtcNow().UtcDateTime.Date,
                    Message = $"{inviter.Username} invited you to join the league \"{league.Name}\".",
                    Type = "league_invite",
                    ActorUserId = inviterId,
                    CreatedAt = _time.GetUtcNow().UtcDateTime
                }
            });

            invitation.League = league;
            invitation.Inviter = inviter;
            invitation.Invitee = invitee;
            return InvitationToDto(invitation);
        }

        public async Task<LeagueInvitationDto> CreateInviteLinkAsync(int leagueId, int inviterId)
        {
            var league = await _leagues.GetByIdWithMembersAsync(leagueId)
                ?? throw new KeyNotFoundException("League not found.");

            RequireOwner(league, inviterId);

            var now = _time.GetUtcNow().UtcDateTime;
            LeagueInvitation? invitation = null;
            // Tokens are unique (index). A collision of random GUIDs is practically impossible, but retry anyway.
            for (var attempt = 0; invitation == null; attempt++)
            {
                try
                {
                    invitation = await _leagues.CreateInvitationAsync(new LeagueInvitation
                    {
                        LeagueId = leagueId,
                        InviterId = inviterId,
                        InviteeId = null,
                        Token = GenerateToken(),
                        Status = LeagueInvitationStatus.Pending,
                        CreatedAt = now,
                        ExpiresAt = now.AddDays(7)
                    });
                }
                catch (DbUpdateException ex) when (ex.IsUniqueViolation() && attempt < 2) { }
            }

            var inviter = await _users.GetByIdAsync(inviterId);
            invitation.League = league;
            invitation.Inviter = inviter;
            return InvitationToDto(invitation);
        }

        public async Task<LeagueDetailDto> AcceptInvitationAsync(int invitationId, int userId)
        {
            var invitation = await _leagues.GetInvitationByIdAsync(invitationId)
                ?? throw new KeyNotFoundException("Invitation not found.");

            if (invitation.InviteeId != userId)
                throw new ForbiddenException("This invitation was not sent to you.");

            if (invitation.Status != LeagueInvitationStatus.Pending)
                throw new ConflictException("This invitation is no longer pending.");

            return await AcceptInvitationCoreAsync(invitation, userId, "This invitation is no longer pending.");
        }

        public async Task<LeagueDetailDto> JoinByTokenAsync(string token, int userId)
        {
            var invitation = await _leagues.GetInvitationByTokenAsync(token)
                ?? throw new KeyNotFoundException("Invite link not found or already used.");

            if (invitation.ExpiresAt.HasValue && invitation.ExpiresAt.Value < _time.GetUtcNow().UtcDateTime)
                throw new ValidationException("This invite link has expired.");

            return await AcceptInvitationCoreAsync(invitation, userId, "Invite link not found or already used.");
        }

        private async Task<LeagueDetailDto> AcceptInvitationCoreAsync(LeagueInvitation invitation, int userId, string notPendingMessage)
        {
            var league = await _leagues.GetByIdAsync(invitation.LeagueId)
                ?? throw new KeyNotFoundException("League not found.");

            // Claims the invitation and adds the membership atomically; a concurrent accept gets NotPending.
            var result = await _leagues.AcceptInvitationAsync(invitation.Id, userId, _time.GetUtcNow().UtcDateTime);
            if (result == AcceptInvitationResult.AlreadyMember)
                throw new ConflictException("You are already a member of this league.");
            if (result == AcceptInvitationResult.NotPending)
                throw new ConflictException(notPendingMessage);

            // Notify the owner
            var joiner = await _users.GetByIdAsync(userId);
            await _notifications.CreateBatchAsync(new List<Notification>
            {
                new Notification
                {
                    UserId = league.OwnerId,
                    GameId = null,
                    ScoringDay = _time.GetUtcNow().UtcDateTime.Date,
                    Message = $"{joiner?.Username ?? "Someone"} joined your league \"{league.Name}\".",
                    Type = "league_joined",
                    ActorUserId = userId,
                    CreatedAt = _time.GetUtcNow().UtcDateTime
                }
            });

            return await GetLeagueDetailAsync(league.Id, userId);
        }

        public async Task DeclineInvitationAsync(int invitationId, int userId)
        {
            var invitation = await _leagues.GetInvitationByIdAsync(invitationId)
                ?? throw new KeyNotFoundException("Invitation not found.");

            if (invitation.InviteeId != userId)
                throw new ForbiddenException("This invitation was not sent to you.");

            if (invitation.Status != LeagueInvitationStatus.Pending)
                throw new ConflictException("This invitation is no longer pending.");

            if (!await _leagues.TryCloseInvitationAsync(invitation.Id, LeagueInvitationStatus.Declined, _time.GetUtcNow().UtcDateTime))
                throw new ConflictException("This invitation is no longer pending.");
        }

        public async Task CancelInvitationAsync(int invitationId, int requestingUserId)
        {
            var invitation = await _leagues.GetInvitationByIdAsync(invitationId)
                ?? throw new KeyNotFoundException("Invitation not found.");

            var league = await _leagues.GetByIdAsync(invitation.LeagueId)
                ?? throw new KeyNotFoundException("League not found.");

            if (league.OwnerId != requestingUserId)
                throw new ForbiddenException("Only the league owner can cancel invitations.");

            if (invitation.Status != LeagueInvitationStatus.Pending)
                throw new ConflictException("This invitation is no longer pending.");

            if (!await _leagues.TryCloseInvitationAsync(invitation.Id, LeagueInvitationStatus.Cancelled, _time.GetUtcNow().UtcDateTime))
                throw new ConflictException("This invitation is no longer pending.");
        }

        public async Task<List<LeagueInvitationDto>> GetMyPendingInvitationsAsync(int userId)
        {
            var invitations = await _leagues.GetPendingInvitationsForUserAsync(userId);
            return invitations.Select(InvitationToDto).ToList();
        }

        // ── Membership ────────────────────────────────────────────────────────

        public async Task LeaveLeagueAsync(int leagueId, int userId)
        {
            var league = await _leagues.GetByIdAsync(leagueId)
                ?? throw new KeyNotFoundException("League not found.");

            if (league.OwnerId == userId)
                throw new ValidationException("The owner cannot leave the league. Transfer ownership or delete the league.");

            var member = await _leagues.GetMemberAsync(leagueId, userId)
                ?? throw new ForbiddenException("You are not a member of this league.");

            await _leagues.RemoveMemberAsync(member);
        }

        public async Task KickMemberAsync(int leagueId, int requestingUserId, int targetUserId)
        {
            var league = await _leagues.GetByIdAsync(leagueId)
                ?? throw new KeyNotFoundException("League not found.");

            RequireOwner(league, requestingUserId);

            if (targetUserId == requestingUserId)
                throw new ValidationException("The owner cannot kick themselves.");

            var member = await _leagues.GetMemberAsync(leagueId, targetUserId)
                ?? throw new NotFoundException("The target user is not a member of this league.");

            await _leagues.RemoveMemberAsync(member);
        }

        // ── Leaderboard ───────────────────────────────────────────────────────

        public async Task<LeagueLeaderboardDto> GetLeaderboardAsync(int leagueId, int gameId, DateTime? scoringDay, int requestingUserId, bool isAdmin = false)
        {
            var league = await _leagues.GetByIdAsync(leagueId)
                ?? throw new KeyNotFoundException("League not found.");

            var member = await _leagues.GetMemberAsync(leagueId, requestingUserId);
            if (member == null)
                throw new ForbiddenException("You are not a member of this league.");

            var game = await _games.GetByIdWithoutBlobAsync(gameId)
                ?? throw new KeyNotFoundException("Game not found.");

            // Default day: the game's current scoring day (not the UTC calendar date).
            var currentDay = _visibility.GetCurrentScoringDay(game);
            var day = scoringDay?.Date ?? currentDay;
            var isCurrentDay = day == currentDay;
            var visible = await _visibility.CanSeeDayAsync(requestingUserId, isAdmin, game, day);

            var strategy = RankingStrategyFactory.GetStrategy(game.RankingMode);
            var entries = await _leagues.GetLeaderboardAsync(leagueId, gameId, day, strategy);

            var dto = new LeagueLeaderboardDto
            {
                LeagueId = leagueId,
                GameId = gameId,
                ScoringDay = day.ToString("yyyy-MM-dd"),
                ScoresHidden = !visible,
            };

            dto.Entries = visible
                ? entries.Select(e => new LeagueLeaderboardEntryDto
                {
                    Rank = e.Rank,
                    UserId = e.UserId,
                    Username = e.Username,
                    Score = e.Score,
                    ScoreValue = e.ScoreValue,
                    SubmissionId = e.SubmissionId,
                    ScreenshotUrl = e.HasScreenshot ? _signer.Url(e.SubmissionId, isCurrentDay) : null
                }).ToList()
                // Hidden: who played stays visible, but not the score, rank, or order.
                : entries
                    .OrderBy(e => e.Username, StringComparer.OrdinalIgnoreCase)
                    .Select(e => new LeagueLeaderboardEntryDto { UserId = e.UserId, Username = e.Username })
                    .ToList();

            return dto;
        }

        public async Task<(List<LeagueGameSummaryDto> Items, int TotalCount)> GetLeagueGameSummariesAsync(int leagueId, int requestingUserId, int days = 7, int page = 1, int pageSize = 20)
        {
            var league = await _leagues.GetByIdAsync(leagueId)
                ?? throw new KeyNotFoundException("League not found.");

            var member = await _leagues.GetMemberAsync(leagueId, requestingUserId);
            if (member == null)
                throw new ForbiddenException("You are not a member of this league.");

            return await _leagues.GetLeagueGameSummariesAsync(leagueId, requestingUserId, days, page, pageSize);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void RequireOwner(League league, int userId)
        {
            if (league.OwnerId != userId)
                throw new ForbiddenException("Only the league owner can perform this action.");
        }

        private static void RequireMember(League league, int userId)
        {
            if (!league.Members.Any(m => m.UserId == userId))
                throw new ForbiddenException("You are not a member of this league.");
        }

        private static LeagueDto LeagueToDto(League l, int memberCount, string ownerUsername) =>
            new LeagueDto
            {
                Id = l.Id,
                Name = l.Name,
                OwnerId = l.OwnerId,
                OwnerUsername = ownerUsername,
                MemberCount = memberCount,
                CreatedAt = l.CreatedAt
            };

        private static LeagueInvitationDto InvitationToDto(LeagueInvitation i) =>
            new LeagueInvitationDto
            {
                Id = i.Id,
                LeagueId = i.LeagueId,
                LeagueName = i.League?.Name ?? string.Empty,
                InviterId = i.InviterId,
                InviterUsername = i.Inviter?.Username ?? string.Empty,
                InviteeId = i.InviteeId,
                InviteeUsername = i.Invitee?.Username,
                Token = i.Token,
                Status = i.Status.ToString().ToLowerInvariant(),
                CreatedAt = i.CreatedAt,
                ExpiresAt = i.ExpiresAt
            };

        private static string GenerateToken() =>
            Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                .Replace("+", "-").Replace("/", "_").Replace("=", "")
                .Substring(0, 22);
    }
}
