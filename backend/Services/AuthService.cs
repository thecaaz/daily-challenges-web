using DailyChallenges.Data;
using DailyChallenges.Exceptions;
using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DailyChallenges.Services
{
    /// <summary>Custom JWT claim names and values.</summary>
    public static class TokenClaims
    {
        /// <summary>"access" for Bearer tokens, "refresh" for the refresh cookie. Bearer auth accepts only "access".</summary>
        public const string TokenUse = "token_use";
        /// <summary>The user's <see cref="User.TokenVersion"/> when the token was issued.</summary>
        public const string TokenVersion = "tv";

        public const string Access = "access";
        public const string Refresh = "refresh";
    }

    public class AuthService : IAuthService
    {
        public const string RefreshCookieName = "refresh_token";

        /// <summary>
        /// A rotated refresh token replayed within this window is only rejected (concurrent tabs, retried
        /// requests); replayed later it is treated as stolen and every session of the user is ended.
        /// </summary>
        public static readonly TimeSpan RefreshReuseGracePeriod = TimeSpan.FromSeconds(30);

        // Verified against when the username doesn't exist, so a failed login takes the same time either way.
        private static readonly Lazy<string> DummyPasswordHash = new(() => BCrypt.Net.BCrypt.HashPassword("dummy-password-for-timing"));

        private readonly IUserRepository _userRepo;
        private readonly AppDbContext _db;
        private readonly JwtOptions _jwtOptions;
        private readonly LevelCalculator _levelCalc;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<AuthService> _logger;
        private readonly TimeProvider _time;

        public AuthService(IUserRepository userRepo, AppDbContext db, IOptions<JwtOptions> jwtOptions, LevelCalculator levelCalc, IWebHostEnvironment env, ILogger<AuthService> logger, TimeProvider? time = null)
        {
            _time = time ?? TimeProvider.System;
            _userRepo = userRepo;
            _db = db;
            _jwtOptions = jwtOptions?.Value ?? new JwtOptions();
            _levelCalc = levelCalc;
            _env = env;
            _logger = logger;
        }

        private int AccessMinutes => _jwtOptions.AccessTokenMinutes <= 0 ? 30 : _jwtOptions.AccessTokenMinutes;

        public async Task<UserDto> RegisterAsync(string username, string password)
        {
            var name = CredentialRules.ValidateUsername(username);
            CredentialRules.ValidatePassword(password);

            // Hash outside the transaction so the write lock isn't held for the BCrypt work.
            var hash = BCrypt.Net.BCrypt.HashPassword(password);

            // BEGIN IMMEDIATE (SQLite): the existence check, the first-user-is-admin check and the insert are
            // serialized with other registrations, so two concurrent first registrations can't both become admin.
            // The NOCASE unique index on Username is the last line of defence.
            User user;
            await using (var tx = await _db.Database.BeginTransactionAsync())
            {
                try
                {
                    if (await _userRepo.ExistsWithUsernameAsync(name))
                        throw new ConflictException("Username already taken");

                    var isFirstUser = await _userRepo.IsEmptyAsync();
                    user = new User { Username = name, PasswordHash = hash, IsAdmin = isFirstUser };
                    await _userRepo.CreateAsync(user);
                    await tx.CommitAsync();
                }
                catch (DbUpdateException ex) when (ex.IsUniqueViolation())
                {
                    await tx.RollbackAsync();
                    _db.ChangeTracker.Clear();
                    throw new ConflictException("Username already taken");
                }
                catch
                {
                    await tx.RollbackAsync();
                    _db.ChangeTracker.Clear();
                    throw;
                }
            }

            return DtoMapper.ToDto(user, _levelCalc, _time.GetUtcNow().UtcDateTime);
        }

        public async Task<AuthResultDto> LoginAsync(string username, string password, HttpResponse response)
        {
            // Username lookups are case-insensitive (NOCASE column).
            var user = await _userRepo.GetByUsernameAsync(CredentialRules.NormalizeUsername(username));
            if (user == null)
            {
                // Same BCrypt cost as a real check, so the response time doesn't reveal unknown usernames.
                BCrypt.Net.BCrypt.Verify(password ?? string.Empty, DummyPasswordHash.Value);
                throw new AuthenticationFailedException("Invalid credentials");
            }

            if (!BCrypt.Net.BCrypt.Verify(password ?? string.Empty, user.PasswordHash))
                throw new AuthenticationFailedException("Invalid credentials");

            return IssueTokens(user, response);
        }

        public async Task LogoutAsync(HttpRequest request, HttpResponse response)
        {
            // Revoke this device's refresh token (other sessions keep working), then drop the cookie.
            if (request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken))
            {
                var token = ValidateRefreshToken(refreshToken);
                if (token != null)
                    await TryRevokeAsync(token.Value.Jti, token.Value.ExpiresAt, RevokedRefreshTokenReason.Logout);
            }

            response.Cookies.Delete(RefreshCookieName, BuildCookieOptions(expires: null));
        }

        public async Task<UserDto?> GetCurrentUserAsync(ClaimsPrincipal user)
        {
            if (!user.Identity?.IsAuthenticated ?? true) return null;
            var idClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(idClaim)) return null;
            if (!int.TryParse(idClaim, out var id)) return null;
            var u = await _userRepo.GetByIdAsync(id);
            if (u == null) return null;
            return DtoMapper.ToDto(u, _levelCalc, _time.GetUtcNow().UtcDateTime);
        }

        public async Task<AuthResultDto?> RefreshAsync(HttpRequest request, HttpResponse response)
        {
            if (!request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken)) return null;

            // Rejects access tokens and pre-upgrade refresh tokens (no token_use / jti / tv claims).
            var token = ValidateRefreshToken(refreshToken);
            if (token == null) return null;
            var (userId, tokenVersion, jti, expiresAt) = token.Value;

            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null) return null;

            // A password reset (or delete) bumps TokenVersion, which ends every older session.
            if (user.TokenVersion != tokenVersion)
            {
                _logger.LogInformation("Refresh rejected for user {UserId}: token version {TokenVersion} is outdated", userId, tokenVersion);
                return null;
            }

            // Rotation: the presented token is consumed. The insert is atomic, so of two concurrent
            // refreshes with the same token only one wins; a replayed (stolen or old) token is rejected.
            if (!await TryRevokeAsync(jti, expiresAt, RevokedRefreshTokenReason.Rotated))
            {
                await HandleRefreshTokenReuseAsync(userId, tokenVersion, jti);
                return null;
            }

            return IssueTokens(user, response);
        }

        /// <summary>Issues an access token and a fresh refresh cookie for the user.</summary>
        private AuthResultDto IssueTokens(User user, HttpResponse response)
        {
            var accessToken = GenerateJwtToken(user, TimeSpan.FromMinutes(AccessMinutes), TokenClaims.Access, jti: null);
            var refreshToken = GenerateJwtToken(user, TimeSpan.FromDays(_jwtOptions.RefreshExpiresDays), TokenClaims.Refresh, jti: Guid.NewGuid().ToString("N"));

            // Do not set an `access_token` cookie; use refresh cookie + Authorization header only.
            response.Cookies.Append(RefreshCookieName, refreshToken,
                BuildCookieOptions(DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshExpiresDays)));

            var dto = DtoMapper.ToDto(user, _levelCalc, _time.GetUtcNow().UtcDateTime);
            return new AuthResultDto(dto, accessToken, AccessMinutes * 60);
        }

        private CookieOptions BuildCookieOptions(DateTimeOffset? expires) => new()
        {
            HttpOnly = true,
            // Force secure cookie except in Development only.
            Secure = !_env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Expires = expires
        };

        /// <summary>Records the refresh token as used. Returns false when it already was (reuse).</summary>
        private async Task<bool> TryRevokeAsync(string jti, DateTime expiresAt, RevokedRefreshTokenReason reason)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var inserted = await _db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT OR IGNORE INTO RevokedRefreshTokens (Jti, ExpiresAt, RevokedAt, Reason) VALUES ({jti}, {expiresAt}, {now}, {(int)reason})");
            return inserted == 1;
        }

        /// <summary>
        /// A refresh token was presented again after it was revoked. Shortly after its rotation that is normal
        /// (two tabs refreshing at once, or a lost response that the client retries), so it is just rejected.
        /// Long after its rotation, the old token can only come from a copy: someone else holds (or held) it.
        /// Since we can't tell which party is the legitimate one, every session of the user is ended by bumping
        /// TokenVersion. Logout-revoked (and legacy, undated) tokens are only rejected.
        /// </summary>
        private async Task HandleRefreshTokenReuseAsync(int userId, int tokenVersion, string jti)
        {
            var revoked = await _db.RevokedRefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.Jti == jti);
            var now = _time.GetUtcNow().UtcDateTime;

            if (revoked is { Reason: RevokedRefreshTokenReason.Rotated, RevokedAt: { } revokedAt }
                && now - revokedAt > RefreshReuseGracePeriod)
            {
                // Conditional on the presented version, so concurrent detections bump it only once.
                var bumped = await _db.Users
                    .Where(u => u.Id == userId && u.TokenVersion == tokenVersion)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.TokenVersion, u => u.TokenVersion + 1));
                _logger.LogWarning(
                    "Refresh token {Jti} of user {UserId} was replayed {Seconds:F0}s after its rotation; treating it as stolen and ending all sessions (bumped: {Bumped})",
                    jti, userId, (now - revokedAt).TotalSeconds, bumped == 1);
                return;
            }

            _logger.LogWarning("Refresh rejected for user {UserId}: refresh token {Jti} was already used or revoked", userId, jti);
        }

        private string GenerateJwtToken(User user, TimeSpan expires, string tokenUse, string? jti)
        {
            var issuer = _jwtOptions.Issuer;
            var audience = _jwtOptions.Audience;
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key ?? string.Empty));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(TokenClaims.TokenUse, tokenUse),
                new Claim(TokenClaims.TokenVersion, user.TokenVersion.ToString(), ClaimValueTypes.Integer32)
            };

            if (jti != null)
                claims.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));

            if (user.IsAdmin)
            {
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            }

            var token = new JwtSecurityToken(issuer, audience, claims, expires: DateTime.UtcNow.Add(expires), signingCredentials: creds);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// Validates signature, issuer, audience and lifetime, and requires token_use=refresh plus jti and tv.
        /// Returns null for anything else (including access tokens).
        /// </summary>
        private (int UserId, int TokenVersion, string Jti, DateTime ExpiresAt)? ValidateRefreshToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_jwtOptions.Key ?? string.Empty);
                var parameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = _jwtOptions.Issuer,
                    ValidAudience = _jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                var principal = tokenHandler.ValidateToken(token, parameters, out var validatedToken);
                if (validatedToken is not JwtSecurityToken jwt) return null;

                // Read the custom claims from the raw payload (not subject to inbound claim-type mapping).
                string? Raw(string type) => jwt.Claims.FirstOrDefault(c => c.Type == type)?.Value;

                if (Raw(TokenClaims.TokenUse) != TokenClaims.Refresh)
                {
                    _logger.LogInformation("Refresh rejected: token is not a refresh token");
                    return null;
                }

                var jti = jwt.Id;
                if (string.IsNullOrEmpty(jti) || jti.Length > 64) return null;
                if (!int.TryParse(Raw(TokenClaims.TokenVersion), out var tv)) return null;
                if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return null;

                return (userId, tv, jti, jwt.ValidTo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Refresh token validation failed");
                return null;
            }
        }
    }
}
