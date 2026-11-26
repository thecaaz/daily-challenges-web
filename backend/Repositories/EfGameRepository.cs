using DailyChallenges.Data;
using DailyChallenges.Exceptions;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories;

public sealed class EfGameRepository(AppDbContext db) : IGameRepository
{
    public Task<List<Game>> GetAllAsync() =>
        db.Games.SelectWithoutBlob().ToListAsync();

    /// <summary>Tracked, full entity (including the image blob). Write paths rely on tracking.</summary>
    public Task<Game?> GetByIdAsync(int id) =>
        db.Games.FirstOrDefaultAsync(g => g.Id == id);

    public Task<Game?> GetByIdWithoutBlobAsync(int id) =>
        db.Games.Where(g => g.Id == id).SelectWithoutBlob().FirstOrDefaultAsync();

    public async Task<(byte[]? Data, string? ContentType)> GetBlobAsync(int id)
    {
        var row = await db.Games
            .Where(g => g.Id == id)
            .Select(g => new { g.ScreenshotData, g.ScreenshotContentType })
            .FirstOrDefaultAsync();
        return row == null ? (null, null) : (row.ScreenshotData, row.ScreenshotContentType);
    }

    public async Task<Game> CreateAsync(Game game)
    {
        db.Games.Add(game);
        await db.SaveChangesAsync();
        return game;
    }

    /// <summary>Copies the editable fields; the image only when a new, non-empty one is given.</summary>
    public async Task<Game> UpdateAsync(Game game)
    {
        var existing = await db.Games.FirstOrDefaultAsync(g => g.Id == game.Id)
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

        await db.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int id)
    {
        var existing = await db.Games.FindAsync(id);
        if (existing == null) return;

        db.Games.Remove(existing);
        await db.SaveChangesAsync();
    }
}
