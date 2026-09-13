using System.Security.Cryptography;
using AssistLK.Infrastructure.Attachments;
using ImageMagick;

namespace AssistLK.IntegrationTests;

public class AttachmentNormalizationTests
{
    private readonly AttachmentImageNormalizer _normalizer = new();
    private static byte[] ImageBytes(MagickFormat format, uint w = 80, uint h = 40)
    {
        using var image = new MagickImage(MagickColors.Red, w, h);
        return image.ToByteArray(format);
    }

    [Theory]
    [InlineData(MagickFormat.Jpeg, "jpg", "image/jpeg")]
    [InlineData(MagickFormat.Png, "png", "image/png")]
    [InlineData(MagickFormat.WebP, "webp", "image/webp")]
    public async Task AllowedFormatsBecomeJpegWithoutUpscaling(MagickFormat format, string ext, string mime)
    {
        var normalized = await _normalizer.NormalizeAsync(new MemoryStream(ImageBytes(format)), "photo." + ext, mime);
        using var decoded = new MagickImage(normalized.Content);
        Assert.Equal(MagickFormat.Jpeg, decoded.Format);
        Assert.Equal(80, normalized.Width); Assert.Equal(40, normalized.Height);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(normalized.Content)).ToLowerInvariant(), normalized.ContentHash);
    }

    [Theory]
    [InlineData("file.svg", "image/svg+xml")][InlineData("file.pdf", "application/pdf")]
    [InlineData("file.gif", "image/gif")][InlineData("file.heic", "image/heic")]
    [InlineData("file.png", "image/jpeg")][InlineData("file.jpg", "image/png")]
    public async Task DeclaredFormatsMustAgree(string name, string mime)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _normalizer.NormalizeAsync(
            new MemoryStream(ImageBytes(MagickFormat.Jpeg)), name, mime));
    }

    [Fact]
    public async Task RejectsOversizeEvenWithoutDeclaredLength()
        => await Assert.ThrowsAsync<ArgumentException>(() => _normalizer.NormalizeAsync(
            new MemoryStream(new byte[5 * 1024 * 1024 + 1]), "a.jpg", "image/jpeg"));

    [Theory]
    [InlineData(10001u, 1u)][InlineData(1u, 10001u)][InlineData(6000u, 4001u)]
    public async Task RejectsDimensionsOrDecodedArea(uint width, uint height)
        => await Assert.ThrowsAsync<ArgumentException>(() => _normalizer.NormalizeAsync(
            new MemoryStream(ImageBytes(MagickFormat.Png, width, height)), "a.png", "image/png"));

    [Fact]
    public async Task ResizesLongEdge()
    {
        var image = await _normalizer.NormalizeAsync(new MemoryStream(ImageBytes(MagickFormat.Png, 3000, 1500)), "a.png", "image/png");
        Assert.Equal(2048, image.Width); Assert.Equal(1024, image.Height);
    }

    [Fact]
    public async Task AppliesOrientationBeforeStrippingExifGpsAndComments()
    {
        using var image = new MagickImage(MagickColors.Blue, 80, 40);
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Orientation, (ushort)6);
        exif.SetValue(ExifTag.GPSLatitudeRef, "N");
        exif.SetValue(ExifTag.GPSLongitudeRef, "E");
        exif.SetValue(ExifTag.Make, "Private camera");
        image.SetProfile(exif); image.Comment = "Private comment";
        image.Orientation = OrientationType.RightTop;
        var source = image.ToByteArray(MagickFormat.Jpeg);
        using var original = new MagickImage(source);
        Assert.Equal(OrientationType.RightTop, original.Orientation);
        Assert.NotNull(original.GetExifProfile());
        var normalized = await _normalizer.NormalizeAsync(new MemoryStream(source), "a.jpg", "image/jpeg");
        using var result = new MagickImage(normalized.Content);
        Assert.Equal(40, normalized.Width); Assert.Equal(80, normalized.Height);
        Assert.Null(result.GetExifProfile());
        Assert.True(string.IsNullOrEmpty(result.Comment));
    }

    [Fact]
    public async Task RejectsTruncatedJpegInsteadOfAcceptingDecoderRecovery()
    {
        var bytes = ImageBytes(MagickFormat.Jpeg);
        await Assert.ThrowsAsync<ArgumentException>(() => _normalizer.NormalizeAsync(
            new MemoryStream(bytes[..^20]), "a.jpg", "image/jpeg"));
    }

    [Fact]
    public async Task RejectsAnimatedWebp()
    {
        using var images = new MagickImageCollection();
        images.Add(new MagickImage(MagickColors.Red, 10, 10));
        images.Add(new MagickImage(MagickColors.Blue, 10, 10));
        foreach (var image in images) image.AnimationDelay = 10;
        await Assert.ThrowsAsync<ArgumentException>(() => _normalizer.NormalizeAsync(
            new MemoryStream(images.ToByteArray(MagickFormat.WebP)), "a.webp", "image/webp"));
    }

    [Theory]
    [InlineData(new byte[] { 255, 216, 255 }, "a.jpg", "image/jpeg")]
    [InlineData(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, "a.png", "image/png")]
    [InlineData(new byte[] { 0, 1, 2 }, "a.jpg", "image/jpeg")]
    public async Task RejectsMalformedImages(byte[] content, string name, string mime)
        => await Assert.ThrowsAsync<ArgumentException>(() => _normalizer.NormalizeAsync(new MemoryStream(content), name, mime));
}
