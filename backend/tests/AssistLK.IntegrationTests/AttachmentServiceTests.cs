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
        public ServiceRequest Request { get; } = new() { CustomerId = Guid.NewGuid(), Description = "Leaking pipe under kitchen sink", LocationText = "Colombo" };
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

    [Fact]
    public async Task DatabaseFailureCompensatesBinary()
    {
        using var f = new Fixture(); f.Repository.FailSave = true;
        await Assert.ThrowsAsync<IOException>(() => f.Upload());
        Assert.Empty(f.Storage.Files);
        Assert.Empty(await f.Db.ServiceRequestAttachments.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task UncertainCommitDoesNotBlindlyDeleteBinary()
    {
        using var f = new Fixture(); f.Repository.FailSave = true; f.Repository.FailReferenceCheck = true;
        await Assert.ThrowsAsync<IOException>(() => f.Upload());
        Assert.Single(f.Storage.Files);
    }

    [Fact]
    public async Task StorageFailureLeavesNoMetadataOrPersistedRevisionChange()
    {
        using var f = new Fixture(); f.Storage.FailWrite = true;
        await Assert.ThrowsAsync<IOException>(() => f.Upload());
        Assert.Empty(await f.Db.ServiceRequestAttachments.ToListAsync());
        Assert.Equal(1, f.Request.EvidenceRevision);
    }

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
            case "location": edit.LocationText = "Kandy"; break;
            case "latitude": edit.Latitude = 6; break;
            case "longitude": edit.Longitude = 80; break;
        }
        await f.Requests.UpdateAsync(f.Request.CustomerId, f.Request.Id, edit);
        Assert.Equal(2, f.Request.EvidenceRevision);
        await f.Requests.UpdateAsync(f.Request.CustomerId, f.Request.Id, edit);
        Assert.Equal(2, f.Request.EvidenceRevision);
    }

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
