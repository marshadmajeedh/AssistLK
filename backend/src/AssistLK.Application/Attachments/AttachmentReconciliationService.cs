using Microsoft.Extensions.Logging;

namespace AssistLK.Application.Attachments;

// Explicit maintenance operation; no scheduler. Call with uploads quiesced.
public sealed class AttachmentReconciliationService(IServiceRequestAttachmentRepository repository,
    IServiceRequestAttachmentStorage storage, ILogger<AttachmentReconciliationService> logger)
{
    public async Task<int> ReconcileAsync(CancellationToken ct = default)
    {
        var deleted = 0;
        foreach (var file in storage.GetCleanupCandidates(DateTime.UtcNow.AddDays(-1)))
        {
            ct.ThrowIfCancellationRequested();
            // Query failures abort cleanup: inability to prove absence must not delete evidence.
            if (await repository.IsReferencedAsync(file.Key, ct)) continue;
            try { await storage.DeleteAsync(file.Key, ct); deleted++; }
            catch (IOException) { logger.LogWarning("Attachment reconciliation will retry an inaccessible object."); }
        }
        return deleted;
    }
}
