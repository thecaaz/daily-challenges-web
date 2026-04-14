using DailyChallenges.DTOs;
using DailyChallenges.Models;
using DailyChallenges.Services;

namespace DailyChallenges.Mapping
{
    public static class DtoMapper
    {
        /// <summary>Maps a User entity to a UserDto, computing XP-into-level and XP-to-next-level.</summary>
        public static UserDto ToDto(User u, LevelCalculator levelCalc)
        {
            var (level, xpInto, xpToNext) = levelCalc.GetLevelInfo(u.TotalXp);
            return new UserDto
            {
                Id = u.Id,
                Username = u.Username,
                IsAdmin = u.IsAdmin,
                TotalXp = u.TotalXp,
                Level = level,
                XpIntoLevel = xpInto,
                XpToNextLevel = xpToNext,
                Streak = u.Streak,
                LastSubmissionAt = u.LastSubmissionAt
            };
        }

        public static SubmissionDto ToDto(Submission s)
        {
            return new SubmissionDto
            {
                Id = s.Id,
                GameId = s.GameId,
                UserId = s.UserId,
                Score = s.Score,
                ScoreValue = s.ScoreValue,
                Rank = null,
                Username = s.Username,
                ScreenshotUrl = s.ScreenshotData != null ? $"/api/submissions/{s.Id}/screenshot" : null,
                CreatedAt = s.CreatedAt
            };
        }

        public static GameDto ToDto(Game g, bool includeSubmissions = false)
        {
            var dto = new GameDto
            {
                Id = g.Id,
                Name = g.Name,
                Url = g.Url,
                Description = g.Description,
                ImageUrl = g.ScreenshotData != null ? $"/api/games/{g.Id}/image" : null,
                // ResetTime is exported as HH:mm (UTC)
                ResetTime = g.ResetTime.ToString(@"hh\:mm"),
                RankingMode = g.RankingMode.ToString().ToLowerInvariant(),
                Submissions = includeSubmissions && g.Submissions != null
                    ? g.Submissions.Select(ToDto).ToList()
                    : null
            };

            try
            {
                // Compute current scoring day on the server side (ResetTime is UTC)
                var currentScoringDay = ScoringDayHelper.GetCurrentScoringDay(g.ResetTime);
                dto.CurrentScoringDay = currentScoringDay.ToString("yyyy-MM-dd");
                var (utcStart, utcEnd) = ScoringDayHelper.GetScoringDayUtcRange(currentScoringDay, g.ResetTime);
                dto.CurrentScoringDayUtcStart = utcStart.ToString("yyyy-MM-ddTHH:mm:ss'Z'");
                dto.CurrentScoringDayUtcEnd = utcEnd.ToString("yyyy-MM-ddTHH:mm:ss'Z'");
            }
            catch
            {
                dto.CurrentScoringDay = null;
                dto.CurrentScoringDayUtcStart = null;
                dto.CurrentScoringDayUtcEnd = null;
            }

            return dto;
        }

        public static XpEventDto ToDto(XpEvent e)
        {
            return new XpEventDto
            {
                Id = e.Id,
                UserId = e.UserId,
                SubmissionId = e.SubmissionId,
                GameId = e.GameId,
                ScoringDay = e.ScoringDay,
                Amount = e.Amount,
                EventType = e.EventType,
                Details = e.Details,
                CreatedAt = e.CreatedAt
            };
        }
        
        public static NotificationDto ToDto(Notification n)
        {
            return new NotificationDto
            {
                Id = n.Id,
                Message = n.Message,
                Type = n.Type,
                GameId = n.GameId,
                ScoringDay = ScoringDayHelper.FormatScoringDay(n.ScoringDay),
                Rank = n.Rank,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            };
        }

        public static UserProfileDto ToUserProfileDto(User u, List<UserGameStatDto> topGames, LevelCalculator levelCalc)
        {
            var (level, xpInto, xpToNext) = levelCalc.GetLevelInfo(u.TotalXp);
            return new UserProfileDto
            {
                UserId = u.Id,
                Username = u.Username,
                TotalXp = u.TotalXp,
                Level = level,
                XpIntoLevel = xpInto,
                XpToNextLevel = xpToNext,
                Streak = u.Streak,
                LastSubmissionAt = topGames?.Max(g => g.LastPlayed) ?? u.LastSubmissionAt,
                TopGames = topGames ?? []
            };
        }

        public static FriendRequestDto ToDto(FriendRequest fr)
        {
            return new FriendRequestDto
            {
                Id = fr.Id,
                SenderId = fr.SenderId,
                SenderUsername = fr.Sender?.Username ?? string.Empty,
                ReceiverId = fr.ReceiverId,
                ReceiverUsername = fr.Receiver?.Username ?? string.Empty,
                Status = fr.Status.ToString().ToLowerInvariant(),
                CreatedAt = fr.CreatedAt
            };
        }

        public static FriendDto ToFriendDto(User u, LevelCalculator levelCalc)
        {
            var (level, _, _) = levelCalc.GetLevelInfo(u.TotalXp);
            return new FriendDto
            {
                UserId = u.Id,
                Username = u.Username,
                Level = level,
                TotalXp = u.TotalXp,
                Streak = u.Streak,
                LastSubmissionAt = u.LastSubmissionAt
            };
        }
    }
}
