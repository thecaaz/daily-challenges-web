using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace DailyChallenges.Services
{
    /// <summary>
    /// Refuses to start outside Development unless Jwt:Key is a strong secret that was never committed.
    /// Registered with ValidateOnStart, and Program.cs also resolves the options right after Build()
    /// so a bad key fails before the database is touched.
    /// </summary>
    public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
    {
        public const int MinKeyLength = 32;

        // SHA-256 fingerprints of keys that were once committed to the repository and must never be used.
        private static readonly HashSet<string> LeakedKeyFingerprints = new(StringComparer.OrdinalIgnoreCase)
        {
            "9fe69346d8134250df9b6c09daf77b6a88cc32310bf1cb8cb1431958005d52c9"
        };

        private readonly IHostEnvironment _env;

        public JwtOptionsValidator(IHostEnvironment env) => _env = env;

        public ValidateOptionsResult Validate(string? name, JwtOptions options)
        {
            if (_env.IsDevelopment()) return ValidateOptionsResult.Success;

            var key = options.Key ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key) || key.Contains("CHANGE_THIS") || key.Length < MinKeyLength)
            {
                return ValidateOptionsResult.Fail(
                    $"Jwt:Key must be configured to a strong secret (>={MinKeyLength} chars) in non-development environments. Set the Jwt__Key environment variable.");
            }

            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
            if (LeakedKeyFingerprints.Contains(fingerprint))
            {
                return ValidateOptionsResult.Fail(
                    "Jwt:Key is a known leaked key. Generate a new secret and set it via the Jwt__Key environment variable.");
            }

            return ValidateOptionsResult.Success;
        }
    }
}
