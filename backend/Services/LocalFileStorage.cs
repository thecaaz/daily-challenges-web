namespace DailyChallenges.Services;

/// <summary>
/// Reads an uploaded image into memory (screenshots are stored as DB blobs), keeping the
/// file-reading and validation logic out of the services that persist it.
/// </summary>
public sealed class LocalFileStorage(IFileValidator validator) : IFileStorage
{
    /// <summary>The file's bytes and the content type detected from them (not the client-declared one).</summary>
    public async Task<(byte[]? Data, string? ContentType)> ReadFileAsync(IFormFile? file)
    {
        if (file == null || file.Length == 0) return (null, null);

        var contentType = validator.ValidateImage(file);
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);
        return (buffer.ToArray(), contentType);
    }
}
