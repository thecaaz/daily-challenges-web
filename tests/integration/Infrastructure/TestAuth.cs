using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DailyChallenges.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DailyChallenges.IntegrationTests.Infrastructure;

/// <summary>Mints access tokens with the same claims AuthService puts in a real login token.</summary>
public static class TestAuth
{
    public static string CreateToken(IServiceProvider services, int userId, string username, bool isAdmin = false, TimeSpan? lifetime = null)
    {
        var jwt = services.GetRequiredService<IOptions<JwtOptions>>().Value;
        return CreateToken(jwt, userId, username, isAdmin, lifetime);
    }

    public static string CreateToken(JwtOptions jwt, int userId, string username, bool isAdmin = false, TimeSpan? lifetime = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username),
            new(TokenClaims.TokenUse, TokenClaims.Access),
            new(TokenClaims.TokenVersion, "0", ClaimValueTypes.Integer32)
        };
        if (isAdmin) claims.Add(new Claim(ClaimTypes.Role, "Admin"));

        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience, claims,
            expires: DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(30)), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
