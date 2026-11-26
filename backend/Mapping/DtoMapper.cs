using DailyChallenges.DTOs;
using DailyChallenges.Models;
using DailyChallenges.Services;

namespace DailyChallenges.Mapping;

public static class DtoMapper
{
    private const string ResetTimeFormat = @"hh\:mm";
    private const string UtcTimestampFormat = "yyyy-MM-ddTHH:mm:ss'Z'";

    /// <summary>
    /// Maps a User entity to a UserDto with its <see cref="UserProgression"/>.
    /// <paramref name="todayUtc"/> (from the caller's TimeProvider) decides the effective streak.
    /// </summary>
    public static UserDto ToDto(User user, LevelCalculator levels, DateTime todayUtc)
    {
        var progression = UserProgression.Of(user, levels, todayUtc);
        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            IsAdmin = user.IsAdmin,
            TotalXp = user.TotalXp,
            Level = progression.Level,
            XpIntoLevel = progression.XpIntoLevel,
            XpToNextLevel = progression.XpToNextLevel,
            Streak = progression.Streak,
            LastSubmissionAt = user.LastSubmissionAt
        };
    }

    /// <summary>Signals a screenshot by its content type, not the blob (list queries do not load the blob).</summary>
    public static SubmissionDto ToDto(Submission submission) => new()
    {
        Id = submission.Id,
        GameId = submission.GameId,
        UserId = submission.UserId,
        Score = submission.Score,
        ScoreValue = submission.ScoreValue,
        Rank = null,
        Username = submission.Username,
        ScreenshotUrl = SubmissionScreenshotUrl.For(submission.Id, submission.ScreenshotContentType),
        CreatedAt = submission.CreatedAt
    };

    /// <summary>
    /// Maps a game, including its current scoring day at <paramref name="nowUtc"/> (from the caller's TimeProvider).
    /// ResetTime is UTC and exported as "hh:mm"; the image is signalled by its content type, not the blob.
    /// </summary>
    public static GameDto ToDto(Game game, DateTime nowUtc)
    {
        var window = CurrentScoringDayWindow.For(game.ResetTime, nowUtc);
        return new GameDto
        {
            Id = game.Id,
            Name = game.Name,
            Url = game.Url,
            Description = game.Description,
            ImageUrl = GameImageUrl.For(game.Id, game.ScreenshotContentType),
            ResetTime = game.ResetTime.ToString(ResetTimeFormat),
            RankingMode = game.RankingMode.ToString().ToLowerInvariant(),
            CurrentScoringDay = window.Day,
            CurrentScoringDayUtcStart = window.UtcStart,
            CurrentScoringDayUtcEnd = window.UtcEnd
        };
    }

    public static XpEventDto ToDto(XpEvent xpEvent) => new()
    {
        Id = xpEvent.Id,
        UserId = xpEvent.UserId,
        SubmissionId = xpEvent.SubmissionId,
        GameId = xpEvent.GameId,
        ScoringDay = xpEvent.ScoringDay.HasValue ? ScoringDayCalculator.FormatScoringDay(xpEvent.ScoringDay.Value) : null,
        Amount = xpEvent.Amount,
        EventType = xpEvent.EventType,
        Details = xpEvent.Details,
        CreatedAt = xpEvent.CreatedAt
    };

    public static NotificationDto ToDto(Notification notification) => new()
    {
        Id = notification.Id,
        Message = notification.Message,
        Type = notification.Type,
        GameId = notification.GameId,
        ScoringDay = ScoringDayCalculator.FormatScoringDay(notification.ScoringDay),
        Rank = notification.Rank,
        ActorUserId = notification.ActorUserId,
        IsRead = notification.IsRead,
        CreatedAt = notification.CreatedAt
    };

    public static UserProfileDto ToUserProfileDto(User user, UserProgression progression, List<UserGameStatDto> topGames) => new()
    {
        UserId = user.Id,
        Username = user.Username,
        TotalXp = user.TotalXp,
        Level = progression.Level,
        XpIntoLevel = progression.XpIntoLevel,
        XpToNextLevel = progression.XpToNextLevel,
        Streak = progression.Streak,
        LastSubmissionAt = user.LastSubmissionAt,
        TopGames = topGames ?? []
    };

    public static FriendRequestDto ToDto(FriendRequest friendRequest) => new()
    {
        Id = friendRequest.Id,
        SenderId = friendRequest.SenderId,
        SenderUsername = friendRequest.Sender?.Username ?? string.Empty,
        ReceiverId = friendRequest.ReceiverId,
        ReceiverUsername = friendRequest.Receiver?.Username ?? string.Empty,
        Status = friendRequest.Status.ToString().ToLowerInvariant(),
        CreatedAt = friendRequest.CreatedAt
    };

    public static FriendDto ToFriendDto(User user, LevelCalculator levels, DateTime todayUtc)
    {
        var progression = UserProgression.Of(user, levels, todayUtc);
        return new FriendDto
        {
            UserId = user.Id,
            Username = user.Username,
            Level = progression.Level,
            TotalXp = user.TotalXp,
            Streak = progression.Streak,
            LastSubmissionAt = user.LastSubmissionAt
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
