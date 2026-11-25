using DailyChallenges.Data;
using DailyChallenges.Models;
using DailyChallenges.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Repositories;

public sealed class EfSessionRepository(AppDbContext db) : ISessionRepository
{
    public async Task<bool> TryRevokeAsync(RevokedRefreshToken revocation)
    {
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT OR IGNORE INTO RevokedRefreshTokens (Jti, ExpiresAt, RevokedAt, Reason) VALUES ({revocation.Jti}, {revocation.ExpiresAt}, {revocation.RevokedAt}, {(int)revocation.Reason})");
        return inserted == 1;
    }

    public Task<RevokedRefreshToken?> GetRevocationAsync(string jti) =>
        db.RevokedRefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.Jti == jti);

    public async Task<bool> EndAllSessionsAsync(int userId, int tokenVersion)
    {
        var bumped = await db.Users
            .Where(u => u.Id == userId && u.TokenVersion == tokenVersion)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.TokenVersion, u => u.TokenVersion + 1));
        return bumped == 1;
    }
}
