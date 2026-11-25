using DailyChallenges.DTOs;
using DailyChallenges.Models;
using DailyChallenges.Services;

namespace DailyChallenges.Mapping;

public static class DtoMapper
{
    private const string ResetTimeFormat = @"hh\:mm";
    private const string UtcTimestampFormat = "yyyy-MM-ddTHH:mm:ss'Z'";

    /// <summary>
    /// Maps a User entity to a UserDto, computing XP-into-level and XP-to-next-level.
    /// <paramref name="todayUtc"/> (from the caller's TimeProvider) decides the effective streak.
    /// </summary>
    public static UserDto ToDto(User u, LevelCalculator levelCalc, DateTime todayUtc)
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
            Streak = StreakCalculator.GetEffectiveStreak(u, todayUtc),
            LastSubmissionAt = u.LastSubmissionAt
        };
    }

    /// <summary>Signals a screenshot by its content type, not the blob (list queries do not load the blob).</summary>
    public static SubmissionDto ToDto(Submission s) => new()
    {
        Id = s.Id,
        GameId = s.GameId,
        UserId = s.UserId,
        Score = s.Score,
        ScoreValue = s.ScoreValue,
        Rank = null,
        Username = s.Username,
        ScreenshotUrl = !string.IsNullOrEmpty(s.ScreenshotContentType) ? $"/api/submissions/{s.Id}/screenshot" : null,
        CreatedAt = s.CreatedAt
    };

    /// <summary>
    /// Maps a game, including its current scoring day. Services pass <paramref name="nowUtc"/> from their
    /// TimeProvider so the DTO uses the same clock as the rest of the request; without it the system clock is used.
    /// ResetTime is UTC and exported as "hh:mm"; the image is signalled by its content type, not the blob.
    /// </summary>
    public static GameDto ToDto(Game g, DateTime? nowUtc = null)
    {
        var window = CurrentScoringDayWindow.For(g.ResetTime, nowUtc ?? DateTime.UtcNow);
        return new GameDto
        {
            Id = g.Id,
            Name = g.Name,
            Url = g.Url,
            Description = g.Description,
            ImageUrl = !string.IsNullOrEmpty(g.ScreenshotContentType) ? $"/api/games/{g.Id}/image" : null,
            ResetTime = g.ResetTime.ToString(ResetTimeFormat),
            RankingMode = g.RankingMode.ToString().ToLowerInvariant(),
            CurrentScoringDay = window.Day,
            CurrentScoringDayUtcStart = window.UtcStart,
            CurrentScoringDayUtcEnd = window.UtcEnd
        };
    }

    /// <summary>Like <see cref="ToDto(Game, DateTime?)"/>, plus the game's loaded submissions (null when not loaded).</summary>
    public static GameDto ToDtoWithSubmissions(Game g, DateTime? nowUtc = null) =>
        ToDto(g, nowUtc) with { Submissions = g.Submissions?.Select(ToDto).ToList() };

    public static XpEventDto ToDto(XpEvent e) => new()
    {
        Id = e.Id,
        UserId = e.UserId,
        SubmissionId = e.SubmissionId,
        GameId = e.GameId,
        ScoringDay = e.ScoringDay.HasValue ? ScoringDayCalculator.FormatScoringDay(e.ScoringDay.Value) : null,
        Amount = e.Amount,
        EventType = e.EventType,
        Details = e.Details,
        CreatedAt = e.CreatedAt
    };

    public static NotificationDto ToDto(Notification n) => new()
    {
        Id = n.Id,
        Message = n.Message,
        Type = n.Type,
        GameId = n.GameId,
        ScoringDay = ScoringDayCalculator.FormatScoringDay(n.ScoringDay),
        Rank = n.Rank,
        ActorUserId = n.ActorUserId,
        IsRead = n.IsRead,
        CreatedAt = n.CreatedAt
    };

    public static UserProfileDto ToUserProfileDto(User u, List<UserGameStatDto> topGames, LevelCalculator levelCalc, DateTime todayUtc)
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
            Streak = StreakCalculator.GetEffectiveStreak(u, todayUtc),
            LastSubmissionAt = u.LastSubmissionAt,
            TopGames = topGames ?? []
        };
    }

    public static FriendRequestDto ToDto(FriendRequest fr) => new()
    {
        Id = fr.Id,
        SenderId = fr.SenderId,
        SenderUsername = fr.Sender?.Username ?? string.Empty,
        ReceiverId = fr.ReceiverId,
        ReceiverUsername = fr.Receiver?.Username ?? string.Empty,
        Status = fr.Status.ToString().ToLowerInvariant(),
        CreatedAt = fr.CreatedAt
    };

    public static FriendDto ToFriendDto(User u, LevelCalculator levelCalc, DateTime todayUtc)
    {
        var (level, _, _) = levelCalc.GetLevelInfo(u.TotalXp);
        return new FriendDto
        {
            UserId = u.Id,
            Username = u.Username,
            Level = level,
            TotalXp = u.TotalXp,
            Streak = StreakCalculator.GetEffectiveStreak(u, todayUtc),
            LastSubmissionAt = u.LastSubmissionAt
        };
    }

    /// <summary>A game's current scoring day ("yyyy-MM-dd") and its UTC range (start inclusive, end exclusive).</summary>
    private sealed record CurrentScoringDayWindow(string? Day, string? UtcStart, string? UtcEnd)
    {
        private static readonly CurrentScoringDayWindow Unknown = new(null, null, null);

        /// <summary>Unknown when the reset time pushes the range outside the representable dates.</summary>
        public static CurrentScoringDayWindow For(TimeSpan resetTimeUtc, DateTime nowUtc)
        {
            try
            {
                var day = ScoringDayCalculator.GetCurrentScoringDay(resetTimeUtc, nowUtc);
                var (utcStart, utcEnd) = ScoringDayCalculator.GetScoringDayUtcRange(day, resetTimeUtc);
                return new CurrentScoringDayWindow(
                    ScoringDayCalculator.FormatScoringDay(day),
                    utcStart.ToString(UtcTimestampFormat),
                    utcEnd.ToString(UtcTimestampFormat));
            }
            catch (ArgumentOutOfRangeException)
            {
                return Unknown;
            }
        }
    }
}
