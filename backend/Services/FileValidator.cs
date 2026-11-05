using DailyChallenges.Exceptions;

namespace DailyChallenges.Services
{
    /// <summary>
    /// Accepts PNG, JPEG and WebP images up to 2 MB. The type is detected from the file's magic bytes;
    /// the content type the client declared is ignored.
    /// </summary>
    public class FileValidator : IFileValidator
    {
        public const long MaxBytes = 2 * 1024 * 1024; // 2MB
        private const int HeaderLength = 12;

        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        private static readonly byte[] JpegSignature = { 0xFF, 0xD8, 0xFF };
        private static readonly byte[] Riff = { 0x52, 0x49, 0x46, 0x46 }; // "RIFF"
        private static readonly byte[] Webp = { 0x57, 0x45, 0x42, 0x50 }; // "WEBP"

        public string ValidateImage(IFormFile file)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (file.Length == 0) throw new ValidationException("file is empty");
            if (file.Length > MaxBytes) throw new ValidationException("file too large (max 2 MB)");

            var header = new byte[HeaderLength];
            int read = 0;
            using (var stream = file.OpenReadStream())
            {
                int n;
                while (read < header.Length && (n = stream.Read(header, read, header.Length - read)) > 0)
                    read += n;
            }

            return DetectContentType(header.AsSpan(0, read))
                ?? throw new ValidationException("invalid file type (PNG, JPEG or WebP required)");
        }

        /// <summary>"image/png", "image/jpeg" or "image/webp" from the leading bytes; null for anything else.</summary>
        public static string? DetectContentType(ReadOnlySpan<byte> header)
        {
            if (header.StartsWith(PngSignature)) return "image/png";
            if (header.StartsWith(JpegSignature)) return "image/jpeg";
            if (header.Length >= HeaderLength && header.StartsWith(Riff) && header.Slice(8, 4).SequenceEqual(Webp)) return "image/webp";
            return null;
        }
    }
}
