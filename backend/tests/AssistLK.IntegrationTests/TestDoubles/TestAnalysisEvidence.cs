using AssistLK.Application.Attachments;
using AssistLK.Application.Interfaces;
using AssistLK.Tests.Shared;

namespace AssistLK.IntegrationTests.TestDoubles;

// Existing text-only fixtures use their own request repositories, not a live storage/Python process.
internal sealed class TestAnalysisEvidence(IServiceRequestRepository requests) : IAnalysisEvidenceRepository
{
    public static ProblemVisualEvidenceService Create(IServiceRequestRepository requests)
        => new(new TestAnalysisEvidence(requests), new FakeAttachmentStorage());

    public async Task<AnalysisEvidenceSnapshot?> GetAsync(Guid requestId, Guid customerId, CancellationToken ct)
    {
        var request = await requests.GetByIdAsync(requestId, cancellationToken: ct);
        return request is null || request.CustomerId != customerId ? null : new(request.EvidenceRevision,
            request.Status, request.Attachments.Select(a => new AnalysisAttachment(a.Id, a.Slot,
                a.StorageKey, a.ContentType, a.FileSizeBytes, a.Width, a.Height)).ToArray());
    }
}
