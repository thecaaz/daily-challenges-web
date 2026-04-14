using DailyChallenges.Data;
using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;

namespace DailyChallenges.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _db;
        private readonly JwtOptions _jwtOptions;
        private readonly LevelCalculator _levelCalc;
        private readonly IWebHostEnvironment _env;

        public AuthService(AppDbContext db, IOptions<JwtOptions> jwtOptions, LevelCalculator levelCalc, IWebHostEnvironment env)
        {
            _db = db;
            _jwtOptions = jwtOptions?.Value ?? new JwtOptions();
            _levelCalc = levelCalc;
            _env = env;
        }

        public async Task<UserDto> RegisterAsync(string username, string password)
        {
            if (await _db.Users.AnyAsync(u => u.Username == username))
                throw new InvalidOperationException("Username already taken");

            var isFirstUser = !await _db.Users.AnyAsync();
            var hash = BCrypt.Net.BCrypt.HashPassword(password);
            var user = new User { Username = username, PasswordHash = hash, IsAdmin = isFirstUser };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return DtoMapper.ToDto(user, _levelCalc);
        }

        public async Task<AuthResultDto> LoginAsync(string username, string password, HttpResponse response)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) throw new UnauthorizedAccessException("Invalid credentials");

            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                throw new UnauthorizedAccessException("Invalid credentials");

            // Access token expiry (minutes)
            var accessMinutes = _jwtOptions.AccessTokenMinutes <= 0 ? 30 : _jwtOptions.AccessTokenMinutes;

            var accessToken = GenerateJwtToken(user, TimeSpan.FromMinutes(accessMinutes));
            var refreshToken = GenerateJwtToken(user, TimeSpan.FromDays(_jwtOptions.RefreshExpiresDays));

            // Force secure cookie except in Development only.
            var cookieSecure = !_env.IsDevelopment();

            // Set refresh token as HttpOnly cookie (used to obtain new access tokens).
            response.Cookies.Append("refresh_token", refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = cookieSecure,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshExpiresDays)
            });

            // Do not set an `access_token` cookie; use refresh cookie + Authorization header only.

            var dto = DtoMapper.ToDto(user, _levelCalc);
            return new AuthResultDto(dto, accessToken, accessMinutes * 60);
        }

        public Task LogoutAsync(HttpResponse response)
        {
            response.Cookies.Delete("refresh_token");
            return Task.CompletedTask;
        }

        public async Task<UserDto?> GetCurrentUserAsync(ClaimsPrincipal user)
        {
            if (!user.Identity?.IsAuthenticated ?? true) return null;
            var idClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(idClaim)) return null;
            if (!int.TryParse(idClaim, out var id)) return null;
            var u = await _db.Users.FindAsync(id);
            if (u == null) return null;
            return DtoMapper.ToDto(u, _levelCalc);
        }
        public async Task<AuthResultDto?> RefreshAsync(HttpRequest request, HttpResponse response)
        {
            if (!request.Cookies.ContainsKey("refresh_token")) return null;
            var refreshToken = request.Cookies["refresh_token"];

            var principal = ValidateToken(refreshToken, validateLifetime: true);
            if (principal == null) return null;

            var idClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(idClaim) || !int.TryParse(idClaim, out var userId)) return null;

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return null;

            var accessMinutes = _jwtOptions.AccessTokenMinutes <= 0 ? 30 : _jwtOptions.AccessTokenMinutes;
            var accessToken = GenerateJwtToken(user, TimeSpan.FromMinutes(accessMinutes));

            // Optionally rotate refresh token: re-issue new refresh cookie with fresh expiry
            var newRefreshToken = GenerateJwtToken(user, TimeSpan.FromDays(_jwtOptions.RefreshExpiresDays));
            var cookieSecure = !_env.IsDevelopment();
            response.Cookies.Append("refresh_token", newRefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = cookieSecure,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshExpiresDays)
            });

            var dto = DtoMapper.ToDto(user, _levelCalc);
            return new AuthResultDto(dto, accessToken, accessMinutes * 60);
        }

        private string GenerateJwtToken(User user, TimeSpan expires)
        {
            var issuer = _jwtOptions.Issuer;
            var audience = _jwtOptions.Audience;
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key ?? string.Empty));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username)
            };

            if (user.IsAdmin)
            {
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            }

            var token = new JwtSecurityToken(issuer, audience, claims, expires: DateTime.UtcNow.Add(expires), signingCredentials: creds);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private ClaimsPrincipal? ValidateToken(string token, bool validateLifetime)
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
                    ValidateLifetime = validateLifetime,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                var principal = tokenHandler.ValidateToken(token, parameters, out var validatedToken);
                return principal;
            }
            catch
            {
                return null;
            }
        }
    }
}
