namespace DailyChallenges.Services
{
    public interface IScoringDayFinalizerService
    {
        Task FinalizeScoringDayAsync(int gameId, DateTime scoringDay);
    }
}
