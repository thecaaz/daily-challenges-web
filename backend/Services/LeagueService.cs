using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using DailyChallenges.Repositories.Contracts;
using DailyChallenges.Services.Contracts;
using DailyChallenges.Services.Ranking;

namespace DailyChallenges.Services
{
    public class LeagueService : ILeagueService
    {
        private readonly ILeagueRepository _leagues;
        private readonly IUserProfileRepository _users;
        private readonly INotificationRepository _notifications;
        private readonly IGameRepository _games;

        public LeagueService(
            ILeagueRepository leagues,
            IUserProfileRepository users,
            INotificationRepository notifications,
            IGameRepository games)
        {
            _leagues = leagues;
            _users = users;
            _notifications = notifications;
            _games = games;
        }

        // ── Create / rename / delete ──────────────────────────────────────────

        public async Task<LeagueDto> CreateLeagueAsync(int ownerId, string name)
        {
            name = name.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > 100)
                throw new ArgumentException("League name must be between 1 and 100 characters.");

            var owner = await _users.GetByIdAsync(ownerId)
                ?? throw new KeyNotFoundException("User not found.");

            var league = await _leagues.CreateAsync(new League
            {
                Name = name,
                OwnerId = ownerId,
                CreatedAt = DateTime.UtcNow
            });

            // Add the owner as a member with Owner role
            await _leagues.AddMemberAsync(new LeagueMember
            {
                LeagueId = league.Id,
                UserId = ownerId,
                Role = LeagueRole.Owner,
                JoinedAt = DateTime.UtcNow
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

            league.Name = newName;
            await _leagues.UpdateAsync(league);

            return LeagueToDto(league, league.Members.Count, league.Owner?.Username ?? string.Empty);
        }

        public async Task DeleteLeagueAsync(int leagueId, int requestingUserId)
        {
            var league = await _leagues.GetByIdWithMembersAsync(leagueId)
                ?? throw new KeyNotFoundException("League not found.");

            RequireOwner(league, requestingUserId);

            await _leagues.DeleteAsync(league);
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
                    Streak = m.User?.Streak ?? 0,
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
                throw new InvalidOperationException("You cannot invite yourself.");

            if (league.Members.Any(m => m.UserId == invitee.Id))
                throw new InvalidOperationException($"{username} is already a member of this league.");

            var existing = await _leagues.GetPendingInvitationAsync(leagueId, invitee.Id);
            if (existing != null)
                throw new InvalidOperationException($"A pending invitation to {username} already exists.");

            var inviter = await _users.GetByIdAsync(inviterId)
                ?? throw new KeyNotFoundException("Inviter not found.");

            var invitation = await _leagues.CreateInvitationAsync(new LeagueInvitation
            {
                LeagueId = leagueId,
                InviterId = inviterId,
                InviteeId = invitee.Id,
                Status = LeagueInvitationStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });

            // Notify the invitee
            await _notifications.CreateBatchAsync(new List<Notification>
            {
                new Notification
                {
                    UserId = invitee.Id,
                    GameId = null,
                    ScoringDay = DateTime.UtcNow.Date,
                    Message = $"{inviter.Username} invited you to join the league \"{league.Name}\".",
                    Type = "league_invite",
                    CreatedAt = DateTime.UtcNow
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

            var token = GenerateToken();
            var expiresAt = DateTime.UtcNow.AddDays(7);

            var invitation = await _leagues.CreateInvitationAsync(new LeagueInvitation
            {
                LeagueId = leagueId,
                InviterId = inviterId,
                InviteeId = null,
                Token = token,
                Status = LeagueInvitationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt
            });

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
                throw new InvalidOperationException("This invitation was not sent to you.");

            if (invitation.Status != LeagueInvitationStatus.Pending)
                throw new InvalidOperationException("This invitation is no longer pending.");

            return await AcceptInvitationCoreAsync(invitation, userId);
        }

        public async Task<LeagueDetailDto> JoinByTokenAsync(string token, int userId)
        {
            var invitation = await _leagues.GetInvitationByTokenAsync(token)
                ?? throw new KeyNotFoundException("Invite link not found or already used.");

            if (invitation.ExpiresAt.HasValue && invitation.ExpiresAt.Value < DateTime.UtcNow)
                throw new InvalidOperationException("This invite link has expired.");

            return await AcceptInvitationCoreAsync(invitation, userId);
        }

        private async Task<LeagueDetailDto> AcceptInvitationCoreAsync(LeagueInvitation invitation, int userId)
        {
            // Re-fetch league with members for membership check
            var league = await _leagues.GetByIdWithMembersAsync(invitation.LeagueId)
                ?? throw new KeyNotFoundException("League not found.");

            if (league.Members.Any(m => m.UserId == userId))
                throw new InvalidOperationException("You are already a member of this league.");

            invitation.Status = LeagueInvitationStatus.Accepted;
            invitation.UpdatedAt = DateTime.UtcNow;
            await _leagues.UpdateInvitationAsync(invitation);

            await _leagues.AddMemberAsync(new LeagueMember
            {
                LeagueId = league.Id,
                UserId = userId,
                Role = LeagueRole.Member,
                JoinedAt = DateTime.UtcNow
            });

            // Notify the owner
            var joiner = await _users.GetByIdAsync(userId);
            await _notifications.CreateBatchAsync(new List<Notification>
            {
                new Notification
                {
                    UserId = league.OwnerId,
                    GameId = null,
                    ScoringDay = DateTime.UtcNow.Date,
                    Message = $"{joiner?.Username ?? "Someone"} joined your league \"{league.Name}\".",
                    Type = "league_joined",
                    CreatedAt = DateTime.UtcNow
                }
            });

            return await GetLeagueDetailAsync(league.Id, userId);
        }

        public async Task DeclineInvitationAsync(int invitationId, int userId)
        {
            var invitation = await _leagues.GetInvitationByIdAsync(invitationId)
                ?? throw new KeyNotFoundException("Invitation not found.");

            if (invitation.InviteeId != userId)
                throw new InvalidOperationException("This invitation was not sent to you.");

            if (invitation.Status != LeagueInvitationStatus.Pending)
                throw new InvalidOperationException("This invitation is no longer pending.");

            invitation.Status = LeagueInvitationStatus.Declined;
            invitation.UpdatedAt = DateTime.UtcNow;
            await _leagues.UpdateInvitationAsync(invitation);
        }

        public async Task CancelInvitationAsync(int invitationId, int requestingUserId)
        {
            var invitation = await _leagues.GetInvitationByIdAsync(invitationId)
                ?? throw new KeyNotFoundException("Invitation not found.");

            var league = await _leagues.GetByIdAsync(invitation.LeagueId)
                ?? throw new KeyNotFoundException("League not found.");

            if (league.OwnerId != requestingUserId)
                throw new InvalidOperationException("Only the league owner can cancel invitations.");

            if (invitation.Status != LeagueInvitationStatus.Pending)
                throw new InvalidOperationException("This invitation is no longer pending.");

            invitation.Status = LeagueInvitationStatus.Cancelled;
            invitation.UpdatedAt = DateTime.UtcNow;
            await _leagues.UpdateInvitationAsync(invitation);
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
                throw new InvalidOperationException("The owner cannot leave the league. Transfer ownership or delete the league.");

            var member = await _leagues.GetMemberAsync(leagueId, userId)
                ?? throw new InvalidOperationException("You are not a member of this league.");

            await _leagues.RemoveMemberAsync(member);
        }

        public async Task KickMemberAsync(int leagueId, int requestingUserId, int targetUserId)
        {
            var league = await _leagues.GetByIdAsync(leagueId)
                ?? throw new KeyNotFoundException("League not found.");

            RequireOwner(league, requestingUserId);

            if (targetUserId == requestingUserId)
                throw new InvalidOperationException("The owner cannot kick themselves.");

            var member = await _leagues.GetMemberAsync(leagueId, targetUserId)
                ?? throw new InvalidOperationException("The target user is not a member of this league.");

            await _leagues.RemoveMemberAsync(member);
        }

        // ── Leaderboard ───────────────────────────────────────────────────────

        public async Task<LeagueLeaderboardDto> GetLeaderboardAsync(int leagueId, int gameId, DateTime scoringDay, int requestingUserId)
        {
            var league = await _leagues.GetByIdAsync(leagueId)
                ?? throw new KeyNotFoundException("League not found.");

            var member = await _leagues.GetMemberAsync(leagueId, requestingUserId);
            if (member == null)
                throw new InvalidOperationException("You are not a member of this league.");

            var game = await _games.GetByIdAsync(gameId)
                ?? throw new KeyNotFoundException("Game not found.");

            var strategy = RankingStrategyFactory.GetStrategy(game.RankingMode);
            var entries = await _leagues.GetLeaderboardAsync(leagueId, gameId, scoringDay, strategy);

            return new LeagueLeaderboardDto
            {
                LeagueId = leagueId,
                GameId = gameId,
                ScoringDay = scoringDay.Date.ToString("yyyy-MM-dd"),
                Entries = entries.Select(e => new LeagueLeaderboardEntryDto
                {
                    Rank = e.Rank,
                    UserId = e.UserId,
                    Username = e.Username,
                    Score = e.Score,
                    ScoreValue = e.ScoreValue,
                    SubmissionId = e.SubmissionId,
                    ScreenshotUrl = e.HasScreenshot ? $"/api/submissions/{e.SubmissionId}/screenshot" : null
                }).ToList()
            };
        }

        public async Task<(List<LeagueGameSummaryDto> Items, int TotalCount)> GetLeagueGameSummariesAsync(int leagueId, int requestingUserId, int days = 7, int page = 1, int pageSize = 20)
        {
            var league = await _leagues.GetByIdAsync(leagueId)
                ?? throw new KeyNotFoundException("League not found.");

            var member = await _leagues.GetMemberAsync(leagueId, requestingUserId);
            if (member == null)
                throw new InvalidOperationException("You are not a member of this league.");

            return await _leagues.GetLeagueGameSummariesAsync(leagueId, requestingUserId, days, page, pageSize);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void RequireOwner(League league, int userId)
        {
            if (league.OwnerId != userId)
                throw new InvalidOperationException("Only the league owner can perform this action.");
        }

        private static void RequireMember(League league, int userId)
        {
            if (!league.Members.Any(m => m.UserId == userId))
                throw new InvalidOperationException("You are not a member of this league.");
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
