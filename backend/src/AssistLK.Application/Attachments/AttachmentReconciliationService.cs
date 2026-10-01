using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AssistLK.Application.Attachments;

public sealed record AttachmentReconciliationResult(int DeletedCount, int ReferencedCount, int SkippedGracePeriodCount);

/// <summary>
/// Reconciles physical attachment storage objects against ServiceRequestAttachment database records.
/// Identifies and cleans up orphan files left behind by failed writes, rollbacks, or crashes,
/// while safeguarding active/in-flight uploads with a conservative grace period.
/// </summary>
public sealed class AttachmentReconciliationService
{
    private readonly IServiceRequestAttachmentRepository _repository;
    private readonly IServiceRequestAttachmentStorage _storage;
    private readonly ILogger<AttachmentReconciliationService> _logger;
    private readonly AttachmentReconciliationOptions _options;

    public AttachmentReconciliationService(
        IServiceRequestAttachmentRepository repository,
        IServiceRequestAttachmentStorage storage,
        ILogger<AttachmentReconciliationService>? logger = null,
        AttachmentReconciliationOptions? directOptions = null,
        IOptions<AttachmentReconciliationOptions>? optionsWrapper = null)
    {
        _repository = repository;
        _storage = storage;
        _logger = logger ?? NullLogger<AttachmentReconciliationService>.Instance;
        _options = directOptions ?? optionsWrapper?.Value ?? new AttachmentReconciliationOptions();
    }

    public async Task<int> ReconcileAsync(CancellationToken ct = default)
    {
        var result = await ReconcileDetailedAsync(ct: ct);
        return result.DeletedCount;
    }

    public async Task<int> ReconcileAsync(TimeSpan minimumAge, CancellationToken ct = default)
    {
        var result = await ReconcileDetailedAsync(minimumAge: minimumAge, ct: ct);
        return result.DeletedCount;
    }

    public async Task<AttachmentReconciliationResult> ReconcileDetailedAsync(TimeSpan? minimumAge = null, CancellationToken ct = default)
    {
        var age = minimumAge ?? TimeSpan.FromMinutes(_options.MinimumFileAgeMinutes);
        var cutoff = DateTime.UtcNow - age;

        _logger.LogInformation(
            "Attachment reconciliation started. Cutoff: {CutoffUtc}, GracePeriodMinutes: {GracePeriodMinutes}.",
            cutoff,
            age.TotalMinutes);

        var deleted = 0;
        var referenced = 0;
        var skippedGracePeriod = 0;

        foreach (var file in _storage.GetCleanupCandidates(cutoff))
        {
            ct.ThrowIfCancellationRequested();

            // Defense-in-depth: candidates inside the grace period must never be deleted
            if (file.LastModifiedUtc >= cutoff)
            {
                skippedGracePeriod++;
                continue;
            }

            // Query failures abort cleanup: inability to prove absence must not delete evidence.
            if (await _repository.IsReferencedAsync(file.Key, ct))
            {
                referenced++;
                continue;
            }

            try
            {
                await _storage.DeleteAsync(file.Key, ct);
                deleted++;
            }
            catch (IOException)
            {
                _logger.LogWarning("Attachment reconciliation will retry an inaccessible object.");
            }
        }

        _logger.LogInformation(
            "Attachment reconciliation completed. Deleted: {DeletedCount}, Referenced: {ReferencedCount}, SkippedGracePeriod: {SkippedGracePeriodCount}.",
            deleted,
            referenced,
            skippedGracePeriod);

        return new AttachmentReconciliationResult(deleted, referenced, skippedGracePeriod);
    }
}
