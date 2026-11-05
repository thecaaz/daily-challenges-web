using DailyChallenges.Services;
using Microsoft.Extensions.Hosting;
using Moq;

namespace DailyChallenges.Tests;

public class JwtOptionsValidatorTests
{
    private static JwtOptionsValidator Validator(string environment)
    {
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(environment);
        return new JwtOptionsValidator(env.Object);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("CHANGE_THIS")]
    [InlineData("CHANGE_THIS-but-long-enough-to-pass-the-length-check")]
    [InlineData("too-short-0123456789")]
    public void Rejects_weak_keys_outside_development(string key)
    {
        var result = Validator("Production").Validate(null, new JwtOptions { Key = key });

        Assert.True(result.Failed);
    }

    [Fact]
    public void Accepts_strong_key_outside_development()
    {
        var result = Validator("Production").Validate(null, new JwtOptions { Key = new string('k', 32) });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Skips_checks_in_development()
    {
        var result = Validator("Development").Validate(null, new JwtOptions { Key = "CHANGE_THIS" });

        Assert.True(result.Succeeded);
    }
}
