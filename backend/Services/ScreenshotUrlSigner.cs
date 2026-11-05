using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace DailyChallenges.Services
{
    public interface IScreenshotUrlSigner
    {
        /// <summary>Plain URL (no signature). Enough for past scoring days.</summary>
        string UnsignedUrl(int submissionId);

        /// <summary>URL with <c>?exp=&lt;unix&gt;&amp;sig=&lt;hex&gt;</c>, valid for about an hour.</summary>
        string SignedUrl(int submissionId);

        /// <summary>Signs only when <paramref name="sign"/> is true (current-day submissions).</summary>
        string Url(int submissionId, bool sign);

        /// <summary>The signature for <c>{id}|{exp}</c> as lowercase hex.</summary>
        string ComputeSignature(int submissionId, long exp);

        /// <summary>True when <paramref name="sig"/> signs <c>{id}|{exp}</c> and <paramref name="exp"/> is still in the future.</summary>
        bool IsValid(int submissionId, long? exp, string? sig);
    }

    /// <summary>
    /// Signs screenshot URLs so &lt;img&gt; requests (which carry no Bearer token) can load
    /// current-day screenshots the caller is allowed to see. HMAC-SHA256 over "{id}|{exp}" with a
    /// key derived (HKDF) from Jwt:Key, so it never equals the JWT signing key itself.
    /// </summary>
    public sealed class ScreenshotUrlSigner : IScreenshotUrlSigner
    {
        /// <summary>Signatures stay valid at least this long.</summary>
        public static readonly TimeSpan Validity = TimeSpan.FromHours(1);

        /// <summary>
        /// exp is rounded down to this bucket, so the URL (and the browser cache entry) stays the same
        /// for a while. Effective validity is therefore between Validity - Bucket and Validity.
        /// </summary>
        private const long BucketSeconds = 15 * 60;

        private static readonly byte[] KeyInfo = Encoding.UTF8.GetBytes("daily-challenges:screenshot-url:v1");

        private readonly byte[] _key;
        private readonly TimeProvider _time;

        public ScreenshotUrlSigner(IOptions<JwtOptions> jwt, TimeProvider time)
        {
            var ikm = Encoding.UTF8.GetBytes(jwt.Value.Key ?? string.Empty);
            _key = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 32, salt: null, info: KeyInfo);
            _time = time;
        }

        public string UnsignedUrl(int submissionId) => $"/api/submissions/{submissionId}/screenshot";

        public string SignedUrl(int submissionId)
        {
            var now = _time.GetUtcNow().ToUnixTimeSeconds();
            var exp = now - (now % BucketSeconds) + (long)Validity.TotalSeconds;
            return $"{UnsignedUrl(submissionId)}?exp={exp.ToString(CultureInfo.InvariantCulture)}&sig={ComputeSignature(submissionId, exp)}";
        }

        public string Url(int submissionId, bool sign) => sign ? SignedUrl(submissionId) : UnsignedUrl(submissionId);

        public string ComputeSignature(int submissionId, long exp) =>
            Convert.ToHexString(Hash(submissionId, exp)).ToLowerInvariant();

        public bool IsValid(int submissionId, long? exp, string? sig)
        {
            if (!exp.HasValue || string.IsNullOrEmpty(sig)) return false;
            if (exp.Value <= _time.GetUtcNow().ToUnixTimeSeconds()) return false;

            byte[] provided;
            try { provided = Convert.FromHexString(sig); }
            catch (FormatException) { return false; }

            return CryptographicOperations.FixedTimeEquals(provided, Hash(submissionId, exp.Value));
        }

        private byte[] Hash(int submissionId, long exp) =>
            HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes($"{submissionId.ToString(CultureInfo.InvariantCulture)}|{exp.ToString(CultureInfo.InvariantCulture)}"));
    }
}
