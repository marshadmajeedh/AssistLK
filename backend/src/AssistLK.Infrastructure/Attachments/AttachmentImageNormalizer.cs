using System.Buffers.Binary;
using System.Security.Cryptography;
using AssistLK.Application.Attachments;
using ImageMagick;

namespace AssistLK.Infrastructure.Attachments;

public sealed class AttachmentImageNormalizer : IAttachmentImageNormalizer
{
    // One decoder for the whole process, including separately constructed test instances.
    private static readonly SemaphoreSlim DecodeGate = new(1, 1);

    public async Task<NormalizedImage> NormalizeAsync(Stream input, string fileName, string contentType,
        CancellationToken cancellationToken = default)
    {
        await DecodeGate.WaitAsync(cancellationToken);
        try
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await input.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + read > ServiceRequestAttachmentService.MaxFileBytes)
                    throw new ArgumentException("Photo must not exceed 5 MiB (5242880 bytes).");
                buffer.Write(chunk, 0, read);
            }
            var bytes = buffer.ToArray();
            var format = Detect(bytes);
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var mime = contentType.ToLowerInvariant();
            var matches = format switch
            {
                MagickFormat.Jpeg => (extension is ".jpg" or ".jpeg") && mime == "image/jpeg",
                MagickFormat.Png => extension == ".png" && mime == "image/png",
                MagickFormat.WebP => extension == ".webp" && mime == "image/webp",
                _ => false
            };
            if (!matches) throw new ArgumentException("Photo format, filename extension and MIME type must agree (JPEG, PNG or static WebP).");
            RejectAnimation(bytes, format);
            // Force an allowlisted decoder: untrusted input cannot select delegates or URL/file coders.
            var settings = new MagickReadSettings { Format = format };
            ResourceLimits.Memory = 256UL * 1024 * 1024;
            ResourceLimits.ListLength = 2;
            ResourceLimits.MaxProfileSize = 1024 * 1024;
            ResourceLimits.Time = 20;
            ResourceLimits.Disk = 0;
            ResourceLimits.Thread = 1;
            var decoderWarned = false;
            using (var info = new MagickImageCollection())
            {
                info.Warning += (_, _) => decoderWarned = true;
                info.Ping(bytes, settings);
                if (decoderWarned) throw new ArgumentException("Photo contains malformed image data.");
                if (info.Count != 1) throw new ArgumentException("Only single-frame photos are accepted.");
                ValidateDimensions(info[0].Width, info[0].Height);
            }
            cancellationToken.ThrowIfCancellationRequested();
            using var images = new MagickImageCollection();
            images.Warning += (_, _) => decoderWarned = true;
            images.Read(bytes, settings);
            if (decoderWarned) throw new ArgumentException("Photo contains malformed image data.");
            if (images.Count != 1) throw new ArgumentException("Only single-frame photos are accepted.");
            var image = images[0];
            ValidateDimensions(image.Width, image.Height);
            image.AutoOrient();
            image.BackgroundColor = MagickColors.White;
            image.Alpha(AlphaOption.Remove);
            if (Math.Max(image.Width, image.Height) > 2048)
                image.Resize(new MagickGeometry(2048, 2048));
            image.Strip();
            image.Quality = 85;
            image.Format = MagickFormat.Jpeg;
            var normalized = image.ToByteArray();
            cancellationToken.ThrowIfCancellationRequested();
            return new NormalizedImage(normalized, (int)image.Width, (int)image.Height,
                Convert.ToHexString(SHA256.HashData(normalized)).ToLowerInvariant());
        }
        catch (MagickException)
        {
            // Decoder messages may contain private content; never return them to the client.
            throw new ArgumentException("Photo is malformed or exceeds image-processing resource limits.");
        }
        finally { DecodeGate.Release(); }
    }

    private static MagickFormat Detect(byte[] b)
    {
        if (b.Length >= 3 && b[0] == 0xff && b[1] == 0xd8 && b[2] == 0xff) return MagickFormat.Jpeg;
        if (b.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return MagickFormat.Png;
        if (b.Length >= 12 && b.AsSpan(0, 4).SequenceEqual("RIFF"u8) && b.AsSpan(8, 4).SequenceEqual("WEBP"u8)) return MagickFormat.WebP;
        throw new ArgumentException("Only JPEG, PNG and static WebP photos are accepted.");
    }

    private static void ValidateDimensions(uint width, uint height)
    {
        if (width == 0 || height == 0 || width > 10000 || height > 10000 || (ulong)width * height > 24_000_000)
            throw new ArgumentException("Photo dimensions must not exceed 10000 pixels per side or 24 megapixels.");
    }

    private static void RejectAnimation(byte[] bytes, MagickFormat format)
    {
        if (format == MagickFormat.Png)
        {
            for (long offset = 8; offset + 12 <= bytes.Length;)
            {
                var length = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan((int)offset, 4));
                if (bytes.AsSpan((int)offset + 4, 4).SequenceEqual("acTL"u8))
                    throw new ArgumentException("Animated PNG is not accepted.");
                offset += 12L + length;
            }
        }
        if (format == MagickFormat.WebP)
        {
            for (long offset = 12; offset + 8 <= bytes.Length;)
            {
                var tag = bytes.AsSpan((int)offset, 4);
                var length = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan((int)offset + 4, 4));
                if (tag.SequenceEqual("ANIM"u8) || tag.SequenceEqual("ANMF"u8)
                    || (tag.SequenceEqual("VP8X"u8) && offset + 8 < bytes.Length && (bytes[(int)offset + 8] & 2) != 0))
                    throw new ArgumentException("Animated WebP is not accepted.");
                offset += 8L + length + (length & 1);
            }
        }
    }
}
