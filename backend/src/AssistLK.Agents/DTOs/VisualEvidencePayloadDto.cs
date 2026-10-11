using System.Text.Json.Serialization;

namespace AssistLK.Agents.DTOs;

/// <summary>Transient internal HTTP content. Never use as workflow audit input or memory.</summary>
public sealed class VisualEvidencePayloadDto
{
    [JsonPropertyName("attachmentId")]
    public Guid AttachmentId { get; init; }
    [JsonPropertyName("contentType")]
    public string ContentType { get; init; } = "image/jpeg";
    [JsonPropertyName("dataBase64")]
    public string DataBase64 { get; init; } = string.Empty;
    [JsonPropertyName("width")]
    public int Width { get; init; }
    [JsonPropertyName("height")]
    public int Height { get; init; }
}

public static class VisualEvidenceLimits
{
    public const int MaxCount = 3;
    public const int MaxDimension = 2048;
    public const int MaxImageBytes = 2 * 1024 * 1024;
    public const int MaxTotalBytes = 4 * 1024 * 1024;
    public const int MaxBase64Characters = 4 * ((MaxImageBytes + 2) / 3);
    public const int MaxTotalBase64Characters = 4 * ((MaxTotalBytes + 2 * MaxCount) / 3);

    // Defense at the HTTP serialization boundary, including direct typed-client callers.
    public static void Validate(IReadOnlyList<VisualEvidencePayloadDto> images)
    {
        if (images.Count > MaxCount || images.Select(i => i.AttachmentId).Distinct().Count() != images.Count)
            throw new ArgumentException("Invalid visual evidence collection.");
        long total = 0;
        long encodedTotal = 0;
        foreach (var image in images)
        {
            if (image.AttachmentId == Guid.Empty || image.ContentType != "image/jpeg"
                || image.Width is < 1 or > MaxDimension || image.Height is < 1 or > MaxDimension
                || image.DataBase64.Length is < 4 or > MaxBase64Characters)
                throw new ArgumentException("Invalid visual evidence metadata or encoded size.");
            var buffer = new byte[MaxImageBytes];
            if (!Convert.TryFromBase64String(image.DataBase64, buffer, out var length) || length == 0)
                throw new ArgumentException("Invalid or oversized visual evidence content.");
            if (Convert.ToBase64String(buffer, 0, length) != image.DataBase64)
                throw new ArgumentException("Visual evidence must use canonical Base64.");
            total += length;
            encodedTotal += image.DataBase64.Length;
        }
        if (total > MaxTotalBytes || encodedTotal > MaxTotalBase64Characters)
            throw new ArgumentException("Visual evidence exceeds aggregate transport limits.");
    }
}
