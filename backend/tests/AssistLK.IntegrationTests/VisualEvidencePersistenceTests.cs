using System.Text.Json;
using AssistLK.Application.Common.Exceptions;
using AssistLK.Application.ServiceRequests.DTOs;
using AssistLK.Application.Services;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using AssistLK.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.IntegrationTests;

public class VisualEvidencePersistenceTests
{
    private sealed class Fixture : IDisposable
    {
        public readonly AssistLKDbContext Db = new(new DbContextOptionsBuilder<AssistLKDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public readonly ServiceRequest Request = new() { CustomerId = Guid.NewGuid(), Description = "Possible sink leak", LocationText = "Colombo" };
        public readonly ServiceRequestService Service;
        public Fixture()
        {
            Db.Add(Request); Db.SaveChanges();
            Service = new(new ServiceRequestRepository(Db), new ProblemAnalysisRepository(Db), new TestDoubles.InMemoryServiceJobRepository());
        }
        public Guid[] Photos(int count = 3)
        {
            var photos = Enumerable.Range(1, count).Select(i => new ServiceRequestAttachment
                { ServiceRequestId = Request.Id, Slot = i, StorageKey = Guid.NewGuid() + ".jpg", FileSizeBytes = 100, Width = 80, Height = 40 }).ToArray();
            Db.AddRange(photos); Db.SaveChanges(); return photos.Select(p => p.Id).ToArray();
        }
        public ApplyProblemAnalysisResult Result(Guid[]? ids = null, string status = "used") => new()
        {
            ServiceRequestId = Request.Id, EvidenceRevision = Request.EvidenceRevision,
            Category = "Plumbing", DetectedProblem = "Possible leak near a pipe joint.", Confidence = .85m,
            Urgency = ServiceRequestUrgency.Medium, AgentName = "ProblemUnderstandingAgent",
            SuppliedAttachmentIds = ids ?? [],
            VisualEvidence = new() { VisionStatus = ids is null ? "not_requested" : status,
                AttachmentIdsUsed = status == "used" ? ids ?? [] : [],
                Observations = ids is not null && status == "used" ? ids.SelectMany((id, i) =>
                    Enumerable.Range(0, i == 2 ? 1 : 2).Select(_ => new ProblemAnalysisVisualObservation
                        { AttachmentId = id, Observation = "Moisture appears visible near the joint." })).ToArray() : [],
                Limitations = ids is not null ? ["The internal cause cannot be seen."] : [] }
        };
        public void Dispose() => Db.Dispose();
    }

    [Fact]
    public async Task TextOnlyAndLegacyAnalysisDefaultToNotRequested()
    {
        using var f = new Fixture();
        await f.Service.ApplyProblemAnalysisResultAsync(f.Result());
        Assert.Equal("not_requested", Assert.Single(f.Db.ProblemAnalyses).VisualEvidence.VisionStatus);
        var detail = await f.Service.GetByIdAsync(f.Request.Id, f.Request.CustomerId);
        Assert.Equal("not_requested", detail.LatestAnalysis!.VisualEvidence!.VisionStatus);
        Assert.Empty(new ProblemAnalysis().VisualEvidence.Observations);
    }

    [Fact]
    public async Task MaximumEvidencePersistsAndOnlyCustomerDetailExposesSafeValue()
    {
        using var f = new Fixture(); var ids = f.Photos();
        await f.Service.ApplyProblemAnalysisResultAsync(f.Result(ids));
        f.Db.ChangeTracker.Clear();
        var stored = Assert.Single(f.Db.ProblemAnalyses).VisualEvidence;
        Assert.Equal(ids, stored.AttachmentIdsUsed); Assert.Equal(5, stored.Observations.Count);
        var detail = await f.Service.GetByIdAsync(f.Request.Id, f.Request.CustomerId);
        Assert.Equal(5, detail.LatestAnalysis!.VisualEvidence!.Observations.Count);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize(detail, options);
        foreach (var forbidden in new[] { "storageKey", "contentHash", "dataBase64", "gemini", "openai", "chainOfThought", ".jpg" })
            Assert.DoesNotContain(forbidden, json);
        var list = await f.Service.GetCurrentCustomerRequestsAsync(f.Request.CustomerId);
        Assert.DoesNotContain("visualEvidence", JsonSerializer.Serialize(list, options));
        var admin = await f.Service.GetAllForAdminAsync();
        Assert.DoesNotContain("visualEvidence", JsonSerializer.Serialize(admin, options));
        var handoff = await f.Service.MarkReadyForMatchingAsync(f.Request.Id, f.Request.CustomerId);
        Assert.Equal(ServiceRequestStatus.ReadyForMatching, handoff.Status);
        var matching = JsonSerializer.Serialize(await f.Service.GetReadyForMatchingAsync(f.Request.Id), options);
        foreach (var field in new[] { "visualEvidence", "attachmentIdsUsed", "observations", "visionStatus" })
            Assert.DoesNotContain(field, matching);
    }

    [Theory]
    [InlineData("unknown")] [InlineData("unused")] [InlineData("foreign")]
    [InlineData("too-many")] [InlineData("long")] [InlineData("partial")]
    [InlineData("base64")] [InlineData("provider")] [InlineData("reasoning")]
    public async Task InvalidDomainEvidenceNeverCreatesAnalysis(string fault)
    {
        using var f = new Fixture(); var ids = f.Photos(); var result = f.Result(ids);
        var observed = ids[0]; var text = "Moisture appears visible.";
        if (fault == "unknown") observed = Guid.NewGuid();
        if (fault == "unused") result.SuppliedAttachmentIds = ids.Take(1).ToArray();
        if (fault == "foreign") result.SuppliedAttachmentIds = [Guid.NewGuid()];
        if (fault == "long") text = new string('x', 241);
        if (fault == "base64") text = Convert.ToBase64String(new byte[100]);
        if (fault == "provider") text = "inline_data contains private image data";
        if (fault == "reasoning") text = "chain-of-thought: private reasoning";
        result.VisualEvidence = new() { VisionStatus = fault == "partial" ? "partial" : "used",
            AttachmentIdsUsed = ids,
            Observations = Enumerable.Range(0, fault == "too-many" ? 6 : 1).Select(_ =>
                new ProblemAnalysisVisualObservation { AttachmentId = observed, Observation = text }).ToArray() };
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.ApplyProblemAnalysisResultAsync(result));
        Assert.Empty(f.Db.ProblemAnalyses); Assert.Equal(ServiceRequestStatus.Created, f.Request.Status);
    }

    [Fact]
    public async Task StaleRevisionRejectsNewVisualAnalysisAndHidesOldInsights()
    {
        using var f = new Fixture(); var ids = f.Photos(); var result = f.Result(ids);
        result.NeedsMoreInformation = true; result.FollowUpQuestions = ["Where is the leak?"];
        await f.Service.ApplyProblemAnalysisResultAsync(result);
        f.Request.EvidenceRevision++; f.Db.SaveChanges();
        await Assert.ThrowsAsync<ConflictException>(() => f.Service.ApplyProblemAnalysisResultAsync(result));
        Assert.Single(f.Db.ProblemAnalyses);
        var detail = await f.Service.GetByIdAsync(f.Request.Id, f.Request.CustomerId);
        Assert.Null(detail.LatestAnalysis!.VisualEvidence);
    }

    [Fact]
    public async Task CancellationKeepsAcceptedAnalysisAndPhotoReferences()
    {
        using var f = new Fixture(); var ids = f.Photos();
        await f.Service.ApplyProblemAnalysisResultAsync(f.Result(ids));
        await f.Service.CancelAsync(f.Request.CustomerId, f.Request.Id);
        var detail = await f.Service.GetByIdAsync(f.Request.Id, f.Request.CustomerId);
        Assert.Equal(ServiceRequestStatus.Cancelled, detail.Status);
        Assert.Equal(ids, detail.LatestAnalysis!.VisualEvidence!.AttachmentIdsUsed);
        Assert.Equal(3, f.Db.ServiceRequestAttachments.Count());
    }
}
