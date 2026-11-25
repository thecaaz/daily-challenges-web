using System.Globalization;
using DailyChallenges.Data;
using DailyChallenges.DTOs;
using DailyChallenges.Exceptions;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Services;

/// <summary>Admin create, update and delete of games. Every request is validated before anything is written.</summary>
public sealed class GameCommandService(
    IGameRepository games,
    IFileStorage files,
    ITransactionRunner transactions,
    GameScoringDayRederiver scoringDays,
    TimeProvider time) : IGameCommandService
{
    private sealed record ParsedSettings(TimeSpan? ResetTime, string? Url, RankingMode? RankingMode);

    private sealed record UploadedImage(byte[]? Data, string? ContentType);

    private sealed record ValidatedUpdate(GameUpdateRequest Request, ParsedSettings Settings, UploadedImage? Image);

    public async Task<GameDto> CreateAsync(GameCreateRequest request)
    {
        var settings = ParseSettings(request.ResetTime, request.Url, request.RankingMode);

        var game = new Game { Name = request.Name };
        if (settings.ResetTime.HasValue) game.ResetTime = settings.ResetTime.Value;
        if (!string.IsNullOrEmpty(settings.Url)) game.Url = settings.Url;
        if (!string.IsNullOrWhiteSpace(request.Description)) game.Description = request.Description;
        if (settings.RankingMode.HasValue) game.RankingMode = settings.RankingMode.Value;

        var image = await ReadImageAsync(request.Image);
        if (image != null)
        {
            game.ScreenshotData = image.Data;
            game.ScreenshotContentType = image.ContentType;
        }

        var created = await games.CreateAsync(game);
        return ToDto(created);
    }

    /// <summary>
    /// A unique violation while saving means two submissions of one user landed on the same scoring day;
    /// it surfaces as the same ConflictException the re-derivation throws for a detected merge.
    /// </summary>
    public async Task<GameDto> UpdateAsync(int id, GameUpdateRequest request)
    {
        var update = new ValidatedUpdate(
            request,
            ParseSettings(request.ResetTime, request.Url, request.RankingMode),
            await ReadImageAsync(request.Image));

        try
        {
            var updated = await transactions.RunAsync(() => ApplyUpdateAsync(id, update));
            return ToDto(updated);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            throw new ConflictException(GameScoringDayRederiver.MergeConflictMessage);
        }
    }

    public async Task DeleteAsync(int id) => await games.DeleteAsync(id);

    /// <summary>
    /// Omitted (null) fields stay unchanged; "" clears Url and Description. A ResetTime change re-derives the
    /// stored ScoringDay of the game's not-yet-finalized submissions in the same transaction.
    /// </summary>
    private async Task<Game> ApplyUpdateAsync(int id, ValidatedUpdate update)
    {
        var (request, settings, image) = update;
        var game = await games.GetByIdAsync(id) ?? throw new KeyNotFoundException("Game not found");

        if (!string.IsNullOrWhiteSpace(request.Name)) game.Name = request.Name;
        if (settings.Url != null) game.Url = settings.Url.Length == 0 ? null : settings.Url;
        if (request.Description != null) game.Description = request.Description.Length == 0 ? null : request.Description;
        if (settings.RankingMode.HasValue) game.RankingMode = settings.RankingMode.Value;
        if (image is { Data.Length: > 0 })
        {
            game.ScreenshotData = image.Data;
            game.ScreenshotContentType = image.ContentType;
        }

        if (settings.ResetTime.HasValue && settings.ResetTime.Value != game.ResetTime)
        {
            await scoringDays.RederiveAsync(game.Id, settings.ResetTime.Value);
            game.ResetTime = settings.ResetTime.Value;
        }

        return await games.UpdateAsync(game);
    }

    private GameDto ToDto(Game game) => DtoMapper.ToDto(game, nowUtc: time.GetUtcNow().UtcDateTime);

    private async Task<UploadedImage?> ReadImageAsync(IFormFile? upload)
    {
        if (upload == null || upload.Length == 0) return null;
        var (data, contentType) = await files.ReadFileAsync(upload);
        return new UploadedImage(data, contentType);
    }

    private static ParsedSettings ParseSettings(string? resetTime, string? url, string? rankingMode) =>
        new(ParseResetTime(resetTime), ParseUrl(url), ParseRankingMode(rankingMode));

    /// <summary>Null/blank → null (unchanged). Otherwise UTC "HH:mm", 00:00–23:59, or a ValidationException.</summary>
    private static TimeSpan? ParseResetTime(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        if (TimeSpan.TryParseExact(input.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var ts)
            && ts >= TimeSpan.Zero && ts < TimeSpan.FromDays(1))
            return ts;
        throw new ValidationException("resetTime must be a UTC time \"HH:mm\" between 00:00 and 23:59.");
    }

    /// <summary>
    /// Null → null (unchanged). "" (or blank) → "" (clear). Otherwise an absolute http(s) URL,
    /// or a ValidationException.
    /// </summary>
    private static string? ParseUrl(string? input)
    {
        if (input == null) return null;
        var trimmed = input.Trim();
        if (trimmed.Length == 0) return string.Empty;
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrEmpty(uri.Host))
            return trimmed;
        throw new ValidationException("url must be an absolute http:// or https:// URL.");
    }

    /// <summary>Null/blank → null (unchanged). Otherwise a RankingMode name (case-insensitive), or a ValidationException.</summary>
    private static RankingMode? ParseRankingMode(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var name = Enum.GetNames<RankingMode>().FirstOrDefault(n => string.Equals(n, input.Trim(), StringComparison.OrdinalIgnoreCase));
        if (name == null)
            throw new ValidationException($"rankingMode must be one of: {string.Join(", ", Enum.GetNames<RankingMode>().Select(n => n.ToLowerInvariant()))}.");
        return Enum.Parse<RankingMode>(name);
    }
}
