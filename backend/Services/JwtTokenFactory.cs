using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DailyChallenges.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DailyChallenges.Services;

/// <summary>
/// Signs access and refresh tokens (HS256) for a user.
/// Lifetimes are measured on <see cref="TimeProvider.System"/>, not the injected clock, on purpose:
/// Microsoft.IdentityModel checks exp against the system clock and has no TimeProvider hook, so
/// tokens issued on any other clock would already be expired (or valid for too long) when checked.
/// </summary>
public sealed class JwtTokenFactory(IOptions<JwtOptions> options)
{
    public const int DefaultAccessTokenMinutes = 30;
    private const string JtiFormat = "N";

    private static readonly TimeProvider TokenClock = TimeProvider.System;

    private readonly JwtOptions _options = options.Value;

    /// <summary>Jwt:AccessTokenMinutes, or 30 minutes when it is not positive.</summary>
    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(
        _options.AccessTokenMinutes <= 0 ? DefaultAccessTokenMinutes : _options.AccessTokenMinutes);

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_options.RefreshExpiresDays);

    /// <summary>A Bearer token: user id, name, token_use=access, tv and the Admin role when applicable. No jti.</summary>
    public IssuedToken CreateAccessToken(User user) =>
        Sign(SessionClaims(user, TokenClaims.Access).Concat(RoleClaims(user)), AccessTokenLifetime);

    /// <summary>A refresh token: like the access token, but token_use=refresh plus a fresh random jti.</summary>
    public IssuedToken CreateRefreshToken(User user) =>
        Sign(SessionClaims(user, TokenClaims.Refresh).Append(NewJtiClaim()).Concat(RoleClaims(user)), RefreshTokenLifetime);

    private static IEnumerable<Claim> SessionClaims(User user, string tokenUse) =>
    [
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.Username),
        new Claim(TokenClaims.TokenUse, tokenUse),
        new Claim(TokenClaims.TokenVersion, user.TokenVersion.ToString(), ClaimValueTypes.Integer32)
    ];

    private static Claim NewJtiClaim() => new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString(JtiFormat));

    private static IEnumerable<Claim> RoleClaims(User user) =>
        user.IsAdmin ? [new Claim(ClaimTypes.Role, AuthRoles.Admin)] : [];

    private IssuedToken Sign(IEnumerable<Claim> claims, TimeSpan lifetime)
    {
        var expiresAt = TokenClock.GetUtcNow().Add(lifetime);
        var credentials = new SigningCredentials(JwtSigningKey.From(_options), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_options.Issuer, _options.Audience, claims,
            expires: expiresAt.UtcDateTime, signingCredentials: credentials);
        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
