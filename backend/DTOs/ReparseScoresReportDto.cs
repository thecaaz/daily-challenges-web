namespace DailyChallenges.DTOs;

/// <summary>Result of POST /api/admin/maintenance/reparse-scores.</summary>
public sealed record ReparseScoresReportDto
{
    /// <summary>True when nothing was written.</summary>
    public bool DryRun { get; init; }
    /// <summary>Submissions examined.</summary>
    public int Total { get; init; }
    /// <summary>Submissions whose ScoreValue differs under the current parser (includes the two counts below).</summary>
    public int Changed { get; init; }
    /// <summary>Had a value, now unparseable: becomes unranked.</summary>
    public int BecameNull { get; init; }
    /// <summary>Had no value, now parses: becomes ranked.</summary>
    public int BecameNumeric { get; init; }
    /// <summary>Up to <see cref="SampleLimit"/> changed rows.</summary>
    public List<ReparseScoreSampleDto> Samples { get; init; } = new();
    public int SampleLimit { get; init; }
    public string Note { get; init; } = string.Empty;
}

public sealed record ReparseScoreSampleDto
{
    public int Id { get; init; }
    public string Score { get; init; } = string.Empty;
    public double? OldValue { get; init; }
    public double? NewValue { get; init; }
}
