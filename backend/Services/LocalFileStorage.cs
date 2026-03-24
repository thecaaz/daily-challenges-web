using Microsoft.AspNetCore.Http;

namespace DailyChallenges.Services
{
    // Simple implementation that reads the file into memory and returns bytes.
    // This keeps existing DB blob storage behavior while isolating file-reading logic.
    public class LocalFileStorage : IFileStorage
    {
        private readonly IFileValidator _validator;

        public LocalFileStorage(IFileValidator validator)
        {
            _validator = validator;
        }

        public async Task<(byte[]? Data, string? ContentType)> ReadFileAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0) return (null, null);
            // validator will throw ArgumentException on invalid files
            _validator.ValidateImage(file);
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            return (ms.ToArray(), file.ContentType);
        }
    }
}
