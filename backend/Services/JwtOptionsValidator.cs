using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace DailyChallenges.Services;

/// <summary>
/// Refuses to start outside Development unless Jwt:Key is a strong secret that was never committed.
/// Registered with ValidateOnStart, and Program.cs also resolves the options right after Build()
/// so a bad key fails before the database is touched.
/// </summary>
public sealed class JwtOptionsValidator(IHostEnvironment env) : IValidateOptions<JwtOptions>
{
    public const int MinKeyLength = 32;

    /// <summary>Marks the placeholder keys shipped in appsettings*.json.</summary>
    private const string PlaceholderKeyMarker = "CHANGE_THIS";

    /// <summary>SHA-256 fingerprints of keys that were once committed to the repository and must never be used.</summary>
    private static readonly HashSet<string> LeakedKeyFingerprints = new(StringComparer.OrdinalIgnoreCase)
    {
        "9fe69346d8134250df9b6c09daf77b6a88cc32310bf1cb8cb1431958005d52c9"
    };

    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        if (env.IsDevelopment()) return ValidateOptionsResult.Success;

        var key = options.Key ?? string.Empty;
        if (IsWeak(key))
        {
            return ValidateOptionsResult.Fail(
                $"Jwt:Key must be configured to a strong secret (>={MinKeyLength} chars) in non-development environments. Set the Jwt__Key environment variable.");
        }

        if (IsLeaked(key))
        {
            return ValidateOptionsResult.Fail(
                "Jwt:Key is a known leaked key. Generate a new secret and set it via the Jwt__Key environment variable.");
        }

        return ValidateOptionsResult.Success;
    }

    private static bool IsWeak(string key) =>
        string.IsNullOrWhiteSpace(key) || key.Contains(PlaceholderKeyMarker) || key.Length < MinKeyLength;

    private static bool IsLeaked(string key) =>
        LeakedKeyFingerprints.Contains(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))));
}
