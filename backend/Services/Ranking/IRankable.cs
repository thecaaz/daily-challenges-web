namespace DailyChallenges.Services.Ranking;

/// <summary>An in-memory row that an <see cref="IRankingStrategy"/> can order.</summary>
public interface IRankable
{
    double? ScoreValue { get; }
    DateTime CreatedAt { get; }
}
