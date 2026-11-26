using DailyChallenges.DTOs;

namespace DailyChallenges.Services;

public interface IAdminXpEventService
{
    /// <summary>One page (1-based) of the user's XP events matching <paramref name="query"/>.</summary>
    Task<(List<XpEventDto> Items, int TotalCount)> GetXpEventsAsync(XpEventQuery query, int page, int pageSize, CancellationToken ct);

    /// <summary>
    /// Up to <see cref="AdminXpEventService.MaxExportRows"/> XP events as a UTF-8 CSV file, with formula-like
    /// text cells neutralized.
    /// </summary>
    Task<(byte[] Data, string Filename)> ExportXpEventsCsvAsync(XpEventQuery query, CancellationToken ct);
}

/// <summary>
/// The XP events of one user, optionally filtered: <see cref="From"/> is inclusive, <see cref="To"/> exclusive,
/// and <see cref="EventType"/> matches exactly.
/// </summary>
public sealed record XpEventQuery(int UserId, DateTime? From, DateTime? To, string? EventType);
