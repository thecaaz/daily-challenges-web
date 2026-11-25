using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DailyChallenges.Data.Configurations;

/// <summary>
/// Usernames are unique case-insensitively: NOCASE on the column makes both the unique index
/// and plain <c>Username == x</c> lookups ignore ASCII case.
/// </summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    private const string CaseInsensitiveCollation = "NOCASE";

    public void Configure(EntityTypeBuilder<User> user)
    {
        user.Property(u => u.Username).UseCollation(CaseInsensitiveCollation);
        user.HasIndex(u => u.Username).IsUnique();
    }
}
