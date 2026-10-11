using AssistLK.Agents.DTOs;
using AssistLK.Application.Common.Exceptions;
using AssistLK.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace AssistLK.Application.Attachments;

// Metadata snapshots are internal locals, not persisted agent input. Storage keys never reach Python.
public sealed record AnalysisAttachment(Guid Id, int Slot, string StorageKey,
    string ContentType, long FileSizeBytes, int Width, int Height);
public sealed record AnalysisEvidenceSnapshot(long Revision, ServiceRequestStatus Status,
    IReadOnlyList<AnalysisAttachment> Attachments);

public interface IAnalysisEvidenceRepository
{
    // Must read fresh, untracked state; bound the attachment query to MaxCount + 1.
    Task<AnalysisEvidenceSnapshot?> GetAsync(Guid requestId, Guid customerId, CancellationToken ct);
}

public sealed class ProblemVisualEvidenceService(
    IAnalysisEvidenceRepository repository, IServiceRequestAttachmentStorage storage,
    ILogger<ProblemVisualEvidenceService>? logger = null)
{
    public async Task<AnalysisEvidenceSnapshot> CaptureAsync(Guid requestId, Guid customerId,
        long revision, CancellationToken ct)
    {
        var snapshot = await repository.GetAsync(requestId, customerId, ct)
            ?? throw new KeyNotFoundException("Service request evidence was not found.");
        if (snapshot.Revision != revision || snapshot.Status != ServiceRequestStatus.Analyzing)
            throw new ConflictException("Request evidence changed during analysis. Refresh and retry.");
        try { Validate(snapshot.Attachments); }
        catch (InvalidOperationException)
        {
            logger?.LogWarning("Visual evidence metadata rejected: count {Count}, revision {Revision}.",
                snapshot.Attachments.Count, snapshot.Revision);
            throw;
        }
        return snapshot with { Attachments = snapshot.Attachments.OrderBy(a => a.Slot).ThenBy(a => a.Id).ToArray() };
    }

    public async Task EnsureCurrentAsync(Guid requestId, Guid customerId, long revision, CancellationToken ct)
    {
        var current = await repository.GetAsync(requestId, customerId, ct);
        if (current is null || current.Revision != revision || current.Status != ServiceRequestStatus.Analyzing)
            throw new ConflictException("Analysis was produced for outdated request evidence.");
    }

    /// <summary>Called only AFTER StartExecutionAsync has persisted the small audit snapshot.</summary>
    public async Task<IReadOnlyList<VisualEvidencePayloadDto>> LoadAsync(AnalysisEvidenceSnapshot snapshot,
        CancellationToken ct)
    {
        Validate(snapshot.Attachments);
        var evidence = new List<VisualEvidencePayloadDto>();
        long total = 0;
        try
        {
            foreach (var a in snapshot.Attachments)
            {
                ct.ThrowIfCancellationRequested();
                await using var stream = await storage.OpenReadAsync(a.StorageKey, ct);
                using var buffer = new MemoryStream();
                var chunk = new byte[81920];
                int read;
                while ((read = await stream.ReadAsync(chunk, ct)) > 0)
                {
                    total += read;
                    if (buffer.Length + read > VisualEvidenceLimits.MaxImageBytes || total > VisualEvidenceLimits.MaxTotalBytes)
                        throw new InvalidOperationException("Photo evidence exceeds internal transport limits.");
                    buffer.Write(chunk, 0, read);
                }
                if (buffer.Length == 0 || buffer.Length != a.FileSizeBytes)
                    throw new InvalidOperationException("Photo evidence is incomplete.");
                evidence.Add(new VisualEvidencePayloadDto
                {
                    AttachmentId = a.Id, ContentType = a.ContentType, Width = a.Width, Height = a.Height,
                    DataBase64 = Convert.ToBase64String(buffer.GetBuffer(), 0, checked((int)buffer.Length))
                });
            }
            return evidence;
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            // Never retain an inner storage exception: it may contain private paths or filenames.
            logger?.LogWarning("Visual evidence load failed: count {Count}, revision {Revision}.",
                snapshot.Attachments.Count, snapshot.Revision);
            throw new InvalidOperationException("Photo evidence could not be loaded safely. Please retry analysis.");
        }
    }

    private static void Validate(IReadOnlyList<AnalysisAttachment> items)
    {
        if (items.Count > VisualEvidenceLimits.MaxCount || items.Select(a => a.Id).Distinct().Count() != items.Count
            || items.Select(a => a.Slot).Distinct().Count() != items.Count)
            throw new InvalidOperationException("Photo evidence metadata is invalid.");
        long total = 0;
        foreach (var a in items)
        {
            if (a.Id == Guid.Empty || a.Slot is < 1 or > 3 || a.ContentType != "image/jpeg"
                || a.Width is < 1 or > VisualEvidenceLimits.MaxDimension || a.Height is < 1 or > VisualEvidenceLimits.MaxDimension
                || a.FileSizeBytes is < 1 or > VisualEvidenceLimits.MaxImageBytes)
                throw new InvalidOperationException("Photo evidence metadata exceeds internal transport limits.");
            total += a.FileSizeBytes;
        }
        if (total > VisualEvidenceLimits.MaxTotalBytes)
            throw new InvalidOperationException("Photo evidence exceeds the aggregate transport limit.");
    }
}
