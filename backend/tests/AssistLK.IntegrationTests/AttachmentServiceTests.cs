using AssistLK.Application.Attachments;
using AssistLK.Application.Common.Exceptions;
using AssistLK.Application.ServiceRequests.DTOs;
using AssistLK.Application.Services;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using AssistLK.Infrastructure.Repositories;
using AssistLK.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssistLK.IntegrationTests;

// ==============================================================================
// TEST SUITE: AttachmentServiceTests
// What this class tests:
//   - ServiceRequestAttachmentService handling of photo uploads, deletes, and slot allocation.
//   - Dual-write fault compensation (deleting binary on DB failure; preserving binary on uncertain commit).
//   - Storage failure handling and background orphan cleanup via AttachmentReconciliationService.
//   - ServiceRequestService evidence revision tracking when details, photos, or clarification answers change.
//   - Concurrency guards preventing stale AI analysis from being applied or marked ready for matching.
// Why these tests are needed:
//   - Protects multi-step storage-and-database consistency so files are never permanently orphaned or broken.
//   - Ensures AI agent problem analysis strictly synchronizes with customer evidence via EvidenceRevision.
// What is real and what is mocked/faked:
//   - Real: ServiceRequestAttachmentService, ServiceRequestService, ServiceRequestAttachmentRepository,
//           ServiceRequestRepository, ProblemAnalysisRepository, AttachmentReconciliationService.
//   - In-Memory/Faked: EF Core InMemory database (AssistLKDbContext), FakeAttachmentStorage (in-memory dict),
//                     Normalizer (fake IAttachmentImageNormalizer), Repository decorator for fault injection.
// Prerequisites:
//   - None (runs purely in-memory).
// Run from repository root:
//   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.AttachmentServiceTests"
// ==============================================================================
public class AttachmentServiceTests
{
    private sealed class Normalizer : IAttachmentImageNormalizer
    {
        public Task<NormalizedImage> NormalizeAsync(Stream input, string fileName, string contentType, CancellationToken cancellationToken = default)
            => Task.FromResult(new NormalizedImage(new byte[] { 1, 2, 3 }, 20, 10, new string('a', 64)));
    }
    private sealed class Repository(IServiceRequestAttachmentRepository inner) : IServiceRequestAttachmentRepository
    {
        public bool FailSave { get; set; }
        public bool FailReferenceCheck { get; set; }
        public Task<ServiceRequest?> GetOwnedAsync(Guid requestId, Guid customerId, CancellationToken ct) => inner.GetOwnedAsync(requestId, customerId, ct);
        public void Add(ServiceRequestAttachment a) => inner.Add(a);
        public void Remove(ServiceRequestAttachment a) => inner.Remove(a);
        public Task SaveAsync(CancellationToken ct) => FailSave ? throw new IOException("DB unavailable") : inner.SaveAsync(ct);
        public Task<bool> IsReferencedAsync(string key, CancellationToken ct) => FailReferenceCheck ? throw new IOException("Unknown commit outcome") : inner.IsReferencedAsync(key, ct);
    }
    private sealed class Fixture : IDisposable
    {
        public AssistLKDbContext Db { get; } = new(new DbContextOptionsBuilder<AssistLKDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public ServiceRequest Request { get; } = new() { CustomerId = Guid.NewGuid(), Description = "Leaking pipe under kitchen sink", LocationText = "Colombo", Latitude = 6.9271m, Longitude = 79.8612m };
        public FakeAttachmentStorage Storage { get; } = new();
        public Repository Repository { get; }
        public ServiceRequestAttachmentService Service { get; }
        public ServiceRequestService Requests { get; }
        public Fixture()
        {
            Db.ServiceRequests.Add(Request); Db.SaveChanges();
            Repository = new Repository(new ServiceRequestAttachmentRepository(Db));
            Service = new(Repository, Storage, new Normalizer(), NullLogger<ServiceRequestAttachmentService>.Instance);
            Requests = new(new ServiceRequestRepository(Db), new ProblemAnalysisRepository(Db));
        }
        public Task<AttachmentResponse> Upload() => Service.UploadAsync(Request.Id, Request.CustomerId, Stream.Null, "../../address.jpg", "image/jpeg");
        public void Dispose() => Db.Dispose();
    }

    // Purpose: Checks that uploading and deleting photos increments EvidenceRevision and reuses free slot numbers (1..3) without altering existing clarification questions.
    // Why: Verifies that photo updates maintain evidence revisions cleanly while slot assignment recycles without side effects on active clarifications.
    // Scenario: Upload photo -> delete photo -> upload another photo on a request awaiting clarification.
    // Pass condition: EvidenceRevision advances from 1 -> 2 -> 3, storage key matches sanitized format, storage is cleaned upon delete, existing clarification question is untouched, and next upload gets slot 1.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.AttachmentServiceTests.AddsRemovesAndReusesSlotsWithoutChangingClarification"
    [Fact]
    public async Task AddsRemovesAndReusesSlotsWithoutChangingClarification()
    {
        using var f = new Fixture();
        Assert.Equal(1, f.Request.EvidenceRevision);
        f.Request.Status = ServiceRequestStatus.AwaitingInformation;
        var question = new ServiceRequestClarification { ServiceRequestId = f.Request.Id, ClarificationRound = 2, Sequence = 1, Question = "Where?" };
        f.Db.ServiceRequestClarifications.Add(question); await f.Db.SaveChangesAsync();
        var photo = await f.Upload();
        Assert.Equal(2, f.Request.EvidenceRevision);
        var row = Assert.Single(f.Db.ServiceRequestAttachments);
        Assert.Matches("^[a-f0-9]{32}\\.jpg$", row.StorageKey);
        Assert.Equal(new string('a', 64), row.ContentHash);
        await f.Service.DeleteAsync(f.Request.Id, f.Request.CustomerId, photo.Id);
        Assert.Equal(3, f.Request.EvidenceRevision);
        Assert.Empty(f.Storage.Files);
        Assert.Null(question.SupersededAt); Assert.Null(question.Answer);
        Assert.Equal(2, question.ClarificationRound);
        Assert.Equal(1, (await f.Upload()).Slot);
    }

    // Purpose: Checks that if database saving fails during attachment upload, the uploaded binary file in storage is compensated (deleted).
    // Why: Prevents orphaned binary files when a database commit fails during a multi-step storage-and-database operation.
    // Scenario: Upload attempted with repository configured to throw IOException on SaveAsync.
    // Pass condition: IOException is thrown, binary file is deleted from storage, and attachment table is empty.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.AttachmentServiceTests.DatabaseFailureCompensatesBinary"
    [Fact]
    public async Task DatabaseFailureCompensatesBinary()
    {
        using var f = new Fixture(); f.Repository.FailSave = true;
        await Assert.ThrowsAsync<IOException>(() => f.Upload());
        Assert.Empty(f.Storage.Files);
        Assert.Empty(await f.Db.ServiceRequestAttachments.AsNoTracking().ToListAsync());
    }

    // Purpose: Checks that if database commit fails and checking whether the attachment was saved also fails, the binary is not deleted.
    // Why: Prevents deleting an uploaded binary if the database transaction may actually have succeeded despite a timeout/network blip.
    // Scenario: Repository fails both save and reference check.
    // Pass condition: IOException is thrown, and storage retains the binary file instead of deleting it blindly.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.AttachmentServiceTests.UncertainCommitDoesNotBlindlyDeleteBinary"
    [Fact]
    public async Task UncertainCommitDoesNotBlindlyDeleteBinary()
    {
        using var f = new Fixture(); f.Repository.FailSave = true; f.Repository.FailReferenceCheck = true;
        await Assert.ThrowsAsync<IOException>(() => f.Upload());
        Assert.Single(f.Storage.Files);
    }

    // Purpose: Checks that if binary storage writing fails, no database record is created and EvidenceRevision remains unchanged.
    // Why: Protects database integrity against half-finished uploads when storage is unavailable.
    // Scenario: Storage configured with FailWrite = true during upload.
    // Pass condition: IOException is thrown, attachment table is empty, and EvidenceRevision remains 1.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.AttachmentServiceTests.StorageFailureLeavesNoMetadataOrPersistedRevisionChange"
    [Fact]
    public async Task StorageFailureLeavesNoMetadataOrPersistedRevisionChange()
    {
        using var f = new Fixture(); f.Storage.FailWrite = true;
        await Assert.ThrowsAsync<IOException>(() => f.Upload());
        Assert.Empty(await f.Db.ServiceRequestAttachments.ToListAsync());
        Assert.Equal(1, f.Request.EvidenceRevision);
    }

    // Purpose: Checks that if storage deletion fails during attachment removal, the attachment metadata is deleted from the DB, and background reconciliation successfully cleans up the orphan later.
    // Why: Prevents deleted photos from remaining accessible while ensuring orphaned storage files are reconciled eventually.
    // Scenario: Attachment deleted with storage delete failing, followed by running AttachmentReconciliationService.ReconcileAsync() once storage recovers.
    // Pass condition: Attachment is not listed or openable via service, reconciliation removes 1 orphaned file, and subsequent reconciliation finds 0 orphans.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.AttachmentServiceTests.FailedDeleteRemainsInvisibleAndReconciliationRetries"
    [Fact]
    public async Task FailedDeleteRemainsInvisibleAndReconciliationRetries()
    {
        using var f = new Fixture(); var photo = await f.Upload();
        f.Storage.FailDelete = true;
        await f.Service.DeleteAsync(f.Request.Id, f.Request.CustomerId, photo.Id);
        Assert.Empty(await f.Service.ListAsync(f.Request.Id, f.Request.CustomerId));
        Assert.Single(f.Storage.Files);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Service.OpenAsync(f.Request.Id, f.Request.CustomerId, photo.Id));
        f.Storage.FailDelete = false;
        var cleanup = new AttachmentReconciliationService(f.Repository, f.Storage, NullLogger<AttachmentReconciliationService>.Instance);
        Assert.Equal(1, await cleanup.ReconcileAsync());
        Assert.Empty(f.Storage.Files);
        await f.Upload();
        Assert.Equal(0, await cleanup.ReconcileAsync());
        Assert.Single(f.Storage.Files);
    }

    // Purpose: Checks that modifying key service request fields (description, category hint, location text, coordinates) increments EvidenceRevision, while idempotent repeat saves do not.
    // Why: Any change in customer-provided problem details represents new evidence that invalidates prior AI conclusions.
    // Scenario: Modifying description, category, location, latitude, or longitude on an existing request.
    // Pass condition: EvidenceRevision increments from 1 to 2 on first update, and stays at 2 on duplicate update with no further changes. Note: This parameterized test runs all 5 InlineData cases.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.AttachmentServiceTests.RelevantEditsIncrementRevision"
    [Theory]
    [InlineData("description")][InlineData("category")][InlineData("location")][InlineData("latitude")][InlineData("longitude")]
    public async Task RelevantEditsIncrementRevision(string field)
    {
        using var f = new Fixture();
        f.Request.Latitude = 7; f.Request.Longitude = 79;
        await f.Db.SaveChangesAsync();
        var edit = new UpdateServiceRequestRequest { Description = f.Request.Description, LocationText = f.Request.LocationText, Latitude = 7, Longitude = 79 };
        switch (field)
        {
            case "description": edit.Description += " now"; break;
            case "category": edit.CategoryHint = "Plumbing"; break;
            case "location": edit.LocationText = "Kandy"; edit.Latitude = 6; edit.Longitude = 80; break;
            case "latitude": edit.Latitude = 6; break;
            case "longitude": edit.Longitude = 80; break;
        }
        await f.Requests.UpdateAsync(f.Request.CustomerId, f.Request.Id, edit);
        Assert.Equal(2, f.Request.EvidenceRevision);
        await f.Requests.UpdateAsync(f.Request.CustomerId, f.Request.Id, edit);
        Assert.Equal(2, f.Request.EvidenceRevision);
    }

    // Purpose: Checks that a service request cannot be marked ReadyForMatching if its EvidenceRevision does not match the latest ProblemAnalysis revision.
    // Why: Prevents transitioning a request to matching based on out-of-date AI analysis when new photos or details were added afterwards.
    // Scenario: Request at revision 3 but analysis recorded at revision 2 attempts to transition to ReadyForMatching.
    // Pass condition: Throws ConflictException while status remains Analyzed; once analysis revision matches request revision (3), MarkReadyForMatchingAsync succeeds and status becomes ReadyForMatching.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.AttachmentServiceTests.CurrentAnalysisRevisionAndStaleReadinessGuard"
    [Fact]
    public async Task CurrentAnalysisRevisionAndStaleReadinessGuard()
    {
        using var f = new Fixture(); await f.Upload();
        await f.Requests.ApplyProblemAnalysisResultAsync(new ApplyProblemAnalysisResult
        {
            ServiceRequestId = f.Request.Id, EvidenceRevision = 2, Category = "Plumbing", Urgency = ServiceRequestUrgency.Medium,
            DetectedProblem = "Possible pipe leak", Confidence = .8m, AgentName = "ProblemUnderstandingAgent"
        });
        Assert.Equal(2, Assert.Single(f.Db.ProblemAnalyses).EvidenceRevision);
        f.Request.EvidenceRevision = 3; await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictException>(() => f.Requests.MarkReadyForMatchingAsync(f.Request.Id, f.Request.CustomerId));
        Assert.Equal(ServiceRequestStatus.Analyzed, f.Request.Status);
        f.Db.ProblemAnalyses.Single().EvidenceRevision = 3; await f.Db.SaveChangesAsync();
        await f.Requests.MarkReadyForMatchingAsync(f.Request.Id, f.Request.CustomerId);
        Assert.Equal(ServiceRequestStatus.ReadyForMatching, f.Request.Status);
    }

    // Purpose: Checks that submitting clarification answers increments EvidenceRevision while keeping the current clarification round and questions intact.
    // Why: Clarification answers provide new factual evidence for the AI agent to re-analyze without artificially inflating round counts.
    // Scenario: Customer submits answer "Under the sink" for round 2 question.
    // Pass condition: EvidenceRevision increments to 2, question answer is saved, round stays at 2, and status remains AwaitingInformation.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.AttachmentServiceTests.ClarificationAnswersIncrementRevisionWithoutAddingRounds"
    [Fact]
    public async Task ClarificationAnswersIncrementRevisionWithoutAddingRounds()
    {
        using var f = new Fixture();
        f.Request.Status = ServiceRequestStatus.AwaitingInformation;
        var question = new ServiceRequestClarification { ServiceRequestId = f.Request.Id, ClarificationRound = 2, Sequence = 1, Question = "Where is the leak?" };
        f.Db.ServiceRequestClarifications.Add(question); await f.Db.SaveChangesAsync();
        await f.Requests.SubmitClarificationAnswersAsync(f.Request.CustomerId, f.Request.Id,
            new SubmitClarificationAnswersRequest { ClarificationRound = 2, Answers = new() { new() { ClarificationId = question.Id, Answer = "Under the sink" } } });
        Assert.Equal(2, f.Request.EvidenceRevision);
        Assert.Equal(2, Assert.Single(f.Db.ServiceRequestClarifications).ClarificationRound);
        Assert.Equal("Under the sink", question.Answer);
        Assert.Null(question.SupersededAt);
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, f.Request.Status);
    }

    // Purpose: Checks that applying an analysis result whose EvidenceRevision is older than the service request's current revision is rejected.
    // Why: Prevents race conditions where a slow/delayed agent response overwrites fresh evidence added while the agent was running.
    // Scenario: Request uploaded photo (moving revision to 2), but agent attempts to submit analysis with EvidenceRevision = 1.
    // Pass condition: Throws ConflictException and no problem analysis record is saved in the database.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.AttachmentServiceTests.StaleAnalysisResultCannotBeApplied"
    [Fact]
    public async Task StaleAnalysisResultCannotBeApplied()
    {
        using var f = new Fixture(); await f.Upload();
        await Assert.ThrowsAsync<ConflictException>(() => f.Requests.ApplyProblemAnalysisResultAsync(new ApplyProblemAnalysisResult
        {
            ServiceRequestId = f.Request.Id, EvidenceRevision = 1, Category = "Plumbing", Urgency = ServiceRequestUrgency.Low,
            DetectedProblem = "Possible pipe leak", Confidence = .8m, AgentName = "ProblemUnderstandingAgent"
        }));
        Assert.Empty(f.Db.ProblemAnalyses);
    }
}
