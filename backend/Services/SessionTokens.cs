namespace DailyChallenges.Services;

/// <summary>The claims of a refresh token that passed <see cref="RefreshTokenReader"/> validation.</summary>
public sealed record RefreshTokenClaims(int UserId, int TokenVersion, string Jti, DateTime ExpiresAt);

/// <summary>A signed JWT and the instant it stops being valid.</summary>
public sealed record IssuedToken(string Value, DateTimeOffset ExpiresAt);
