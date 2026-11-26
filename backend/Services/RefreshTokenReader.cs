using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DailyChallenges.Services;

/// <summary>
/// Validates signature (HS256 only), issuer, audience and lifetime, and requires token_use=refresh plus
/// a jti (at most 64 chars, the RevokedRefreshTokens key length) and a tv claim. Anything else, including
/// access tokens and pre-upgrade tokens without those claims, reads as null.
/// </summary>
public sealed class RefreshTokenReader(IOptions<JwtOptions> options, ILogger<RefreshTokenReader> logger)
{
    public const int MaxJtiLength = 64;
    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    private readonly TokenValidationParameters _parameters = BuildParameters(options.Value);

    public RefreshTokenClaims? Read(string token)
    {
        var validated = Validate(token);
        if (validated is null) return null;
        var (principal, jwt) = validated.Value;

        if (RawClaim(jwt, TokenClaims.TokenUse) != TokenClaims.Refresh)
        {
            logger.LogInformation("Refresh rejected: token is not a refresh token");
            return null;
        }

        var jti = jwt.Id;
        if (string.IsNullOrEmpty(jti) || jti.Length > MaxJtiLength) return null;
        if (!int.TryParse(RawClaim(jwt, TokenClaims.TokenVersion), out var tokenVersion)) return null;
        if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return null;

        return new RefreshTokenClaims(userId, tokenVersion, jti, jwt.ValidTo);
    }

    /// <summary>
    /// Null for any token the handler rejects. The handler reports every kind of invalid input
    /// (malformed, bad signature, expired, ...) by throwing, so all of them are logged and mapped to null here.
    /// </summary>
    private (ClaimsPrincipal Principal, JwtSecurityToken Jwt)? Validate(string token)
    {
        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, _parameters, out var validatedToken);
            return validatedToken is JwtSecurityToken jwt ? (principal, jwt) : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Refresh token validation failed");
            return null;
        }
    }

    /// <summary>Reads a claim from the raw payload, which is not subject to inbound claim-type mapping.</summary>
    private static string? RawClaim(JwtSecurityToken jwt, string type) =>
        jwt.Claims.FirstOrDefault(c => c.Type == type)?.Value;

    private static TokenValidationParameters BuildParameters(JwtOptions options) => new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = options.Issuer,
        ValidAudience = options.Audience,
        IssuerSigningKey = JwtSigningKey.From(options),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateLifetime = true,
        ClockSkew = ClockSkew
    };
}
