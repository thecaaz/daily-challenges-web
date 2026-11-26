using DailyChallenges.Exceptions;

namespace DailyChallenges.Services;

/// <summary>
/// Accepts PNG, JPEG and WebP images up to 2 MB. The type is detected from the file's magic bytes;
/// the content type the client declared is ignored.
/// </summary>
public sealed class FileValidator : IFileValidator
{
    public const long MaxBytes = 2 * 1024 * 1024;
    private const int HeaderLength = 12;
    private const int WebpMarkerOffset = 8;

    private const string PngContentType = "image/png";
    private const string JpegContentType = "image/jpeg";
    private const string WebpContentType = "image/webp";

    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] JpegSignature = { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] RiffMarker = "RIFF"u8.ToArray();
    private static readonly byte[] WebpMarker = "WEBP"u8.ToArray();

    public string ValidateImage(IFormFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Length == 0) throw new ValidationException("file is empty");
        if (file.Length > MaxBytes) throw new ValidationException("file too large (max 2 MB)");

        return DetectContentType(ReadHeader(file))
            ?? throw new ValidationException("invalid file type (PNG, JPEG or WebP required)");
    }

    /// <summary>"image/png", "image/jpeg" or "image/webp" from the leading bytes; null for anything else.</summary>
    public static string? DetectContentType(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(PngSignature)) return PngContentType;
        if (header.StartsWith(JpegSignature)) return JpegContentType;
        if (IsWebp(header)) return WebpContentType;
        return null;
    }

    /// <summary>"RIFF" at offset 0 and "WEBP" at offset 8.</summary>
    private static bool IsWebp(ReadOnlySpan<byte> header) =>
        header.Length >= HeaderLength
        && header.StartsWith(RiffMarker)
        && header.Slice(WebpMarkerOffset, WebpMarker.Length).SequenceEqual(WebpMarker);

    /// <summary>Up to <see cref="HeaderLength"/> leading bytes (fewer when the file is shorter).</summary>
    private static ReadOnlySpan<byte> ReadHeader(IFormFile file)
    {
        var header = new byte[HeaderLength];
        using var stream = file.OpenReadStream();
        var read = stream.ReadAtLeast(header, HeaderLength, throwOnEndOfStream: false);
        return header.AsSpan(0, read);
    }
}
