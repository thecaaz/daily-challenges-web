using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace DailyChallenges.Services;

/// <summary>
/// Signs screenshot URLs so &lt;img&gt; requests (which carry no Bearer token) can load
/// current-day screenshots the caller is allowed to see. HMAC-SHA256 over "{id}|{exp}" with a
/// key derived (HKDF) from Jwt:Key, so it never equals the JWT signing key itself.
/// </summary>
public sealed class ScreenshotUrlSigner(IOptions<JwtOptions> jwt, TimeProvider time) : IScreenshotUrlSigner
{
    /// <summary>Signatures stay valid at least this long.</summary>
    public static readonly TimeSpan Validity = TimeSpan.FromHours(1);

    /// <summary>
    /// exp is rounded down to this bucket, so the URL (and the browser cache entry) stays the same
    /// for a while. Effective validity is therefore between Validity - Bucket and Validity.
    /// </summary>
    private static readonly TimeSpan Bucket = TimeSpan.FromMinutes(15);

    private const int KeyLengthBytes = 32;
    private static readonly byte[] KeyInfo = Encoding.UTF8.GetBytes("daily-challenges:screenshot-url:v1");

    private readonly byte[] _key = DeriveKey(jwt.Value.Key);

    public string UnsignedUrl(int submissionId) => $"/api/submissions/{submissionId}/screenshot";

    public string SignedUrl(int submissionId)
    {
        var now = UnixNow();
        var bucketStart = now - now % (long)Bucket.TotalSeconds;
        var exp = bucketStart + (long)Validity.TotalSeconds;
        return $"{UnsignedUrl(submissionId)}?exp={exp.ToString(CultureInfo.InvariantCulture)}&sig={ComputeSignature(submissionId, exp)}";
    }

    public string Url(int submissionId, bool sign) => sign ? SignedUrl(submissionId) : UnsignedUrl(submissionId);

    public string ComputeSignature(int submissionId, long exp) =>
        Convert.ToHexString(Hash(submissionId, exp)).ToLowerInvariant();

    public bool IsValid(int submissionId, long? exp, string? sig)
    {
        if (!exp.HasValue || string.IsNullOrEmpty(sig)) return false;
        if (exp.Value <= UnixNow()) return false;

        return TryDecodeHex(sig, out var provided)
            && CryptographicOperations.FixedTimeEquals(provided, Hash(submissionId, exp.Value));
    }

    private long UnixNow() => time.GetUtcNow().ToUnixTimeSeconds();

    /// <summary>Accepts upper- or lowercase hex; false for odd lengths or non-hex characters.</summary>
    private static bool TryDecodeHex(string hex, out byte[] bytes)
    {
        bytes = new byte[hex.Length / 2];
        var status = Convert.FromHexString(hex, bytes, out var charsConsumed, out var bytesWritten);
        return status == OperationStatus.Done && charsConsumed == hex.Length && bytesWritten == bytes.Length;
    }

    private static byte[] DeriveKey(string? jwtKey) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(jwtKey ?? string.Empty), KeyLengthBytes, salt: null, info: KeyInfo);

    private byte[] Hash(int submissionId, long exp) =>
        HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes($"{submissionId.ToString(CultureInfo.InvariantCulture)}|{exp.ToString(CultureInfo.InvariantCulture)}"));
}
