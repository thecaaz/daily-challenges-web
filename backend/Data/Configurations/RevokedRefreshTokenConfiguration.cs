using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DailyChallenges.Data.Configurations;

/// <summary>Indexed by expiry because the background purge deletes expired revocations.</summary>
internal sealed class RevokedRefreshTokenConfiguration : IEntityTypeConfiguration<RevokedRefreshToken>
{
    public void Configure(EntityTypeBuilder<RevokedRefreshToken> revokedToken) =>
        revokedToken.HasIndex(t => t.ExpiresAt);
}
