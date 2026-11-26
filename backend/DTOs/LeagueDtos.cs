namespace DailyChallenges.DTOs;

public sealed record LeagueDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int OwnerId { get; init; }
    public string OwnerUsername { get; init; } = string.Empty;
    public int MemberCount { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record LeagueMemberDto
{
    public int UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public int Level { get; init; }
    public int Streak { get; init; }
    public string Role { get; init; } = string.Empty;
    public DateTime JoinedAt { get; init; }
}

public sealed record LeagueInvitationDto
{
    public int Id { get; init; }
    public int LeagueId { get; init; }
    public string LeagueName { get; init; } = string.Empty;
    public int InviterId { get; init; }
    public string InviterUsername { get; init; } = string.Empty;
    public int? InviteeId { get; init; }
    public string? InviteeUsername { get; init; }
    public string? Token { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
}

public sealed record LeagueDetailDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int OwnerId { get; init; }
    public string OwnerUsername { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public List<LeagueMemberDto> Members { get; init; } = new();
    public List<LeagueInvitationDto> PendingInvitations { get; init; } = new();
}

/// <summary>Rank, Score, ScoreValue, SubmissionId and ScreenshotUrl are null when the scores are hidden.</summary>
public sealed record LeagueLeaderboardEntryDto
{
    public int? Rank { get; init; }
    public int UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string? Score { get; init; }
    public double? ScoreValue { get; init; }
    public int? SubmissionId { get; init; }
    public string? ScreenshotUrl { get; init; }
}

public sealed record LeagueLeaderboardDto
{
    public int LeagueId { get; init; }
    public int GameId { get; init; }
    public string ScoringDay { get; init; } = string.Empty;
    public List<LeagueLeaderboardEntryDto> Entries { get; init; } = new();
    /// <summary>True when the day is the game's current scoring day and the caller may not see its scores.</summary>
    public bool ScoresHidden { get; init; }
}


public sealed record LeagueCreateRequest
{
    public string Name { get; init; } = string.Empty;
}

public sealed record LeagueRenameRequest
{
    public string Name { get; init; } = string.Empty;
}

public sealed record LeagueInviteByUsernameRequest
{
    public string Username { get; init; } = string.Empty;
}

public sealed record LeagueGameSummaryDto
{
    public int GameId { get; init; }
    public string GameName { get; init; } = string.Empty;
    public string? IconUrl { get; init; }
    public DateTime LastPlayedAt { get; init; }
    public int PlayCount { get; init; }
    public string? TopScore { get; init; }
    public double? TopScoreValue { get; init; }
    public string? MyBestScore { get; init; }
    public double? MyBestScoreValue { get; init; }
    public int? MyRank { get; init; }
    /// <summary>Counts per day, oldest->newest.</summary>
    public List<int> RecentPlays { get; init; } = new();
    /// <summary>The game's scoring days ("yyyy-MM-dd", oldest->newest) that RecentPlays counts, index for index.</summary>
    public List<string> RecentPlayDays { get; init; } = new();
}
