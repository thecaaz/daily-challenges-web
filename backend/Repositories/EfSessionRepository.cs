using DailyChallenges.Data;
using DailyChallenges.Models;
using DailyChallenges.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories;

public sealed class EfSessionRepository(AppDbContext db) : ISessionRepository
{
    public async Task<bool> TryRevokeAsync(RevokedRefreshToken revocation, CancellationToken ct)
    {
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT OR IGNORE INTO RevokedRefreshTokens (Jti, ExpiresAt, RevokedAt, Reason) VALUES ({revocation.Jti}, {revocation.ExpiresAt}, {revocation.RevokedAt}, {(int)revocation.Reason})", ct);
        return inserted == 1;
    }

    public Task<RevokedRefreshToken?> GetRevocationAsync(string jti, CancellationToken ct) =>
        db.RevokedRefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.Jti == jti, ct);

    public async Task<bool> EndAllSessionsAsync(int userId, int tokenVersion, CancellationToken ct)
    {
        var bumped = await db.Users
            .Where(u => u.Id == userId && u.TokenVersion == tokenVersion)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.TokenVersion, u => u.TokenVersion + 1), ct);
        return bumped == 1;
    }

    public Task<int> DeleteExpiredRevocationsAsync(DateTime nowUtc, CancellationToken ct) =>
        db.RevokedRefreshTokens.Where(t => t.ExpiresAt < nowUtc).ExecuteDeleteAsync(ct);
}
