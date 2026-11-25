using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace DailyChallenges.Services;

/// <summary>The symmetric key for signing and validating JWTs: the UTF-8 bytes of Jwt:Key.</summary>
public static class JwtSigningKey
{
    public static SymmetricSecurityKey From(JwtOptions options) =>
        new(Encoding.UTF8.GetBytes(options.Key ?? string.Empty));
}
