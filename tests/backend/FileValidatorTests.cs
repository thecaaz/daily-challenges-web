using DailyChallenges.Services;
using Microsoft.AspNetCore.Http;
using Moq;

namespace DailyChallenges.Tests;

public class FileValidatorTests
{
    private readonly FileValidator _validator = new();

    private static IFormFile MakeFile(long length, string contentType)
    {
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.Length).Returns(length);
        mock.Setup(f => f.ContentType).Returns(contentType);
        return mock.Object;
    }

    // ── Null / empty ──────────────────────────────────────────────────────────

    [Fact]
    public void ValidateImage_NullFile_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _validator.ValidateImage(null!));
    }

    [Fact]
    public void ValidateImage_EmptyFile_ThrowsArgumentException()
    {
        var file = MakeFile(0, "image/png");
        var ex = Assert.Throws<ArgumentException>(() => _validator.ValidateImage(file));
        Assert.Contains("empty", ex.Message);
    }

    // ── Size limit ────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateImage_ExactlyAtLimit_Passes()
    {
        var file = MakeFile(2 * 1024 * 1024, "image/png");
        // Should not throw
        _validator.ValidateImage(file);
    }

    [Fact]
    public void ValidateImage_OneByteOverLimit_ThrowsArgumentException()
    {
        var file = MakeFile(2 * 1024 * 1024 + 1, "image/png");
        var ex = Assert.Throws<ArgumentException>(() => _validator.ValidateImage(file));
        Assert.Contains("large", ex.Message);
    }

    [Fact]
    public void ValidateImage_SmallFile_Passes()
    {
        var file = MakeFile(1024, "image/jpeg");
        _validator.ValidateImage(file);
    }

    // ── MIME type ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/webp")]
    public void ValidateImage_ValidContentType_Passes(string contentType)
    {
        var file = MakeFile(1024, contentType);
        _validator.ValidateImage(file);
    }

    [Theory]
    [InlineData("image/gif")]
    [InlineData("image/bmp")]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    [InlineData("")]
    public void ValidateImage_InvalidContentType_ThrowsArgumentException(string contentType)
    {
        var file = MakeFile(1024, contentType);
        var ex = Assert.Throws<ArgumentException>(() => _validator.ValidateImage(file));
        Assert.Contains("invalid file type", ex.Message);
    }

    [Fact]
    public void ValidateImage_NullContentType_ThrowsArgumentException()
    {
        var file = MakeFile(1024, null!);
        Assert.Throws<ArgumentException>(() => _validator.ValidateImage(file));
    }
}
