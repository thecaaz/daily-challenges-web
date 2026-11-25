using DailyChallenges.Services;
using DailyChallenges.TestUtilities;
using Microsoft.Extensions.Options;

namespace DailyChallenges.Tests;

public class ScreenshotUrlSignerTests
{
    private static readonly DateTime Now = new(2026, 6, 15, 10, 7, 30, DateTimeKind.Utc);
    private static readonly long NowUnix = new DateTimeOffset(Now).ToUnixTimeSeconds();

    private static (ScreenshotUrlSigner Signer, FixedTimeProvider Clock) Create(string key = "unit-test-signing-key-0123456789abcdef")
    {
        var clock = new FixedTimeProvider(Now);
        return (new ScreenshotUrlSigner(Options.Create(new JwtOptions { Key = key }), clock), clock);
    }

    private static (long Exp, string Sig) ParseQuery(string url)
    {
        var query = url[(url.IndexOf('?') + 1)..].Split('&').Select(p => p.Split('=')).ToDictionary(p => p[0], p => p[1]);
        return (long.Parse(query["exp"]), query["sig"]);
    }

    [Fact]
    public void UnsignedUrl_is_the_plain_screenshot_route()
    {
        var (signer, _) = Create();

        Assert.Equal("/api/submissions/42/screenshot", signer.UnsignedUrl(42));
    }

    [Fact]
    public void SignedUrl_rounds_exp_down_to_15_minutes_and_adds_one_hour()
    {
        var (signer, _) = Create();
        var bucketStart = NowUnix - NowUnix % 900;

        var url = signer.SignedUrl(42);

        Assert.StartsWith("/api/submissions/42/screenshot?exp=", url);
        var (exp, sig) = ParseQuery(url);
        Assert.Equal(bucketStart + 3600, exp);
        Assert.Equal(signer.ComputeSignature(42, exp), sig);
        Assert.Matches("^[0-9a-f]{64}$", sig);
    }

    [Fact]
    public void Url_signs_only_when_asked()
    {
        var (signer, _) = Create();

        Assert.Equal(signer.UnsignedUrl(7), signer.Url(7, sign: false));
        Assert.Equal(signer.SignedUrl(7), signer.Url(7, sign: true));
    }

    [Fact]
    public void IsValid_accepts_a_fresh_signature_in_either_hex_case()
    {
        var (signer, _) = Create();
        var (exp, sig) = ParseQuery(signer.SignedUrl(42));

        Assert.True(signer.IsValid(42, exp, sig));
        Assert.True(signer.IsValid(42, exp, sig.ToUpperInvariant()));
    }

    [Fact]
    public void IsValid_rejects_other_ids_other_exp_and_other_keys()
    {
        var (signer, _) = Create();
        var (other, _) = Create("a-completely-different-signing-key-0123");
        var (exp, sig) = ParseQuery(signer.SignedUrl(42));

        Assert.False(signer.IsValid(43, exp, sig));
        Assert.False(signer.IsValid(42, exp + 1, sig));
        Assert.False(other.IsValid(42, exp, sig));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("zz")]
    [InlineData("abc")]
    [InlineData("ab")]
    public void IsValid_rejects_missing_malformed_or_truncated_signatures(string? sig)
    {
        var (signer, _) = Create();
        var (exp, _) = ParseQuery(signer.SignedUrl(42));

        Assert.False(signer.IsValid(42, exp, sig));
    }

    [Fact]
    public void IsValid_rejects_a_correct_signature_with_trailing_bytes()
    {
        var (signer, _) = Create();
        var (exp, sig) = ParseQuery(signer.SignedUrl(42));

        Assert.False(signer.IsValid(42, exp, sig + "00"));
    }

    [Fact]
    public void IsValid_rejects_missing_or_expired_exp()
    {
        var (signer, clock) = Create();
        var (exp, sig) = ParseQuery(signer.SignedUrl(42));

        Assert.False(signer.IsValid(42, null, sig));
        clock.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime);
        Assert.False(signer.IsValid(42, exp, sig));
    }
}
