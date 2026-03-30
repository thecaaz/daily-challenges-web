using DailyChallenges.Data;
using DailyChallenges.DTOs;
using DailyChallenges.Mapping;
using DailyChallenges.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DailyChallenges.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;
        private readonly LevelCalculator _levelCalc;

        public AuthService(AppDbContext db, IConfiguration config, IWebHostEnvironment env, LevelCalculator levelCalc)
        {
            _db = db;
            _config = config;
            _env = env;
            _levelCalc = levelCalc;
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

        public async Task<UserDto> LoginAsync(string username, string password, HttpResponse response)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) throw new UnauthorizedAccessException("Invalid credentials");

            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                throw new UnauthorizedAccessException("Invalid credentials");

            var token = GenerateJwtToken(user);

            var expiresDays = int.Parse(_config["Jwt:ExpiresDays"] ?? "7");

            // Per user request: force insecure cookie (not recommended for production).
            // This unconditionally disables the Secure flag so the cookie can be set
            // over plain HTTP.
            var cookieSecure = false;

            response.Cookies.Append("access_token", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = cookieSecure,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(expiresDays)
            });

            return DtoMapper.ToDto(user, _levelCalc);
        }

        public Task LogoutAsync(HttpResponse response)
        {
            response.Cookies.Delete("access_token");
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

        private string GenerateJwtToken(User user)
        {
            var issuer = _config["Jwt:Issuer"];
            var audience = _config["Jwt:Audience"];
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"] ?? string.Empty));
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

            var token = new JwtSecurityToken(issuer, audience, claims, expires: DateTime.UtcNow.AddDays(double.Parse(_config["Jwt:ExpiresDays"] ?? "7")), signingCredentials: creds);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
