namespace DailyChallenges.Services
{
    public interface IFileValidator
    {
        /// <summary>
        /// Checks size and magic bytes (PNG, JPEG, WebP) and returns the content type derived from the bytes.
        /// The client-declared content type is ignored. Throws a ValidationException for an invalid file.
        /// </summary>
        string ValidateImage(IFormFile file);
    }
}
