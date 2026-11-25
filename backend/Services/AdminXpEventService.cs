using System.Text;
using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Repositories;

namespace DailyChallenges.Services;

public sealed class AdminXpEventService(IXpEventRepository xpEvents) : IAdminXpEventService
{
    private const string CsvHeader = "Id,UserId,SubmissionId,GameId,ScoringDay,Amount,EventType,Details,CreatedAt";

    public async Task<(List<XpEventDto> Items, int TotalCount)> GetXpEventsAsync(int userId, int page = 1, int pageSize = 20, DateTime? from = null, DateTime? to = null, string? eventType = null)
    {
        var (items, total) = await xpEvents.GetByUserPagedAsync(userId, page, pageSize, from, to, eventType);
        var dtos = items.Select(e => DtoMapper.ToDto(e)).ToList();
        return (dtos, total);
    }

    public async Task<(byte[] Data, string Filename)> ExportXpEventsCsvAsync(int userId, DateTime? from = null, DateTime? to = null, string? eventType = null, int maxRows = 10000)
    {
        var (items, _) = await GetXpEventsAsync(userId, 1, maxRows, from, to, eventType);

        var csv = new StringBuilder();
        csv.AppendLine(CsvHeader);
        foreach (var item in items)
            csv.AppendLine(ToCsvLine(item));

        return (Encoding.UTF8.GetBytes(csv.ToString()), $"xp-events-user-{userId}.csv");
    }

    /// <summary>
    /// Quotes a text cell for CSV. Cells starting with = + - @ (or tab / CR) get a leading apostrophe so
    /// spreadsheet apps don't evaluate them as formulas (CSV injection).
    /// </summary>
    public static string EscapeCsvText(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        if (s[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            s = "'" + s;
        return '"' + s.Replace("\"", "\"\"") + '"';
    }

    private static string ToCsvLine(XpEventDto item) => string.Join(',',
        item.Id.ToString(),
        item.UserId.ToString(),
        item.SubmissionId?.ToString() ?? string.Empty,
        item.GameId?.ToString() ?? string.Empty,
        item.ScoringDay ?? string.Empty,
        item.Amount.ToString(),
        EscapeCsvText(item.EventType),
        EscapeCsvText(item.Details),
        EscapeCsvText(item.CreatedAt.ToString("o")));
}
