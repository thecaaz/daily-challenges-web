using DailyChallenges.DTOs;

namespace DailyChallenges.Services;

public interface IAdminXpEventService
{
    /// <summary>One page of the user's XP events; <paramref name="from"/> is inclusive, <paramref name="to"/> exclusive.</summary>
    Task<(List<XpEventDto> Items, int TotalCount)> GetXpEventsAsync(int userId, int page = 1, int pageSize = 20, DateTime? from = null, DateTime? to = null, string? eventType = null);

    /// <summary>Up to <paramref name="maxRows"/> XP events as a UTF-8 CSV file, with formula-like text cells neutralized.</summary>
    Task<(byte[] Data, string Filename)> ExportXpEventsCsvAsync(int userId, DateTime? from = null, DateTime? to = null, string? eventType = null, int maxRows = 10000);
}
