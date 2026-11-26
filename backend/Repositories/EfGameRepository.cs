using DailyChallenges.Data;
using DailyChallenges.Exceptions;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories;

public sealed class EfGameRepository(AppDbContext db) : IGameRepository
{
    public Task<List<Game>> GetAllAsync(CancellationToken ct) =>
        db.Games.SelectWithoutBlob().ToListAsync(ct);

    /// <summary>Tracked, full entity (including the image blob). Write paths rely on tracking.</summary>
    public Task<Game?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Games.FirstOrDefaultAsync(g => g.Id == id, ct);

    public Task<Game?> GetByIdWithoutBlobAsync(int id, CancellationToken ct) =>
        db.Games.Where(g => g.Id == id).SelectWithoutBlob().FirstOrDefaultAsync(ct);

    public async Task<(byte[]? Data, string? ContentType)> GetBlobAsync(int id, CancellationToken ct)
    {
        var row = await db.Games
            .Where(g => g.Id == id)
            .Select(g => new { g.ScreenshotData, g.ScreenshotContentType })
            .FirstOrDefaultAsync(ct);
        return row == null ? (null, null) : (row.ScreenshotData, row.ScreenshotContentType);
    }

    public async Task<Game> CreateAsync(Game game, CancellationToken ct)
    {
        db.Games.Add(game);
        await db.SaveChangesAsync(ct);
        return game;
    }

    /// <summary>Copies the editable fields; the image only when a new, non-empty one is given.</summary>
    public async Task<Game> UpdateAsync(Game game, CancellationToken ct)
    {
        var existing = await db.Games.FirstOrDefaultAsync(g => g.Id == game.Id, ct)
            ?? throw new NotFoundException("Game not found");

        existing.Name = game.Name;
        existing.Url = game.Url;
        existing.ResetTime = game.ResetTime;
        existing.Description = game.Description;
        existing.RankingMode = game.RankingMode;
        if (game.ScreenshotData is { Length: > 0 })
        {
            existing.ScreenshotData = game.ScreenshotData;
            existing.ScreenshotContentType = game.ScreenshotContentType;
        }

        await db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var existing = await db.Games.FindAsync([id], ct);
        if (existing == null) return;

        db.Games.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
