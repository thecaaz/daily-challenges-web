using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using DailyChallenges.Repositories.Contracts;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DailyChallenges.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepo;
        private readonly JwtOptions _jwtOptions;
        private readonly LevelCalculator _levelCalc;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IUserRepository userRepo, IOptions<JwtOptions> jwtOptions, LevelCalculator levelCalc, IWebHostEnvironment env, ILogger<AuthService> logger)
        {
            _userRepo = userRepo;
            _jwtOptions = jwtOptions?.Value ?? new JwtOptions();
            _levelCalc = levelCalc;
            _env = env;
            _logger = logger;
        }

        public async Task<UserDto> RegisterAsync(string username, string password)
        {
            if (await _userRepo.ExistsWithUsernameAsync(username))
                throw new InvalidOperationException("Username already taken");

            var isFirstUser = await _userRepo.IsEmptyAsync();
            var hash = BCrypt.Net.BCrypt.HashPassword(password);
            var user = new User { Username = username, PasswordHash = hash, IsAdmin = isFirstUser };
            await _userRepo.CreateAsync(user);
            return DtoMapper.ToDto(user, _levelCalc);
        }

        public async Task<AuthResultDto> LoginAsync(string username, string password, HttpResponse response)
        {
            var user = await _userRepo.GetByUsernameAsync(username);
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
            var u = await _userRepo.GetByIdAsync(id);
            if (u == null) return null;
            return DtoMapper.ToDto(u, _levelCalc);
        }
        public async Task<AuthResultDto?> RefreshAsync(HttpRequest request, HttpResponse response)
        {
            if (!request.Cookies.TryGetValue("refresh_token", out var refreshToken)) return null;

            var principal = ValidateToken(refreshToken, validateLifetime: true);
            if (principal == null) return null;

            var idClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(idClaim) || !int.TryParse(idClaim, out var userId)) return null;

            var user = await _userRepo.GetByIdAsync(userId);
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token validation failed (validateLifetime={ValidateLifetime})", validateLifetime);
                return null;
            }
        }
    }
}
