using System.Text.Json;
using DailyChallenges.Services.Contracts;

namespace DailyChallenges.Services;

/// <summary>Reads <c>release-info.json</c>, written by the release pipeline into the content root.</summary>
public sealed class ReleaseInfoReader(IWebHostEnvironment env, ILogger<ReleaseInfoReader> logger)
{
    private const string FileName = "release-info.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Null when the file is missing or unreadable (logged as a warning).</summary>
    public async Task<ReleaseInfo?> ReadAsync()
    {
        var path = Path.Combine(env.ContentRootPath, FileName);
        if (!File.Exists(path)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<ReleaseInfo>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning(ex, "Failed to read release-info.json at {Path}", path);
            return null;
        }
    }
}
