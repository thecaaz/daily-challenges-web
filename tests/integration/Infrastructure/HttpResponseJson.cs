using System.Text.Json;

namespace DailyChallenges.IntegrationTests.Infrastructure;

/// <summary>Reads API response bodies as detached <see cref="JsonElement"/>s. Import with <c>using static</c>.</summary>
public static class HttpResponseJson
{
    /// <summary>Parses the body without looking at the status code (for error bodies and status-agnostic contracts).</summary>
    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    /// <summary>Asserts a 2xx status (showing the body on failure), then parses the body.</summary>
    public static async Task<JsonElement> ReadSuccessJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }
}
