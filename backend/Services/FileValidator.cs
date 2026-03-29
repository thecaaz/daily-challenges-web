namespace DailyChallenges.Services
{
    public class FileValidator : IFileValidator
    {
        private const long DEFAULT_MAX_BYTES = 2 * 1024 * 1024; // 2MB
        private static readonly string[] DefaultAllowed = new[] { "image/png", "image/jpeg", "image/webp" };

        public void ValidateImage(IFormFile file)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (file.Length == 0) throw new ArgumentException("file is empty");
            if (file.Length > DEFAULT_MAX_BYTES) throw new ArgumentException("file too large");
            if (string.IsNullOrWhiteSpace(file.ContentType) || !DefaultAllowed.Contains(file.ContentType)) throw new ArgumentException("invalid file type");
        }
    }
}
