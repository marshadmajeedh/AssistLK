using AssistLK.Domain.Entities;

namespace AssistLK.Application.Attachments;

public sealed record AttachmentResponse(Guid Id, int Slot, string ContentType,
    long FileSizeBytes, int Width, int Height, DateTime CreatedAt);

// Binary data stays in the upload/storage boundary, never agent workflow input.
public sealed record NormalizedImage(byte[] Content, int Width, int Height, string ContentHash);
public sealed record StoredAttachmentFile(string Key, DateTime LastModifiedUtc);

public interface IAttachmentImageNormalizer
{
    Task<NormalizedImage> NormalizeAsync(Stream input, string fileName, string contentType,
        CancellationToken cancellationToken = default);
}

public interface IServiceRequestAttachmentStorage
{
    Task WriteAsync(string key, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
    IReadOnlyList<StoredAttachmentFile> GetCleanupCandidates(DateTime olderThanUtc);
}

public interface IServiceRequestAttachmentRepository
{
    Task<ServiceRequest?> GetOwnedAsync(Guid requestId, Guid customerId, CancellationToken ct);
    void Add(ServiceRequestAttachment attachment);
    void Remove(ServiceRequestAttachment attachment);
    Task SaveAsync(CancellationToken ct);
    Task<bool> IsReferencedAsync(string key, CancellationToken ct);
}
