using DailyChallenges.Exceptions;
using DailyChallenges.Services;
using Microsoft.AspNetCore.Http;
using Moq;

namespace DailyChallenges.Tests;

public class FileValidatorTests
{
    private readonly FileValidator _validator = new();

    internal static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52 };
    internal static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1 };
    internal static readonly byte[] Webp = { 0x52, 0x49, 0x46, 0x46, 0x24, 0, 0, 0, 0x57, 0x45, 0x42, 0x50, 0x56, 0x50, 0x38, 0x20 };

    private static IFormFile MakeFile(byte[] content, string declaredType)
    {
        return new FormFile(new MemoryStream(content), 0, content.Length, "file", "upload.bin")
        {
            Headers = new HeaderDictionary(),
            ContentType = declaredType
        };
    }

    /// <summary>A file that claims a length without real content (for size checks).</summary>
    private static IFormFile MakeSizedFile(long length, byte[] header)
    {
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.Length).Returns(length);
        mock.Setup(f => f.ContentType).Returns("image/png");
        mock.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(header));
        return mock.Object;
    }

    // ── Null / empty / size ──────────────────────────────────────────────────

    [Fact]
    public void Null_file_throws()
    {
        Assert.Throws<ArgumentNullException>(() => _validator.ValidateImage(null!));
    }

    [Fact]
    public void Empty_file_is_rejected()
    {
        var ex = Assert.Throws<ValidationException>(() => _validator.ValidateImage(MakeFile(Array.Empty<byte>(), "image/png")));
        Assert.Contains("empty", ex.Message);
    }

    [Fact]
    public void Exactly_at_limit_passes()
    {
        Assert.Equal("image/png", _validator.ValidateImage(MakeSizedFile(2 * 1024 * 1024, Png)));
    }

    [Fact]
    public void One_byte_over_limit_is_rejected()
    {
        var ex = Assert.Throws<ValidationException>(() => _validator.ValidateImage(MakeSizedFile(2 * 1024 * 1024 + 1, Png)));
        Assert.Contains("large", ex.Message);
    }

    // ── Magic bytes ──────────────────────────────────────────────────────────

    public static IEnumerable<object[]> RealImages() => new[]
    {
        new object[] { Png, "image/png" },
        new object[] { Jpeg, "image/jpeg" },
        new object[] { Webp, "image/webp" },
    };

    [Theory]
    [MemberData(nameof(RealImages))]
    public void Detects_type_from_bytes(byte[] content, string expected)
    {
        Assert.Equal(expected, _validator.ValidateImage(MakeFile(content, expected)));
    }

    [Theory]
    [MemberData(nameof(RealImages))]
    public void Declared_type_is_ignored(byte[] content, string expected)
    {
        Assert.Equal(expected, _validator.ValidateImage(MakeFile(content, "application/octet-stream")));
        Assert.Equal(expected, _validator.ValidateImage(MakeFile(content, "")));
    }

    [Fact]
    public void Png_declared_as_jpeg_is_png()
    {
        Assert.Equal("image/png", _validator.ValidateImage(MakeFile(Png, "image/jpeg")));
    }

    public static IEnumerable<object[]> NotImages() => new[]
    {
        new object[] { "hello, I am text"u8.ToArray() },
        new object[] { "GIF89a\u0001\u0000\u0001\u0000"u8.ToArray() },             // GIF
        new object[] { new byte[] { 0x42, 0x4D, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 } }, // BMP
        new object[] { "%PDF-1.7 ......"u8.ToArray() },
        new object[] { "RIFF$\u0000\u0000\u0000WAVEfmt "u8.ToArray() },      // RIFF, but not WebP
        new object[] { new byte[] { 0x89, 0x50, 0x4E, 0x47 } },                   // truncated PNG signature
        new object[] { new byte[] { 0xFF, 0xD8 } },                               // truncated JPEG signature
        new object[] { "RIFF\u0000\u0000\u0000\u0000WEB"u8.ToArray() },           // truncated WebP header
    };

    [Theory]
    [MemberData(nameof(NotImages))]
    public void Non_images_are_rejected_even_when_declared_as_png(byte[] content)
    {
        var ex = Assert.Throws<ValidationException>(() => _validator.ValidateImage(MakeFile(content, "image/png")));
        Assert.Contains("invalid file type", ex.Message);
    }

    [Fact]
    public void LocalFileStorage_returns_the_detected_type_and_all_bytes()
    {
        var storage = new LocalFileStorage(_validator);
        var (data, type) = storage.ReadFileAsync(MakeFile(Png, "image/jpeg")).GetAwaiter().GetResult();
        Assert.Equal("image/png", type);
        Assert.Equal(Png, data);
    }
}
