using DailyChallenges.Models;
namespace DailyChallenges.Repositories;

public interface IGameRepository
{
    /// <summary>All games without the image blob (read-only copies).</summary>
    Task<List<Game>> GetAllAsync(CancellationToken ct);
    /// <summary>Tracked, full entity (including the image blob). For write paths.</summary>
    Task<Game?> GetByIdAsync(int id, CancellationToken ct);
    /// <summary>Read-only copy without the image blob. For read paths (visibility checks, DTOs).</summary>
    Task<Game?> GetByIdWithoutBlobAsync(int id, CancellationToken ct);
    /// <summary>Only the image bytes and content type; (null, null) when the game does not exist.</summary>
    Task<(byte[]? Data, string? ContentType)> GetBlobAsync(int id, CancellationToken ct);
    Task<Game> CreateAsync(Game game, CancellationToken ct);
    Task<Game> UpdateAsync(Game game, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}
