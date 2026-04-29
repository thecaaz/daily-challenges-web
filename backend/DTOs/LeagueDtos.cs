namespace DailyChallenges.DTOs
{
    public class LeagueDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int OwnerId { get; set; }
        public string OwnerUsername { get; set; } = string.Empty;
        public int MemberCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class LeagueMemberDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public int Level { get; set; }
        public int Streak { get; set; }
        public string Role { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }
    }

    public class LeagueInvitationDto
    {
        public int Id { get; set; }
        public int LeagueId { get; set; }
        public string LeagueName { get; set; } = string.Empty;
        public int InviterId { get; set; }
        public string InviterUsername { get; set; } = string.Empty;
        public int? InviteeId { get; set; }
        public string? InviteeUsername { get; set; }
        public string? Token { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class LeagueDetailDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int OwnerId { get; set; }
        public string OwnerUsername { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<LeagueMemberDto> Members { get; set; } = new();
        public List<LeagueInvitationDto> PendingInvitations { get; set; } = new();
    }

    public class LeagueLeaderboardEntryDto
    {
        public int Rank { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Score { get; set; } = string.Empty;
        public int ScoreValue { get; set; }
    }

    public class LeagueLeaderboardDto
    {
        public int LeagueId { get; set; }
        public int GameId { get; set; }
        public string ScoringDay { get; set; } = string.Empty;
        public List<LeagueLeaderboardEntryDto> Entries { get; set; } = new();
    }

    // ── Request bodies ────────────────────────────────────────────────────────

    public class LeagueCreateRequest
    {
        public string Name { get; set; } = string.Empty;
    }

    public class LeagueRenameRequest
    {
        public string Name { get; set; } = string.Empty;
    }

    public class LeagueInviteByUsernameRequest
    {
        public string Username { get; set; } = string.Empty;
    }
}
