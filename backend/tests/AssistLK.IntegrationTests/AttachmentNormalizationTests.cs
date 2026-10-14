using System.Security.Cryptography;
using AssistLK.Infrastructure.Attachments;
using ImageMagick;

namespace AssistLK.IntegrationTests;

/// <summary>
/// What this class tests:
///   Image normalization pipeline using ImageMagick (AttachmentImageNormalizer), covering format conversion (JPEG/PNG/WebP -> JPEG), MIME/extension consistency, 5MB file size limits, dimension/area decompression bomb protections (10,000px, 24MP), 2048px downscaling, EXIF orientation correction, EXIF/GPS/comment privacy stripping, and rejection of truncated, animated, or malformed images.
/// Why these tests are needed:
///   Protects the backend against decompression bombs and parser exploits, strips private customer GPS coordinates, and standardizes image formats for multimodal AI models.
/// What is real and what is mocked/faked:
///   - Real: ImageMagick library (MagickImage, MagickImageCollection, ExifProfile) and AttachmentImageNormalizer.
///   - Mocked/Faked: None.
/// Prerequisites:
///   None. ImageMagick native dependencies bundled.
/// Run from repository root:
///   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.AttachmentNormalizationTests"
/// </summary>
public class AttachmentNormalizationTests
{
    private readonly AttachmentImageNormalizer _normalizer = new();
    private static byte[] ImageBytes(MagickFormat format, uint w = 80, uint h = 40)
    {
        using var image = new MagickImage(MagickColors.Red, w, h);
        return image.ToByteArray(format);
    }

    /// <summary>
    /// Purpose: Checks that JPEG, PNG, and WebP images are normalized into JPEG without upscaling smaller dimensions.
    /// Why: Guarantees consistent image format for downstream vision models while preventing unnecessary image stretching.
    /// Scenario: Normalizes 80x40 images in Jpeg, Png, and WebP formats.
    /// Pass condition: Output format is Jpeg, dimensions remain 80x40, and content hash matches SHA-256.
    /// Note: Parameterized test (Theory). The command below executes all 3 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.AttachmentNormalizationTests.AllowedFormatsBecomeJpegWithoutUpscaling"
    /// </summary>
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

    /// <summary>
    /// Purpose: Checks that mismatched filename extensions, forbidden formats (SVG, PDF, GIF, HEIC), or conflicting MIME types are rejected.
    /// Why: Prevents polyglot file uploads, script injection (SVG), and format spoofing attacks.
    /// Scenario: Submits JPEG bytes with SVG, PDF, GIF, HEIC, or mismatched MIME types.
    /// Pass condition: Throws ArgumentException for each mismatched or unsupported combination.
    /// Note: Parameterized test (Theory). The command below executes all 6 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.AttachmentNormalizationTests.DeclaredFormatsMustAgree"
    /// </summary>
    [Theory]
    [InlineData("file.svg", "image/svg+xml")][InlineData("file.pdf", "application/pdf")]
    [InlineData("file.gif", "image/gif")][InlineData("file.heic", "image/heic")]
    [InlineData("file.png", "image/jpeg")][InlineData("file.jpg", "image/png")]
    public async Task DeclaredFormatsMustAgree(string name, string mime)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _normalizer.NormalizeAsync(
            new MemoryStream(ImageBytes(MagickFormat.Jpeg)), name, mime));
    }

    /// <summary>
    /// Purpose: Checks that streams exceeding 5MB (5 * 1024 * 1024 + 1 bytes) are rejected even when Content-Length header is omitted.
    /// Why: Denial-of-service defense: prevents attackers from bypassing file size limits via chunked transfer encoding.
    /// Scenario: Passes a 5MB + 1 byte stream to NormalizeAsync.
    /// Pass condition: Throws ArgumentException.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.AttachmentNormalizationTests.RejectsOversizeEvenWithoutDeclaredLength"
    /// </summary>
    [Fact]
    public async Task RejectsOversizeEvenWithoutDeclaredLength()
        => await Assert.ThrowsAsync<ArgumentException>(() => _normalizer.NormalizeAsync(
            new MemoryStream(new byte[5 * 1024 * 1024 + 1]), "a.jpg", "image/jpeg"));

    /// <summary>
    /// Purpose: Checks that images exceeding 10,000px on any edge or exceeding 24 million pixels in total area are rejected.
    /// Why: Decompression bomb (pixel flood) defense: protects server memory against images that are small on disk but allocate gigabytes when uncompressed.
    /// Scenario: Tests 10001x1, 1x10001, and 6000x4001 (24.006 MP) images.
    /// Pass condition: Throws ArgumentException for each case.
    /// Note: Parameterized test (Theory). The command below executes all 3 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.AttachmentNormalizationTests.RejectsDimensionsOrDecodedArea"
    /// </summary>
    [Theory]
    [InlineData(10001u, 1u)][InlineData(1u, 10001u)][InlineData(6000u, 4001u)]
    public async Task RejectsDimensionsOrDecodedArea(uint width, uint height)
        => await Assert.ThrowsAsync<ArgumentException>(() => _normalizer.NormalizeAsync(
            new MemoryStream(ImageBytes(MagickFormat.Png, width, height)), "a.png", "image/png"));

    /// <summary>
    /// Purpose: Checks that oversized high-resolution photos are proportionally downscaled to a max long edge of 2048px.
    /// Why: Reduces image payload size and token consumption for multimodal vision models without losing problem detail.
    /// Scenario: Submits a 3000x1500 PNG image.
    /// Pass condition: Normalized image dimensions are exactly 2048x1024.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.AttachmentNormalizationTests.ResizesLongEdge"
    /// </summary>
    [Fact]
    public async Task ResizesLongEdge()
    {
        var image = await _normalizer.NormalizeAsync(new MemoryStream(ImageBytes(MagickFormat.Png, 3000, 1500)), "a.png", "image/png");
        Assert.Equal(2048, image.Width); Assert.Equal(1024, image.Height);
    }

    /// <summary>
    /// Purpose: Checks that EXIF orientation is physically applied to rotate the pixels, followed by complete removal of EXIF metadata (GPS, camera info, comments).
    /// Why: Ensures photos taken on mobile phones display right-side up, while permanently removing sensitive location data and private metadata for customer privacy.
    /// Scenario: Image with Orientation=RightTop, GPS latitude/longitude, Make, and Comment is normalized.
    /// Pass condition: Width and Height swap (40x80), ExifProfile is null, and Comment is empty.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.AttachmentNormalizationTests.AppliesOrientationBeforeStrippingExifGpsAndComments"
    /// </summary>
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

    /// <summary>
    /// Purpose: Checks that truncated or corrupted JPEG files are rejected rather than partially decoded with grey fill.
    /// Why: Prevents corrupt or incomplete photo uploads from passing validation into the problem understanding pipeline.
    /// Scenario: Truncates the last 20 bytes of a valid JPEG.
    /// Pass condition: Throws ArgumentException.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.AttachmentNormalizationTests.RejectsTruncatedJpegInsteadOfAcceptingDecoderRecovery"
    /// </summary>
    [Fact]
    public async Task RejectsTruncatedJpegInsteadOfAcceptingDecoderRecovery()
    {
        var bytes = ImageBytes(MagickFormat.Jpeg);
        await Assert.ThrowsAsync<ArgumentException>(() => _normalizer.NormalizeAsync(
            new MemoryStream(bytes[..^20]), "a.jpg", "image/jpeg"));
    }

    /// <summary>
    /// Purpose: Checks that multi-frame animated WebP images are rejected.
    /// Why: AssistLK only accepts static still photos; animated images complicate model vision parsing and increase storage overhead.
    /// Scenario: Creates a 2-frame animated WebP collection and passes to normalizer.
    /// Pass condition: Throws ArgumentException.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.AttachmentNormalizationTests.RejectsAnimatedWebp"
    /// </summary>
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

    /// <summary>
    /// Purpose: Checks that incomplete image headers or random non-image bytes are rejected.
    /// Why: Validates defense against corrupted uploads, empty bodies, or fake extension files.
    /// Scenario: Passes partial JPEG magic bytes, partial PNG magic bytes, and arbitrary bytes { 0, 1, 2 }.
    /// Pass condition: Throws ArgumentException for all cases.
    /// Note: Parameterized test (Theory). The command below executes all 3 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.AttachmentNormalizationTests.RejectsMalformedImages"
    /// </summary>
    [Theory]
    [InlineData(new byte[] { 255, 216, 255 }, "a.jpg", "image/jpeg")]
    [InlineData(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, "a.png", "image/png")]
    [InlineData(new byte[] { 0, 1, 2 }, "a.jpg", "image/jpeg")]
    public async Task RejectsMalformedImages(byte[] content, string name, string mime)
        => await Assert.ThrowsAsync<ArgumentException>(() => _normalizer.NormalizeAsync(new MemoryStream(content), name, mime));
}
