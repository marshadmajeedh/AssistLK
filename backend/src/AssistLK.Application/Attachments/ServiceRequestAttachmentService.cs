using AssistLK.Application.Common.Exceptions;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace AssistLK.Application.Attachments;

public sealed class ServiceRequestAttachmentService(
    IServiceRequestAttachmentRepository repository,
    IServiceRequestAttachmentStorage storage,
    IAttachmentImageNormalizer normalizer,
    ILogger<ServiceRequestAttachmentService> logger)
{
    public const int MaxFileBytes = 5 * 1024 * 1024;
    public const int MaxRequestBytes = 6 * 1024 * 1024;

    public async Task<AttachmentResponse> UploadAsync(Guid requestId, Guid customerId,
        Stream input, string fileName, string contentType, CancellationToken ct = default)
    {
        var request = await OwnedAsync(requestId, customerId, ct);
        RequireEditable(request);
        var slot = Enumerable.Range(1, 3).FirstOrDefault(s => request.Attachments.All(a => a.Slot != s));
        if (slot == 0) throw new ConflictException("A request can have at most three photos.");
        var image = await normalizer.NormalizeAsync(input, fileName, contentType, ct);
        var attachment = new ServiceRequestAttachment
        {
            ServiceRequestId = request.Id,
            StorageKey = Guid.NewGuid().ToString("N") + ".jpg",
            Slot = slot, Width = image.Width, Height = image.Height,
            FileSizeBytes = image.Content.LongLength, ContentHash = image.ContentHash
        };
        // Promote before the atomic DB save. Uncertain failures are reconciled, never blindly deleted.
        try
        {
            await storage.WriteAsync(attachment.StorageKey, image.Content, ct);
            repository.Add(attachment);
            request.EvidenceRevision = checked(request.EvidenceRevision + 1);
            await repository.SaveAsync(ct);
        }
        catch
        {
            try
            {
                if (!await repository.IsReferencedAsync(attachment.StorageKey, CancellationToken.None))
                    await storage.DeleteAsync(attachment.StorageKey, CancellationToken.None);
            }
            catch
            {
                logger.LogWarning("Attachment upload cleanup deferred to reconciliation.");
            }
            throw;
        }
        return Map(attachment);
    }

    public async Task<IReadOnlyList<AttachmentResponse>> ListAsync(Guid requestId, Guid customerId,
        CancellationToken ct = default) => (await OwnedAsync(requestId, customerId, ct))
        .Attachments.OrderBy(a => a.Slot).Select(Map).ToArray();

    public async Task<Stream> OpenAsync(Guid requestId, Guid customerId, Guid attachmentId,
        CancellationToken ct = default)
    {
        var request = await OwnedAsync(requestId, customerId, ct);
        var attachment = Find(request, attachmentId);
        try { return await storage.OpenReadAsync(attachment.StorageKey, ct); }
        catch (FileNotFoundException) { throw new KeyNotFoundException("Photo content is unavailable."); }
    }

    public async Task DeleteAsync(Guid requestId, Guid customerId, Guid attachmentId,
        CancellationToken ct = default)
    {
        var request = await OwnedAsync(requestId, customerId, ct);
        RequireEditable(request);
        var attachment = Find(request, attachmentId);
        repository.Remove(attachment);
        request.EvidenceRevision = checked(request.EvidenceRevision + 1);
        await repository.SaveAsync(ct);
        try { await storage.DeleteAsync(attachment.StorageKey, CancellationToken.None); }
        catch { logger.LogWarning("Attachment binary deletion deferred to reconciliation."); }
    }

    private async Task<ServiceRequest> OwnedAsync(Guid requestId, Guid customerId, CancellationToken ct)
        => await repository.GetOwnedAsync(requestId, customerId, ct)
            ?? throw new KeyNotFoundException("Service request was not found.");

    private static ServiceRequestAttachment Find(ServiceRequest request, Guid id)
        => request.Attachments.SingleOrDefault(a => a.Id == id)
            ?? throw new KeyNotFoundException("Photo was not found.");

    private static void RequireEditable(ServiceRequest request)
    {
        if (request.Status is not ServiceRequestStatus.Created and not ServiceRequestStatus.AwaitingInformation)
            throw new ConflictException("Photos cannot be changed in the current request status.");
    }

    private static AttachmentResponse Map(ServiceRequestAttachment a)
        => new(a.Id, a.Slot, a.ContentType, a.FileSizeBytes, a.Width, a.Height, a.CreatedAt);
}
