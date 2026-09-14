using AssistLK.Agents.DTOs;
using AssistLK.Application.Attachments;
using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.Infrastructure.Repositories;

public sealed class AnalysisEvidenceRepository(AssistLKDbContext db) : IAnalysisEvidenceRepository
{
    public async Task<AnalysisEvidenceSnapshot?> GetAsync(Guid requestId, Guid customerId, CancellationToken ct)
    {
        var request = await db.ServiceRequests.AsNoTracking()
            .Where(r => r.Id == requestId && r.CustomerId == customerId)
            .Select(r => new
            {
                r.EvidenceRevision, r.Status,
                Attachments = r.Attachments.OrderBy(a => a.Slot).ThenBy(a => a.Id)
                    .Take(VisualEvidenceLimits.MaxCount + 1)
                    .Select(a => new AnalysisAttachment(a.Id, a.Slot, a.StorageKey, a.ContentType,
                        a.FileSizeBytes, a.Width, a.Height)).ToArray()
            }).SingleOrDefaultAsync(ct);
        return request is null ? null : new(request.EvidenceRevision, request.Status, request.Attachments);
    }
}
