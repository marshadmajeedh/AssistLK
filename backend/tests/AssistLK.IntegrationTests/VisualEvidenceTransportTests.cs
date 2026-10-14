using System.Net;
using System.Text;
using System.Text.Json;
using AssistLK.Agents.Adapters;
using AssistLK.Agents.Clients;
using AssistLK.Agents.Configuration;
using AssistLK.Agents.Core;
using AssistLK.Agents.DTOs;
using AssistLK.Agents.Models;
using AssistLK.Application.Attachments;
using AssistLK.Application.Common.Exceptions;
using AssistLK.Application.Services;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using AssistLK.Infrastructure.Repositories;
using AssistLK.IntegrationTests.TestDoubles;
using AssistLK.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AssistLK.IntegrationTests;

/// <summary>
/// What this class tests:
///   End-to-end transport, validation, wire serialization, and privacy protection of customer problem photo evidence sent from C# backend to the Python FastAPI agent service (ProblemUnderstandingHttpClient), covering base64 exclusion from persistent logs/audit records, visual payload limits, corrupt metadata rejection, concurrent revision changes, customer isolation, error path sanitization, and transient image serialization ignore attributes.
/// Why these tests are needed:
///   Ensures that high-resolution image data (up to 4MB aggregate) passes securely over the internal wire with X-Internal-Api-Key without leaking private image binaries, internal storage paths, or base64 blobs into database tables (AgentExecutions, AgentMemories, ProblemAnalyses), audit logs, or error messages.
/// What is real and what is mocked/faked:
///   - Real: ProblemVisualEvidenceService, ProblemUnderstandingWorkflowService, ExternalProblemUnderstandingAgentAdapter, AgentMemoryService, AgentMonitoringService, AgentSafetyService, ServiceRequestService backed by EF Core In-Memory database.
///   - Mocked/Faked: HTTP transport via in-memory HttpMessageHandler simulating Python FastAPI responses and FakeAttachmentStorage.
/// Prerequisites:
///   None. Runs in-memory.
/// Run from repository root:
///   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.VisualEvidenceTransportTests"
/// </summary>
public class VisualEvidenceTransportTests
{
    private sealed class Fixture : IDisposable
    {
        public readonly AssistLKDbContext Db = new(new DbContextOptionsBuilder<AssistLKDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public readonly FakeAttachmentStorage Storage = new();
        public readonly ServiceRequest Request = new()
        {
            CustomerId = Guid.NewGuid(), Description = "Water pipe burst under the kitchen sink.",
            LocationText = "Colombo", EvidenceRevision = 7
        };
        public AgentExecutionRequestDto? Sent;
        public string? WireJson;
        public string? AuthHeader;
        public Action? DuringExecution;
        public Action<AgentExecutionResponseDto>? RewriteResponse;
        public HttpStatusCode ResponseStatus = HttpStatusCode.OK;
        public string ErrorBody = "";
        public bool NetworkFailure;
        public readonly ProblemVisualEvidenceService Evidence;
        public readonly ProblemUnderstandingWorkflowService Workflow;

        public Fixture()
        {
            Db.ServiceRequests.Add(Request);
            Db.SaveChanges();
            Evidence = new(new AnalysisEvidenceRepository(Db), Storage);
            var client = new ProblemUnderstandingHttpClient(new HttpClient(new Handler(async message =>
            {
                WireJson = await message.Content!.ReadAsStringAsync();
                Sent = JsonSerializer.Deserialize<AgentExecutionRequestDto>(WireJson)!;
                AuthHeader = message.Headers.GetValues("X-Internal-Api-Key").Single();
                DuringExecution?.Invoke();
                if (NetworkFailure) throw new HttpRequestException("unavailable");
                var response = await new FakeProblemUnderstandingClient().ExecuteAsync(Sent);
                RewriteResponse?.Invoke(response);
                return new HttpResponseMessage(ResponseStatus)
                {
                    Content = new StringContent(ResponseStatus == HttpStatusCode.OK
                        ? JsonSerializer.Serialize(response) : ErrorBody, Encoding.UTF8, "application/json")
                };
            })), new AgentServicesOptions { ProblemUnderstandingUrl = "http://internal", InternalApiKey = "test-internal-key" });
            var registry = new AgentRegistry();
            registry.Register(new ExternalProblemUnderstandingAgentAdapter(client));
            var memory = new AgentMemoryService(Db);
            Workflow = new(new AgentWorkflowService(Db), new AgentContextService(memory), memory,
                new AgentMonitoringService(Db), new AgentSafetyService(new AgentSafetyPolicyEngine(), Db),
                new AgentOrchestrator(registry), registry,
                new ServiceRequestService(new ServiceRequestRepository(Db), new ProblemAnalysisRepository(Db)), Evidence);
        }

        public ServiceRequestAttachment Add(int slot, int size = 64)
        {
            var a = new ServiceRequestAttachment
            {
                ServiceRequestId = Request.Id, Slot = slot, StorageKey = Guid.NewGuid() + ".jpg",
                FileSizeBytes = size, Width = 80, Height = 40
            };
            Request.Attachments.Add(a);
            Db.ServiceRequestAttachments.Add(a);
            Storage.Files[a.StorageKey] = Enumerable.Repeat((byte)slot, size).ToArray();
            Db.SaveChanges();
            return a;
        }
        public Task<ProblemUnderstandingWorkflowResult> Run() => Workflow.AnalyzeAsync(Request.Id, Request.CustomerId);
        public void Dispose() => Db.Dispose();
    }

    /// <summary>
    /// Purpose: Checks that valid visual analysis results (used, unsupported, failed) map to domain entities and execution logs without storing binary base64 or provider payloads.
    /// Why: Verifies that image insights are captured while preventing multi-megabyte image binaries from bloating database execution logs.
    /// Scenario: Agent returns visual results for status in ["used", "unsupported", "failed"].
    /// Pass condition: Domain VisionStatus matches, observations map correctly, base64 is not contained in persisted audit/memory, and execution output is under 5KB.
    /// Note: Parameterized test (Theory). The command below executes all 3 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.VisualEvidenceTransportTests.BoundedVisualResultIsMappedAndAuditedWithoutBinaryOrProviderPayload"
    /// </summary>
    [Theory]
    [InlineData("used")] [InlineData("unsupported")] [InlineData("failed")]
    public async Task BoundedVisualResultIsMappedAndAuditedWithoutBinaryOrProviderPayload(string status)
    {
        using var f = new Fixture();
        var attachment = f.Add(1);
        f.RewriteResponse = response =>
        {
            response.Result!.VisionStatus = status;
            response.Metadata!.Provider = "gemini";
            if (status == "used")
            {
                response.Result.AttachmentIdsUsed = [attachment.Id];
                response.Result.VisualObservations = [new() { AttachmentId = attachment.Id,
                    Observation = "Moisture appears visible around the pipe connection." }];
                response.Result.VisualLimitations = ["The internal cause cannot be seen."];
            }
        };
        var result = await f.Run();
        Assert.True(result.Success, result.ErrorMessage);
        var execution = Assert.Single(f.Db.AgentExecutions);
        using var document = JsonDocument.Parse(execution.Output!);
        var visual = document.RootElement.GetProperty("VisualResult");
        Assert.Equal(status, visual.GetProperty("visionStatus").GetString());
        var domain = Assert.Single(f.Db.ProblemAnalyses).VisualEvidence;
        Assert.Equal(status, domain.VisionStatus);
        Assert.Equal(status == "used" ? 1 : 0, domain.Observations.Count);
        Assert.Equal(status == "used" ? new[] { attachment.Id } : Array.Empty<Guid>(), domain.AttachmentIdsUsed);
        Assert.Equal(status == "used" ? new[] { "The internal cause cannot be seen." } : Array.Empty<string>(), domain.Limitations);
        Assert.DoesNotContain(f.Sent!.Input.VisualEvidence[0].DataBase64, JsonSerializer.Serialize(domain));
        Assert.Equal(status == "used" ? 1 : 0, visual.GetProperty("visualObservations").GetArrayLength());
        if (status == "used") Assert.Equal(attachment.Id,
            visual.GetProperty("visualObservations")[0].GetProperty("attachmentId").GetGuid());
        var persisted = JsonSerializer.Serialize(new
        {
            execution.Input, execution.Output, Memory = f.Db.AgentMemories.Select(m => m.Value).ToArray()
        });
        Assert.DoesNotContain(f.Sent!.Input.VisualEvidence[0].DataBase64, persisted);
        foreach (var forbidden in new[] { "dataBase64", "inline_data", "image_url", "chainOfThought", attachment.StorageKey })
            Assert.DoesNotContain(forbidden, persisted);
        Assert.True(execution.Output!.Length < 5000);
        Assert.Equal(7, Assert.Single(f.Db.ProblemAnalyses).EvidenceRevision);
    }

    /// <summary>
    /// Purpose: Checks that malformed visual output (unknown attachment IDs, >5 observations, >2 per image, >240 chars, partial status, unacknowledged photos, echoed base64, >3 limitations) fails safely.
    /// Why: Prevents hallucinated or corrupted visual agent outputs from polluting the database or echoing large base64 data in error logs.
    /// Scenario: Tests 12 fault types in visual agent response.
    /// Pass condition: Workflow result is Failed, zero ProblemAnalyses or AgentMemories persisted, status recovers to Created, and base64 is absent from errors and execution logs.
    /// Note: Parameterized test (Theory). The command below executes all 12 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.VisualEvidenceTransportTests.InvalidVisualOutputFailsWithoutPersistingRejectedContent"
    /// </summary>
    [Theory]
    [InlineData("unknown-id")] [InlineData("too-many")] [InlineData("long")]
    [InlineData("per-image")] [InlineData("partial")] [InlineData("no-ack")]
    [InlineData("payload")] [InlineData("summary-payload")] [InlineData("metadata-payload")]
    [InlineData("provider-payload")] [InlineData("failure-payload")] [InlineData("limitations")]
    public async Task InvalidVisualOutputFailsWithoutPersistingRejectedContent(string fault)
    {
        using var f = new Fixture();
        var attachment = f.Add(1);
        f.RewriteResponse = response =>
        {
            var output = response.Result!;
            response.Metadata!.Provider = "gemini";
            output.VisionStatus = "used";
            output.AttachmentIdsUsed = [attachment.Id];
            output.VisualObservations = [new() { AttachmentId = attachment.Id, Observation = "Moisture appears visible." }];
            var encoded = f.Sent!.Input.VisualEvidence[0].DataBase64;
            switch (fault)
            {
                case "unknown-id": output.VisualObservations = [new() { AttachmentId = Guid.NewGuid(), Observation = "Moisture appears visible." }]; break;
                case "too-many": output.VisualObservations = Enumerable.Repeat(output.VisualObservations[0], 6).ToList(); break;
                case "per-image": output.VisualObservations = Enumerable.Repeat(output.VisualObservations[0], 3).ToList(); break;
                case "long": output.VisualObservations = [new() { AttachmentId = attachment.Id, Observation = new string('x', 241) }]; break;
                case "partial": output.VisionStatus = "partial"; break;
                case "no-ack": output.AttachmentIdsUsed = []; break;
                case "payload": output.VisualObservations = [new() { AttachmentId = attachment.Id, Observation = encoded }]; break;
                case "summary-payload": output.ProblemSummary = encoded; break;
                case "metadata-payload": output.AdditionalInformation = new() { ["rawProviderPayload"] = encoded }; break;
                case "provider-payload": response.Metadata.Provider = encoded; break;
                case "failure-payload": response.Success = false; response.ErrorMessage = encoded; break;
                case "limitations": output.VisualLimitations = ["Cannot see.", "Cannot hear.", "Cannot smell.", "Cannot assess."]; break;
            }
        };
        var result = await f.Run();
        Assert.False(result.Success);
        Assert.Empty(f.Db.ProblemAnalyses);
        Assert.Empty(f.Db.AgentMemories);
        Assert.Equal(ServiceRequestStatus.Created, f.Request.Status);
        var persisted = JsonSerializer.Serialize(f.Db.AgentExecutions.Select(e => new { e.Input, e.Output, e.Status }).ToArray());
        Assert.DoesNotContain(f.Sent!.Input.VisualEvidence[0].DataBase64, persisted);
        Assert.DoesNotContain(f.Sent.Input.VisualEvidence[0].DataBase64, result.ErrorMessage!);
        Assert.Equal("Failed", Assert.Single(f.Db.AgentExecutions).Status);
    }

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request);
    }

    /// <summary>
    /// Purpose: Checks that whitespace-only description with attached photos does not call Python agent and returns unsupported vision status requesting more info.
    /// Why: Architectural constraint: images cannot substitute for a problem description text; empty text must request clarification without consuming AI inference tokens.
    /// Scenario: Context has whitespace description and an attached image.
    /// Pass condition: Python client is not called, VisionStatus is "unsupported", and NeedsMoreInformation is true.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.VisualEvidenceTransportTests.EmptyTextCannotClaimImageUnderstandingOrCallPython"
    /// </summary>
    [Fact]
    public async Task EmptyTextCannotClaimImageUnderstandingOrCallPython()
    {
        var client = new FakeProblemUnderstandingClient
        {
            CustomHandler = _ => throw new InvalidOperationException("Should not call Python for empty text.")
        };
        var result = await new ExternalProblemUnderstandingAgentAdapter(client).ExecuteAsync(new AgentContext
        {
            Input = " ",
            VisualEvidence = [new() { AttachmentId = Guid.NewGuid(), Width = 80, Height = 40,
                DataBase64 = Convert.ToBase64String(new byte[64]) }]
        });
        Assert.True(result.Success);
        var output = Assert.IsType<ProblemUnderstandingOutput>(result.Data);
        Assert.Equal("unsupported", output.VisualResult.VisionStatus);
        Assert.Empty(output.VisualResult.VisualObservations);
        Assert.True(output.NeedsMoreInformation);
    }

    /// <summary>
    /// Purpose: Checks that HTTP request payload carries exact ordered image base64, but persisted audit log contains only attachment GUIDs.
    /// Why: High-bandwidth images belong strictly on the internal HTTP wire, while the database audit table must store lightweight metadata only.
    /// Scenario: Sends 0, 1, and 3 attachments to the agent.
    /// Pass condition: Wire payload has correct ContentType, dimensions, and base64; audit log contains only attachment IDs with no base64 and length < 2000 chars.
    /// Note: Parameterized test (Theory). The command below executes all 3 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.VisualEvidenceTransportTests.WireHasExactOrderedNormalizedEvidenceButAuditContainsOnlyBoundedIdentities"
    /// </summary>
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(3)]
    public async Task WireHasExactOrderedNormalizedEvidenceButAuditContainsOnlyBoundedIdentities(int count)
    {
        using var f = new Fixture();
        var attachments = Enumerable.Range(1, count).Reverse().Select(slot => f.Add(slot)).OrderBy(a => a.Slot).ToArray();
        // The persisted snapshot must exist before the HTTP request is sent.
        f.DuringExecution = () => Assert.Single(f.Db.AgentExecutions);
        var result = await f.Run();
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("test-internal-key", f.AuthHeader);
        Assert.Equal(attachments.Select(a => a.Id), f.Sent!.Input.VisualEvidence.Select(a => a.AttachmentId));
        foreach (var image in f.Sent.Input.VisualEvidence)
        {
            var a = attachments.Single(a => a.Id == image.AttachmentId);
            Assert.Equal("image/jpeg", image.ContentType);
            Assert.Equal(a.Width, image.Width); Assert.Equal(a.Height, image.Height);
            Assert.Equal(f.Storage.Files[a.StorageKey], Convert.FromBase64String(image.DataBase64));
            Assert.DoesNotContain(a.StorageKey, f.WireJson!);
        }
        var audit = Assert.Single(f.Db.AgentExecutions).Input!;
        var parsed = JsonSerializer.Deserialize<ProblemUnderstandingInput>(audit)!;
        Assert.Equal(7, parsed.EvidenceRevision);
        Assert.Equal(attachments.Select(a => a.Id), parsed.AttachmentIds);
        Assert.DoesNotContain("dataBase64", audit, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("VisualEvidence", audit);
        Assert.True(audit.Length < 2000);
        foreach (var image in f.Sent.Input.VisualEvidence) Assert.DoesNotContain(image.DataBase64, audit);
        Assert.Equal(7, Assert.Single(f.Db.ProblemAnalyses).EvidenceRevision);
    }

    /// <summary>
    /// Purpose: Checks that corrupted attachment metadata (exceeding individual/aggregate size, >3 count, non-JPEG MIME, dimensions >2048px) aborts before reading disk or sending HTTP.
    /// Why: Fail-fast defense: corrupt database records must never trigger storage reads or network traffic to Python agents.
    /// Scenario: Tests oversize, aggregate size, count > 3, wrong MIME, and oversized width.
    /// Pass condition: Workflow fails, zero HTTP requests sent (Sent is null), no analyses persisted.
    /// Note: Parameterized test (Theory). The command below executes all 5 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.VisualEvidenceTransportTests.CorruptMetadataFailsBeforeReadingOrSending"
    /// </summary>
    [Theory]
    [InlineData("oversize")] [InlineData("aggregate")] [InlineData("count")]
    [InlineData("mime")] [InlineData("dimension")]
    public async Task CorruptMetadataFailsBeforeReadingOrSending(string kind)
    {
        using var f = new Fixture();
        var a = f.Add(1);
        switch (kind)
        {
            case "oversize": a.FileSizeBytes = VisualEvidenceLimits.MaxImageBytes + 1; break;
            case "aggregate":
                a.FileSizeBytes = VisualEvidenceLimits.MaxImageBytes;
                f.Add(2).FileSizeBytes = VisualEvidenceLimits.MaxImageBytes;
                f.Add(3); break;
            case "count": f.Add(2); f.Add(3); f.Add(4); break;
            case "mime": a.ContentType = "image/png"; break;
            case "dimension": a.Width = 2049; break;
        }
        f.Db.SaveChanges();
        var result = await f.Run();
        Assert.False(result.Success);
        Assert.Null(f.Sent);
        Assert.Empty(f.Db.ProblemAnalyses);
        Assert.Equal(ServiceRequestStatus.Created, f.Request.Status);
    }

    /// <summary>
    /// Purpose: Checks that missing files, empty files, size mismatches, or oversized binaries fail cleanly without exposing internal filesystem paths in error messages.
    /// Why: Protects against information disclosure vulnerabilities regarding server file paths.
    /// Scenario: Attachment file is missing from storage, empty (0 bytes), size mismatched (65 bytes), or exceeds max bytes.
    /// Pass condition: Result fails, ErrorMessage does not contain StorageKey path, status recovers to Created, and execution marked Failed.
    /// Note: Parameterized test (Theory). The command below executes all 4 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.VisualEvidenceTransportTests.MissingOrMismatchedBinaryFailsAndRecoversWithoutLeakingPaths"
    /// </summary>
    [Theory]
    [InlineData("missing")] [InlineData("empty")] [InlineData("size-mismatch")] [InlineData("actual-oversize")]
    public async Task MissingOrMismatchedBinaryFailsAndRecoversWithoutLeakingPaths(string kind)
    {
        using var f = new Fixture();
        var a = f.Add(1);
        if (kind == "missing") f.Storage.Files.TryRemove(a.StorageKey, out _);
        else f.Storage.Files[a.StorageKey] = new byte[kind switch
        {
            "empty" => 0, "actual-oversize" => VisualEvidenceLimits.MaxImageBytes + 1, _ => 65
        }];
        var result = await f.Run();
        Assert.False(result.Success);
        Assert.DoesNotContain(a.StorageKey, result.ErrorMessage!);
        Assert.Null(f.Sent);
        Assert.Equal("Failed", Assert.Single(f.Db.AgentExecutions).Status);
        Assert.Equal(ServiceRequestStatus.Created, f.Request.Status);
    }

    /// <summary>
    /// Purpose: Checks that HTTP error responses (422, 503, 401) recover cleanly without echoing private image content in error strings.
    /// Why: Prevents upstream AI service error bodies containing image snippets from leaking into user-visible error messages.
    /// Scenario: Python service returns 422, 503, or 401 with error body containing private image tokens.
    /// Pass condition: Result fails, ErrorMessage does not contain "PRIVATE_IMAGE_CONTENT", and status recovers to Created.
    /// Note: Parameterized test (Theory). The command below executes all 3 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.VisualEvidenceTransportTests.PythonFailureRecoversWithoutEchoingRejectedImageContent"
    /// </summary>
    [Theory]
    [InlineData(422)] [InlineData(503)] [InlineData(401)]
    public async Task PythonFailureRecoversWithoutEchoingRejectedImageContent(int status)
    {
        using var f = new Fixture();
        f.Add(1);
        f.ResponseStatus = (HttpStatusCode)status;
        f.ErrorBody = "dataBase64: PRIVATE_IMAGE_CONTENT";
        var result = await f.Run();
        Assert.False(result.Success);
        Assert.DoesNotContain("PRIVATE_IMAGE_CONTENT", result.ErrorMessage!);
        Assert.Equal(ServiceRequestStatus.Created, f.Request.Status);
        Assert.Empty(f.Db.ProblemAnalyses);
    }

    /// <summary>
    /// Purpose: Checks that network failure connecting to Python agent recovers cleanly without attempting an unauthorized in-process fallback.
    /// Why: Ensures failures in external microservices are handled deterministically without silent degradation.
    /// Scenario: HttpClient throws HttpRequestException ("unavailable").
    /// Pass condition: Result fails, no analyses saved, status recovers to Created.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.VisualEvidenceTransportTests.UnavailablePythonRecoversWithoutNativeFallback"
    /// </summary>
    [Fact]
    public async Task UnavailablePythonRecoversWithoutNativeFallback()
    {
        using var f = new Fixture();
        f.Add(1); f.NetworkFailure = true;
        var result = await f.Run();
        Assert.False(result.Success);
        Assert.Empty(f.Db.ProblemAnalyses);
        Assert.Equal(ServiceRequestStatus.Created, f.Request.Status);
    }

    /// <summary>
    /// Purpose: Checks that if evidence revision changes during agent execution, the result is rejected with ConflictException.
    /// Why: Concurrency protection: guarantees analysis based on old photos is discarded if a customer updates their photos mid-flight.
    /// Scenario: EvidenceRevision is incremented during agent execution callback.
    /// Pass condition: Throws ConflictException, zero analyses or memories saved, and execution status is Failed.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.VisualEvidenceTransportTests.ChangedRevisionCannotBecomeCurrentOrEnterSemanticMemory"
    /// </summary>
    [Fact]
    public async Task ChangedRevisionCannotBecomeCurrentOrEnterSemanticMemory()
    {
        using var f = new Fixture();
        f.Add(1);
        f.DuringExecution = () => { f.Request.EvidenceRevision++; f.Db.SaveChanges(); };
        await Assert.ThrowsAsync<ConflictException>(() => f.Run());
        Assert.Empty(f.Db.ProblemAnalyses);
        Assert.Empty(f.Db.AgentMemories);
        Assert.Equal("Failed", Assert.Single(f.Db.AgentExecutions).Status);
        Assert.Equal(ServiceRequestStatus.Created, f.Request.Status);
    }

    /// <summary>
    /// Purpose: Checks that concurrent revision change from a separate DbContext cleanly recovers using fresh state.
    /// Why: Verifies that separate HTTP requests updating photos concurrently trigger conflict rejection and leave database in a clean state.
    /// Scenario: Second DbContext increments EvidenceRevision concurrently.
    /// Pass condition: Throws ConflictException, database verifies updated revision 8, and status remains Created with zero memories.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.VisualEvidenceTransportTests.ConcurrentRevisionChangeRecoversUsingFreshState"
    /// </summary>
    [Fact]
    public async Task ConcurrentRevisionChangeRecoversUsingFreshState()
    {
        using var f = new Fixture();
        f.Add(1);
        var options = (DbContextOptions<AssistLKDbContext>)f.Db.GetService<IDbContextOptions>();
        f.DuringExecution = () =>
        {
            using var other = new AssistLKDbContext(options);
            var changed = other.ServiceRequests.Single();
            changed.EvidenceRevision++;
            other.SaveChanges();
        };
        await Assert.ThrowsAsync<ConflictException>(() => f.Run());
        using var verify = new AssistLKDbContext(options);
        var current = await verify.ServiceRequests.SingleAsync();
        Assert.Equal(8, current.EvidenceRevision);
        Assert.Equal(ServiceRequestStatus.Created, current.Status);
        Assert.Empty(verify.ProblemAnalyses);
        Assert.Empty(verify.AgentMemories);
    }

    /// <summary>
    /// Purpose: Checks that VisualEvidenceLimits enforces individual (2MB) and aggregate (4MB) decoded byte caps.
    /// Why: Guarantees memory safety at the HTTP boundary before serialization.
    /// Scenario: Validates combinations of image payload sizes.
    /// Pass condition: Accepted payloads pass validation; over-limit payloads throw ArgumentException.
    /// Note: Parameterized test (Theory). The command below executes all 3 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.VisualEvidenceTransportTests.HttpBoundaryEnforcesDecodedAndAggregateLimits"
    /// </summary>
    [Theory]
    [InlineData(2097152, 2097152, true)]
    [InlineData(2097152, 2097153, false)]
    [InlineData(2097153, 0, false)]
    public void HttpBoundaryEnforcesDecodedAndAggregateLimits(int first, int second, bool accepted)
    {
        VisualEvidencePayloadDto Image(int size) => new()
        {
            AttachmentId = Guid.NewGuid(), Width = 80, Height = 40,
            DataBase64 = Convert.ToBase64String(new byte[size])
        };
        var images = new List<VisualEvidencePayloadDto> { Image(first) };
        if (second > 0) images.Add(Image(second));
        if (accepted) VisualEvidenceLimits.Validate(images);
        else Assert.Throws<ArgumentException>(() => VisualEvidenceLimits.Validate(images));
    }

    /// <summary>
    /// Purpose: Checks that 3 individually valid images exceeding the 4MB aggregate limit are rejected.
    /// Why: Prevents multi-image aggregate denial of service.
    /// Scenario: Two 2MB images plus one 1-byte image (4MB + 1 byte total).
    /// Pass condition: Throws ArgumentException.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.VisualEvidenceTransportTests.HttpBoundaryRejectsAggregateOfIndividuallyValidImages"
    /// </summary>
    [Fact]
    public void HttpBoundaryRejectsAggregateOfIndividuallyValidImages()
    {
        var images = Enumerable.Range(1, 3).Select(i => new VisualEvidencePayloadDto
        {
            AttachmentId = Guid.NewGuid(), Width = 80, Height = 40,
            DataBase64 = Convert.ToBase64String(new byte[i == 3 ? 1 : VisualEvidenceLimits.MaxImageBytes])
        }).ToArray();
        Assert.Throws<ArgumentException>(() => VisualEvidenceLimits.Validate(images));
    }

    /// <summary>
    /// Purpose: Checks that AnalysisEvidenceRepository enforces customer ID ownership when fetching photos.
    /// Why: Multi-tenancy security: customer A must never load customer B's photos into evidence snapshots.
    /// Scenario: Queries photos for Request.Id using a foreign CustomerId.
    /// Pass condition: Returns null.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.VisualEvidenceTransportTests.EvidenceRepositoryDoesNotReturnAnotherCustomersPhotos"
    /// </summary>
    [Fact]
    public async Task EvidenceRepositoryDoesNotReturnAnotherCustomersPhotos()
    {
        using var f = new Fixture();
        f.Add(1);
        Assert.Null(await new AnalysisEvidenceRepository(f.Db).GetAsync(f.Request.Id, Guid.NewGuid(), default));
    }

    /// <summary>
    /// Purpose: Checks that a real normalized ImageMagick JPEG matches byte-for-byte on the wire and is completely omitted from audits and memories.
    /// Why: End-to-end confirmation that real binary image bytes reach the agent wire accurately while never touching audit or memory storage.
    /// Scenario: Normalizes a synthetic PNG into JPEG, attaches it, and runs workflow.
    /// Pass condition: Wire payload decodes to exact JPEG bytes, while persisted execution inputs/outputs and memories do not contain the base64 string.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.VisualEvidenceTransportTests.RealNormalizedJpegIsExactOnWireAndAbsentFromEveryPersistedAuditAndMemory"
    /// </summary>
    [Fact]
    public async Task RealNormalizedJpegIsExactOnWireAndAbsentFromEveryPersistedAuditAndMemory()
    {
        using var f = new Fixture();
        var a = f.Add(1);
        using var source = new ImageMagick.MagickImage(ImageMagick.MagickColors.Blue, 80, 40);
        var normalized = await new AssistLK.Infrastructure.Attachments.AttachmentImageNormalizer().NormalizeAsync(
            new MemoryStream(source.ToByteArray(ImageMagick.MagickFormat.Png)), "synthetic.png", "image/png");
        f.Storage.Files[a.StorageKey] = normalized.Content;
        a.FileSizeBytes = normalized.Content.Length;
        f.Db.SaveChanges();
        var result = await f.Run();
        Assert.True(result.Success);
        var encoded = Assert.Single(f.Sent!.Input.VisualEvidence).DataBase64;
        Assert.Equal(normalized.Content, Convert.FromBase64String(encoded));
        var persisted = JsonSerializer.Serialize(new
        {
            Inputs = f.Db.AgentExecutions.Select(e => e.Input).ToArray(),
            Outputs = f.Db.AgentExecutions.Select(e => e.Output).ToArray(),
            Memory = f.Db.AgentMemories.Select(m => m.Value).ToArray()
        });
        Assert.DoesNotContain(encoded, persisted);
        Assert.DoesNotContain("dataBase64", persisted);
    }

    /// <summary>
    /// Purpose: Checks that [JsonIgnore] on AgentContext.VisualEvidence prevents accidental serialization of transient images.
    /// Why: Defense-in-depth: if an engineer serializes AgentContext, image data must not be written out.
    /// Scenario: Serializes AgentContext with attached visual evidence.
    /// Pass condition: Serialized JSON does not contain "PRIVATE_IMAGE_DATA" or "VisualEvidence".
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.VisualEvidenceTransportTests.AccidentalContextSerializationCannotIncludeTransientImages"
    /// </summary>
    [Fact]
    public void AccidentalContextSerializationCannotIncludeTransientImages()
    {
        var context = new AgentContext
        {
            VisualEvidence = [new() { AttachmentId = Guid.NewGuid(), Width = 1, Height = 1, DataBase64 = "PRIVATE_IMAGE_DATA" }]
        };
        var serialized = JsonSerializer.Serialize(context);
        Assert.DoesNotContain("PRIVATE_IMAGE_DATA", serialized);
        Assert.DoesNotContain("VisualEvidence", serialized);
    }

    /// <summary>
    /// Purpose: Checks that cancelling CancellationToken throws OperationCanceledException instead of being swallowed as a storage error.
    /// Why: Proper task cancellation propagation across async storage operations.
    /// Scenario: Calls Evidence.LoadAsync with a pre-cancelled token.
    /// Pass condition: Throws OperationCanceledException.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.VisualEvidenceTransportTests.CancellationIsNotConvertedToStorageFailure"
    /// </summary>
    [Fact]
    public async Task CancellationIsNotConvertedToStorageFailure()
    {
        using var f = new Fixture();
        f.Add(1);
        f.Request.Status = ServiceRequestStatus.Analyzing; f.Db.SaveChanges();
        var snapshot = await f.Evidence.CaptureAsync(f.Request.Id, f.Request.CustomerId, 7, default);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Evidence.LoadAsync(snapshot, cancellation.Token));
    }

    /// <summary>
    /// Purpose: Checks that evidence snapshot queries read fresh database values even if the request entity is already tracked by EF Core.
    /// Why: Ensures snapshot generation is not blinded by stale in-memory entity cache.
    /// Scenario: Modifies EvidenceRevision in another context, queries snapshot via repository.
    /// Pass condition: Snapshot Revision reflects updated database value (8) rather than tracked value (7).
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.VisualEvidenceTransportTests.FreshSnapshotSeesChangesDespiteTrackedRequest"
    /// </summary>
    [Fact]
    public async Task FreshSnapshotSeesChangesDespiteTrackedRequest()
    {
        using var f = new Fixture();
        var options = f.Db.GetService<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptions>();
        using var other = new AssistLKDbContext((DbContextOptions<AssistLKDbContext>)options);
        var request = await other.ServiceRequests.SingleAsync();
        request.EvidenceRevision++;
        await other.SaveChangesAsync();
        Assert.Equal(7, f.Request.EvidenceRevision);
        var snapshot = await new AnalysisEvidenceRepository(f.Db).GetAsync(f.Request.Id, f.Request.CustomerId, default);
        Assert.Equal(8, snapshot!.Revision);
    }

    private sealed class ThrowingStorage : IServiceRequestAttachmentStorage
    {
        public Task<Stream> OpenReadAsync(string key, CancellationToken ct = default) => throw new IOException("PRIVATE/path/photo.jpg");
        public Task WriteAsync(string key, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
        public IReadOnlyList<StoredAttachmentFile> GetCleanupCandidates(DateTime before) => [];
    }

    /// <summary>
    /// Purpose: Checks that storage I/O exceptions are sanitized so internal file paths are stripped and not exposed via InnerException.
    /// Why: Prevents local server directory structures from leaking through unhandled exception handlers.
    /// Scenario: ThrowingStorage throws IOException with path "PRIVATE/path/photo.jpg".
    /// Pass condition: Throws InvalidOperationException with null InnerException and no "PRIVATE" string.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.VisualEvidenceTransportTests.StorageExceptionIsSanitizedWithoutInnerException"
    /// </summary>
    [Fact]
    public async Task StorageExceptionIsSanitizedWithoutInnerException()
    {
        using var f = new Fixture();
        var a = f.Add(1);
        f.Request.Status = ServiceRequestStatus.Analyzing; f.Db.SaveChanges();
        var service = new ProblemVisualEvidenceService(new AnalysisEvidenceRepository(f.Db), new ThrowingStorage());
        var snapshot = await service.CaptureAsync(f.Request.Id, f.Request.CustomerId, 7, default);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.LoadAsync(snapshot, default));
        Assert.Null(error.InnerException);
        Assert.DoesNotContain("PRIVATE", error.ToString());
    }
}
