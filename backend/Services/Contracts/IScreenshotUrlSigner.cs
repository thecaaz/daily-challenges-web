namespace DailyChallenges.Services;

public interface IScreenshotUrlSigner
{
    /// <summary>Plain URL (no signature). Enough for past scoring days.</summary>
    string UnsignedUrl(int submissionId);

    /// <summary>URL with <c>?exp=&lt;unix&gt;&amp;sig=&lt;hex&gt;</c>, valid for about an hour.</summary>
    string SignedUrl(int submissionId);

    /// <summary>The signature for <c>{id}|{exp}</c> as lowercase hex.</summary>
    string ComputeSignature(int submissionId, long exp);

    /// <summary>True when <paramref name="sig"/> signs <c>{id}|{exp}</c> and <paramref name="exp"/> is still in the future.</summary>
    bool IsValid(int submissionId, long? exp, string? sig);
}
