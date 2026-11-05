using DailyChallenges.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace DailyChallenges.IntegrationTests;

/// <summary>The app must refuse to start outside Development with a weak or placeholder Jwt:Key.</summary>
public class JwtKeyGuardTests
{
    [Theory]
    [InlineData("CHANGE_THIS")]
    [InlineData("short-key")]
    public void Startup_fails_with_weak_key_outside_development(string key)
    {
        using var root = new ApiFactory();
        using var factory = root.WithWebHostBuilder(b => b.UseSetting("Jwt:Key", key));

        var ex = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(ex);
        Assert.Contains(Chain(ex!), e => e is OptionsValidationException && e.Message.Contains("Jwt:Key"));
    }

    [Fact]
    public void Startup_allows_placeholder_key_in_development()
    {
        using var root = new ApiFactory();
        using var factory = root.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("Jwt:Key", "CHANGE_THIS");
        });

        using var client = factory.CreateClient();
    }

    [Theory]
    [InlineData("Xp:LevelBase", "0")]
    [InlineData("Xp:LevelExponent", "-1")]
    public void Startup_fails_with_an_invalid_level_curve(string key, string value)
    {
        using var root = new ApiFactory();
        using var factory = root.WithWebHostBuilder(b => b.UseSetting(key, value));

        var ex = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(ex);
        Assert.Contains(Chain(ex!), e => e is OptionsValidationException && e.Message.Contains(key));
    }

    private static IEnumerable<Exception> Chain(Exception ex)
    {
        for (Exception? e = ex; e != null; e = e.InnerException) yield return e;
    }
}
