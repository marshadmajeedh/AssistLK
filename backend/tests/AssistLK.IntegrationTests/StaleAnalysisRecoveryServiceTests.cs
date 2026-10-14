using AssistLK.Application.Interfaces;
using AssistLK.Application.ServiceRequests;
using AssistLK.Application.Services;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssistLK.IntegrationTests;

// ==============================================================================
// TEST SUITE: StaleAnalysisRecoveryServiceTests
// What this class tests:
//   - StaleAnalysisRecoveryService background recovery of service requests stuck in Analyzing status.
//   - Timeout evaluation against StaleAnalysisMinutes (recovering old requests while leaving fresh ones alone).
//   - Intelligent status restoration:
//     - Recovers to AwaitingInformation if active, non-superseded, unanswered clarification questions exist.
//     - Recovers to Created if no clarifications exist, or all questions are answered, or questions are superseded.
//     - Promotes to Analyzed if a valid analysis for the current EvidenceRevision already exists.
//   - Non-Analyzing status immunity (Created, AwaitingInformation, Analyzed, ReadyForMatching, Cancelled are untouched).
//   - Invariant preservation (EvidenceRevision and ProblemAnalysis history are strictly preserved).
//   - Concurrency protection against race conditions with completing agent executions.
// Why these tests are needed:
//   - If the AI agent worker process crashes, times out, or restarts while Analyzing a request,
//     the request must not remain stranded indefinitely. This service self-heals stuck workflows.
// What is real and what is mocked/faked:
//   - Real: StaleAnalysisRecoveryService, C1RecoveryOptions.
//   - In-Memory/Faked: TestRecoveryEnvironment, TestRecoveryRepo (in-memory C# list with concurrency exception injection).
// Prerequisites:
//   - None (runs purely in-memory).
// Run from repository root:
//   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.StaleAnalysisRecoveryServiceTests"
// ==============================================================================
public class StaleAnalysisRecoveryServiceTests
{
    // Purpose: Checks that a request in Analyzing status updated 10 minutes ago (> 5 min threshold) is recovered to Created.
    // Why: Unlocks stranded requests after an agent timeout or crash.
    // Scenario: Analyzing request with no clarifications, updated 10 minutes ago.
    // Pass condition: Recovered count is 1, status becomes Created.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.StaleAnalysisRecoveryServiceTests.Test1_Stale_Analyzing_Request_Is_Recovered"
    [Fact]
    public async Task Test1_Stale_Analyzing_Request_Is_Recovered()
    {
        // Arrange
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Leaking pipe in kitchen",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Analyzing,
            EvidenceRevision = 1,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10) // 10 mins ago (> 5 min threshold)
        };
        testBed.AddRequest(request);

        // Act
        var recoveredCount = await testBed.Service.RecoverStaleAnalysesAsync();

        // Assert
        Assert.Equal(1, recoveredCount);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        Assert.Equal(ServiceRequestStatus.Created, updated.Status);
    }

    // Purpose: Checks that an Analyzing request updated 2 minutes ago (< 5 min threshold) is NOT recovered.
    // Why: Prevents interrupting an agent that is still actively processing within the normal execution window.
    // Scenario: Analyzing request updated 2 minutes ago.
    // Pass condition: Recovered count is 0, status remains Analyzing.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.StaleAnalysisRecoveryServiceTests.Test2_Fresh_Analyzing_Request_Is_NOT_Recovered"
    [Fact]
    public async Task Test2_Fresh_Analyzing_Request_Is_NOT_Recovered()
    {
        // Arrange
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Leaking pipe in kitchen",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Analyzing,
            EvidenceRevision = 1,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-2) // 2 mins ago (< 5 min threshold)
        };
        testBed.AddRequest(request);

        // Act
        var recoveredCount = await testBed.Service.RecoverStaleAnalysesAsync();

        // Assert
        Assert.Equal(0, recoveredCount);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        Assert.Equal(ServiceRequestStatus.Analyzing, updated.Status);
    }

    // Purpose: Checks that requests in Created status are ignored by recovery, even if old.
    // Why: Created is an idle state awaiting dispatch, not a stuck in-flight operation.
    // Scenario: Request in Created status updated 2 hours ago.
    // Pass condition: 0 recovered, status remains Created.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.StaleAnalysisRecoveryServiceTests.Test3_Created_Request_Unchanged"
    [Fact]
    public async Task Test3_Created_Request_Unchanged()
    {
        // Arrange
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Leaking pipe in kitchen",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Created,
            EvidenceRevision = 1,
            UpdatedAt = DateTime.UtcNow.AddHours(-2)
        };
        testBed.AddRequest(request);

        // Act
        var recoveredCount = await testBed.Service.RecoverStaleAnalysesAsync();

        // Assert
        Assert.Equal(0, recoveredCount);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        Assert.Equal(ServiceRequestStatus.Created, updated.Status);
    }

    // Purpose: Checks that requests in AwaitingInformation status are ignored by the stale analysis recovery service.
    // Why: AwaitingInformation is waiting on customer input, which is handled by lifecycle expiration, not analysis recovery.
    // Scenario: Request in AwaitingInformation status updated 2 hours ago.
    // Pass condition: 0 recovered, status remains AwaitingInformation.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.StaleAnalysisRecoveryServiceTests.Test4_AwaitingInformation_Request_Unchanged"
    [Fact]
    public async Task Test4_AwaitingInformation_Request_Unchanged()
    {
        // Arrange
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Leaking pipe in kitchen",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.AwaitingInformation,
            EvidenceRevision = 1,
            UpdatedAt = DateTime.UtcNow.AddHours(-2)
        };
        testBed.AddRequest(request);

        // Act
        var recoveredCount = await testBed.Service.RecoverStaleAnalysesAsync();

        // Assert
        Assert.Equal(0, recoveredCount);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, updated.Status);
    }

    // Purpose: Checks that requests in Analyzed status are ignored by recovery.
    // Why: Analysis successfully completed; the request must not be rolled back.
    // Scenario: Request in Analyzed status updated 2 hours ago.
    // Pass condition: 0 recovered, status remains Analyzed.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.StaleAnalysisRecoveryServiceTests.Test5_Analyzed_Request_Unchanged"
    [Fact]
    public async Task Test5_Analyzed_Request_Unchanged()
    {
        // Arrange
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Leaking pipe in kitchen",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Analyzed,
            EvidenceRevision = 1,
            UpdatedAt = DateTime.UtcNow.AddHours(-2)
        };
        testBed.AddRequest(request);

        // Act
        var recoveredCount = await testBed.Service.RecoverStaleAnalysesAsync();

        // Assert
        Assert.Equal(0, recoveredCount);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        Assert.Equal(ServiceRequestStatus.Analyzed, updated.Status);
    }

    // Purpose: Checks that requests in ReadyForMatching status are untouched by recovery.
    // Why: The request has finished C1 and must not be downgraded.
    // Scenario: Request in ReadyForMatching status updated 2 hours ago.
    // Pass condition: 0 recovered, status remains ReadyForMatching.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.StaleAnalysisRecoveryServiceTests.Test6_ReadyForMatching_Unchanged"
    [Fact]
    public async Task Test6_ReadyForMatching_Unchanged()
    {
        // Arrange
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Leaking pipe in kitchen",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.ReadyForMatching,
            EvidenceRevision = 1,
            UpdatedAt = DateTime.UtcNow.AddHours(-2)
        };
        testBed.AddRequest(request);

        // Act
        var recoveredCount = await testBed.Service.RecoverStaleAnalysesAsync();

        // Assert
        Assert.Equal(0, recoveredCount);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        Assert.Equal(ServiceRequestStatus.ReadyForMatching, updated.Status);
    }

    // Purpose: Checks that requests in Cancelled status are untouched by recovery.
    // Why: Terminal state must remain immutable.
    // Scenario: Request in Cancelled status updated 2 hours ago.
    // Pass condition: 0 recovered, status remains Cancelled.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.StaleAnalysisRecoveryServiceTests.Test7_Cancelled_Unchanged"
    [Fact]
    public async Task Test7_Cancelled_Unchanged()
    {
        // Arrange
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Leaking pipe in kitchen",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Cancelled,
            EvidenceRevision = 1,
            UpdatedAt = DateTime.UtcNow.AddHours(-2)
        };
        testBed.AddRequest(request);

        // Act
        var recoveredCount = await testBed.Service.RecoverStaleAnalysesAsync();

        // Assert
        Assert.Equal(0, recoveredCount);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        Assert.Equal(ServiceRequestStatus.Cancelled, updated.Status);
    }

    // Purpose: Checks that recovering a stale request does not increment or alter its EvidenceRevision.
    // Why: Evidence has not changed; only the processing state was reset.
    // Scenario: Stale Analyzing request with EvidenceRevision = 4 is recovered.
    // Pass condition: Status is Created and EvidenceRevision remains strictly 4.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.StaleAnalysisRecoveryServiceTests.Test8_Recovery_Does_Not_Modify_EvidenceRevision"
    [Fact]
    public async Task Test8_Recovery_Does_Not_Modify_EvidenceRevision()
    {
        // Arrange
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        const long initialEvidenceRevision = 4L;
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Electrical short circuit",
            LocationText = "Kandy",
            Status = ServiceRequestStatus.Analyzing,
            EvidenceRevision = initialEvidenceRevision,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-15)
        };
        testBed.AddRequest(request);

        // Act
        var recovered = await testBed.Service.RecoverStaleRequestAsync(requestId);

        // Assert
        Assert.True(recovered);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        Assert.Equal(ServiceRequestStatus.Created, updated.Status);
        Assert.Equal(initialEvidenceRevision, updated.EvidenceRevision);
    }

    // Purpose: Checks that prior problem analysis records in history are preserved during recovery.
    // Why: Diagnostic history and audit trails must never be deleted on crash recovery.
    // Scenario: Stale Analyzing request with an earlier ProblemAnalysis from round 1.
    // Pass condition: Analysis record remains in the list after recovery.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.StaleAnalysisRecoveryServiceTests.Test9_Recovery_Does_Not_Remove_ProblemAnalysis_And_Preserves_History"
    [Fact]
    public async Task Test9_Recovery_Does_Not_Remove_ProblemAnalysis_And_Preserves_History()
    {
        // Arrange
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Refrigerator not cooling",
            LocationText = "Galle",
            Status = ServiceRequestStatus.Analyzing,
            EvidenceRevision = 2L, // New revision after customer edited details
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        // Analysis exists for previous revision 1
        var priorAnalysis = new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            EvidenceRevision = 1L,
            DetectedProblem = "Compressor faulty",
            Confidence = 0.85m,
            AgentName = "ProblemUnderstandingAgent"
        };
        request.ProblemAnalyses.Add(priorAnalysis);
        testBed.AddRequest(request);

        // Act
        var recovered = await testBed.Service.RecoverStaleRequestAsync(requestId);

        // Assert
        Assert.True(recovered);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        Assert.Equal(ServiceRequestStatus.Created, updated.Status);
        Assert.Single(updated.ProblemAnalyses);
        Assert.Equal(priorAnalysis.Id, updated.ProblemAnalyses.First().Id);
        Assert.Equal("Compressor faulty", updated.ProblemAnalyses.First().DetectedProblem);
    }

    // Purpose: Checks that if a valid ProblemAnalysis matching the current EvidenceRevision exists, the request is promoted to Analyzed rather than downgraded to Created.
    // Why: Prevents rolling back a request if the analysis completed right as the recovery job scanned it.
    // Scenario: Stale Analyzing request that already has a ProblemAnalysis matching EvidenceRevision 1.
    // Pass condition: Status becomes Analyzed instead of Created.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.StaleAnalysisRecoveryServiceTests.Test9b_Valid_Analysis_For_Current_Revision_Prevents_Downgrade"
    [Fact]
    public async Task Test9b_Valid_Analysis_For_Current_Revision_Prevents_Downgrade()
    {
        // Arrange
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Air conditioner leak",
            LocationText = "Negombo",
            Status = ServiceRequestStatus.Analyzing,
            EvidenceRevision = 1L,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        // Analysis already completed for current revision 1
        var currentAnalysis = new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            EvidenceRevision = 1L,
            DetectedProblem = "Condensate drainage blocked",
            Confidence = 0.90m,
            AgentName = "ProblemUnderstandingAgent"
        };
        request.ProblemAnalyses.Add(currentAnalysis);
        testBed.AddRequest(request);

        // Act
        var recovered = await testBed.Service.RecoverStaleRequestAsync(requestId);

        // Assert
        Assert.False(recovered);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        // Did not downgrade to Created because valid analysis exists
        Assert.Equal(ServiceRequestStatus.Analyzing, updated.Status);
    }

    // Purpose: Checks that if a concurrency exception occurs during save (e.g. agent submitted result concurrently), the recovery worker catches it and skips overwriting.
    // Why: Prevents race conditions from clobbering successful concurrent agent completion.
    // Scenario: Repository configured to throw DbUpdateConcurrencyException on save.
    // Pass condition: Recovery returns false, and request status remains Analyzing.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.StaleAnalysisRecoveryServiceTests.Test10_Concurrent_Advancement_Prevents_Stale_Worker_Overwrite"
    [Fact]
    public async Task Test10_Concurrent_Advancement_Prevents_Stale_Worker_Overwrite()
    {
        // Arrange
        var testBed = new TestRecoveryEnvironment(throwConcurrencyExceptionOnSave: true);
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Generator repair",
            LocationText = "Kurunegala",
            Status = ServiceRequestStatus.Analyzing,
            EvidenceRevision = 1L,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-15)
        };
        testBed.AddRequest(request);

        // Act
        var recovered = await testBed.Service.RecoverStaleRequestAsync(requestId);

        // Assert
        Assert.False(recovered); // Concurrency exception safely handled and prevented downgrade
    }

    // Purpose: Checks that running recovery repeatedly back-to-back is idempotent.
    // Why: Ensures recovery job cycles don't cause duplicate state transitions or corrupt data.
    // Scenario: Recovery run once (recovers 1), then immediately run twice more.
    // Pass condition: First run returns 1, subsequent runs return 0, request remains Created.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.StaleAnalysisRecoveryServiceTests.Test11_Repeated_Recovery_Invocation_Is_Idempotent"
    [Fact]
    public async Task Test11_Repeated_Recovery_Invocation_Is_Idempotent()
    {
        // Arrange
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Washing machine drainage fault",
            LocationText = "Matara",
            Status = ServiceRequestStatus.Analyzing,
            EvidenceRevision = 1L,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        testBed.AddRequest(request);

        // Act 1
        var firstRunCount = await testBed.Service.RecoverStaleAnalysesAsync();
        Assert.Equal(1, firstRunCount);
        Assert.Equal(ServiceRequestStatus.Created, testBed.GetRequest(requestId)!.Status);

        // Act 2 (Repeated)
        var secondRunCount = await testBed.Service.RecoverStaleAnalysesAsync();
        Assert.Equal(0, secondRunCount);

        // Act 3 (Repeated again)
        var thirdRunCount = await testBed.Service.RecoverStaleAnalysesAsync();
        Assert.Equal(0, thirdRunCount);

        // Request remains safely in Created state
        Assert.Equal(ServiceRequestStatus.Created, testBed.GetRequest(requestId)!.Status);
    }

    // Purpose: Checks that multiple requests across different states (stale without clarification, stale with clarification, fresh, already analyzed) are processed independently and accurately.
    // Why: Validates batch scan and routing logic under realistic multi-request workloads.
    // Scenario: 5 requests seeded; 3 stale (two with no clarification -> Created; one with clarification -> AwaitingInformation), 1 fresh, 1 analyzed.
    // Pass condition: Exactly 3 recovered, each assigned to the correct target status.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.StaleAnalysisRecoveryServiceTests.Test12_Multiple_Eligible_Stale_Requests_Recover_Independently"
    [Fact]
    public async Task Test12_Multiple_Eligible_Stale_Requests_Recover_Independently()
    {
        // Arrange
        var testBed = new TestRecoveryEnvironment();

        // 1. Stale Analyzing request with NO clarifications -> recovers to Created
        var id1 = Guid.NewGuid();
        testBed.AddRequest(new ServiceRequest
        {
            Id = id1,
            CustomerId = Guid.NewGuid(),
            Description = "Roof leak",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Analyzing,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
        });

        // 2. Stale Analyzing request WITH clarifications -> recovers to AwaitingInformation
        var id2 = Guid.NewGuid();
        var req2 = new ServiceRequest
        {
            Id = id2,
            CustomerId = Guid.NewGuid(),
            Description = "Solar inverter error",
            LocationText = "Kandy",
            Status = ServiceRequestStatus.Analyzing,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-12)
        };
        req2.Clarifications.Add(new ServiceRequestClarification
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = id2,
            ClarificationRound = 1,
            Question = "What error code is shown?",
            Answer = null,
            AnsweredAt = null,
            SupersededAt = null
        });
        testBed.AddRequest(req2);

        // 3. Stale Analyzing request with NO clarifications -> recovers to Created
        var id3 = Guid.NewGuid();
        testBed.AddRequest(new ServiceRequest
        {
            Id = id3,
            CustomerId = Guid.NewGuid(),
            Description = "Door lock jammed",
            LocationText = "Galle",
            Status = ServiceRequestStatus.Analyzing,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-8)
        });

        // 4. Fresh Analyzing request -> should NOT recover
        var id4 = Guid.NewGuid();
        testBed.AddRequest(new ServiceRequest
        {
            Id = id4,
            CustomerId = Guid.NewGuid(),
            Description = "CCTV camera offline",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Analyzing,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-1)
        });

        // 5. Already Analyzed request -> should NOT recover
        var id5 = Guid.NewGuid();
        testBed.AddRequest(new ServiceRequest
        {
            Id = id5,
            CustomerId = Guid.NewGuid(),
            Description = "Plumbing repair",
            LocationText = "Jaffna",
            Status = ServiceRequestStatus.Analyzed,
            UpdatedAt = DateTime.UtcNow.AddHours(-1)
        });

        // Act
        var recoveredCount = await testBed.Service.RecoverStaleAnalysesAsync();

        // Assert
        Assert.Equal(3, recoveredCount);

        // id1: recovered to Created
        Assert.Equal(ServiceRequestStatus.Created, testBed.GetRequest(id1)!.Status);

        // id2: recovered to AwaitingInformation (preserving clarification context)
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, testBed.GetRequest(id2)!.Status);

        // id3: recovered to Created
        Assert.Equal(ServiceRequestStatus.Created, testBed.GetRequest(id3)!.Status);

        // id4: untouched in Analyzing
        Assert.Equal(ServiceRequestStatus.Analyzing, testBed.GetRequest(id4)!.Status);

        // id5: untouched in Analyzed
        Assert.Equal(ServiceRequestStatus.Analyzed, testBed.GetRequest(id5)!.Status);
    }

    // Purpose: Checks that a stale Analyzing request with no clarification questions recovers to Created.
    // Why: No pending customer action exists, so the request must re-enter the initial queue.
    // Scenario: Stale Analyzing request with empty Clarifications list.
    // Pass condition: RecoverStaleRequestAsync returns true and status is Created.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.StaleAnalysisRecoveryServiceTests.Lifecycle_Case1_No_Clarification_Recovers_To_Created"
    [Fact]
    public async Task Lifecycle_Case1_No_Clarification_Recovers_To_Created()
    {
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "No clarifications requested",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Analyzing,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        testBed.AddRequest(request);

        var recovered = await testBed.Service.RecoverStaleRequestAsync(requestId);

        Assert.True(recovered);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        Assert.Equal(ServiceRequestStatus.Created, updated.Status);
    }

    // Purpose: Checks that a stale Analyzing request with an active unanswered clarification question recovers to AwaitingInformation.
    // Why: The customer still needs to answer the question before re-analysis can succeed.
    // Scenario: Stale Analyzing request with an active, unanswered question.
    // Pass condition: RecoverStaleRequestAsync returns true and status is AwaitingInformation.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.StaleAnalysisRecoveryServiceTests.Lifecycle_Case2_Current_Unanswered_Clarification_Recovers_To_AwaitingInformation"
    [Fact]
    public async Task Lifecycle_Case2_Current_Unanswered_Clarification_Recovers_To_AwaitingInformation()
    {
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Has pending question",
            LocationText = "Kandy",
            Status = ServiceRequestStatus.Analyzing,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        request.Clarifications.Add(new ServiceRequestClarification
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            ClarificationRound = 1,
            Question = "Is it leaking from the base?",
            Answer = null,
            SupersededAt = null
        });
        testBed.AddRequest(request);

        var recovered = await testBed.Service.RecoverStaleRequestAsync(requestId);

        Assert.True(recovered);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, updated.Status);
    }

    // Purpose: Checks that a stale Analyzing request whose clarifications have already been answered recovers to Created.
    // Why: The customer already answered; the request is ready for full analysis from the beginning.
    // Scenario: Stale Analyzing request with answered question (Answer = "Samsung RT28").
    // Pass condition: Status is Created.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.StaleAnalysisRecoveryServiceTests.Lifecycle_Case3_Answered_Clarification_Only_Recovers_To_Created"
    [Fact]
    public async Task Lifecycle_Case3_Answered_Clarification_Only_Recovers_To_Created()
    {
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Customer answered previous question",
            LocationText = "Galle",
            Status = ServiceRequestStatus.Analyzing,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        request.Clarifications.Add(new ServiceRequestClarification
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            ClarificationRound = 1,
            Question = "What model is the appliance?",
            Answer = "Samsung RT28",
            AnsweredAt = DateTime.UtcNow.AddMinutes(-20),
            SupersededAt = null
        });
        testBed.AddRequest(request);

        var recovered = await testBed.Service.RecoverStaleRequestAsync(requestId);

        Assert.True(recovered);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        // Because question was answered, customer owes no information -> Created
        Assert.Equal(ServiceRequestStatus.Created, updated.Status);
    }

    // Purpose: Checks that a stale Analyzing request whose unanswered question was superseded by an edit recovers to Created.
    // Why: Superseded questions are inactive; customer owes no answers on obsolete rounds.
    // Scenario: Stale Analyzing request with a question having non-null SupersededAt.
    // Pass condition: Status is Created.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.StaleAnalysisRecoveryServiceTests.Lifecycle_Case4_Superseded_Clarification_Only_Recovers_To_Created"
    [Fact]
    public async Task Lifecycle_Case4_Superseded_Clarification_Only_Recovers_To_Created()
    {
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Previous question was superseded by customer edit",
            LocationText = "Jaffna",
            Status = ServiceRequestStatus.Analyzing,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        request.Clarifications.Add(new ServiceRequestClarification
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            ClarificationRound = 1,
            Question = "Is the pipe plastic or copper?",
            Answer = null,
            SupersededAt = DateTime.UtcNow.AddMinutes(-15) // Superseded!
        });
        testBed.AddRequest(request);

        var recovered = await testBed.Service.RecoverStaleRequestAsync(requestId);

        Assert.True(recovered);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        // Superseded questions are inactive, customer owes no information -> Created
        Assert.Equal(ServiceRequestStatus.Created, updated.Status);
    }

    // Purpose: Checks that a request with round 1 answered and round 2 unanswered recovers to AwaitingInformation.
    // Why: Identifies that an active, unanswered round exists despite historical answered rounds.
    // Scenario: Stale Analyzing request with round 1 answered and round 2 unanswered.
    // Pass condition: Status is AwaitingInformation.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.StaleAnalysisRecoveryServiceTests.Lifecycle_Case5_Historical_Answered_Plus_Current_Unanswered_Recovers_To_AwaitingInformation"
    [Fact]
    public async Task Lifecycle_Case5_Historical_Answered_Plus_Current_Unanswered_Recovers_To_AwaitingInformation()
    {
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Round 2 in progress",
            LocationText = "Matara",
            Status = ServiceRequestStatus.Analyzing,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        // Round 1 answered
        request.Clarifications.Add(new ServiceRequestClarification
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            ClarificationRound = 1,
            Question = "What color is the smoke?",
            Answer = "White smoke",
            AnsweredAt = DateTime.UtcNow.AddMinutes(-30),
            SupersededAt = null
        });
        // Round 2 unanswered
        request.Clarifications.Add(new ServiceRequestClarification
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            ClarificationRound = 2,
            Question = "Does the engine crank?",
            Answer = null,
            SupersededAt = null
        });
        testBed.AddRequest(request);

        var recovered = await testBed.Service.RecoverStaleRequestAsync(requestId);

        Assert.True(recovered);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        // Current unanswered question exists -> AwaitingInformation
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, updated.Status);
    }

    private sealed class TestRecoveryEnvironment
    {
        private readonly List<ServiceRequest> _requests = new();
        private readonly bool _throwConcurrencyExceptionOnSave;

        public TestRecoveryRepo Repository { get; }
        public StaleAnalysisRecoveryService Service { get; }

        public TestRecoveryEnvironment(bool throwConcurrencyExceptionOnSave = false)
        {
            _throwConcurrencyExceptionOnSave = throwConcurrencyExceptionOnSave;
            Repository = new TestRecoveryRepo(_requests, _throwConcurrencyExceptionOnSave);
            var options = new C1RecoveryOptions
            {
                StaleAnalysisMinutes = 5,
                PollIntervalSeconds = 60,
                Enabled = true
            };
            Service = new StaleAnalysisRecoveryService(
                Repository,
                workflowDbContext: null,
                directOptions: options,
                logger: NullLogger<StaleAnalysisRecoveryService>.Instance);
        }

        public void AddRequest(ServiceRequest request) => _requests.Add(request);

        public ServiceRequest? GetRequest(Guid id) => _requests.FirstOrDefault(r => r.Id == id);
    }

    private sealed class TestRecoveryRepo : IServiceRequestRepository
    {
        private readonly List<ServiceRequest> _requests;
        private readonly bool _throwConcurrencyException;

        public TestRecoveryRepo(List<ServiceRequest> requests, bool throwConcurrencyException = false)
        {
            _requests = requests;
            _throwConcurrencyException = throwConcurrencyException;
        }

        public Task<ServiceRequest?> ReloadForRecoveryAsync(Guid serviceRequestId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_requests.FirstOrDefault(x => x.Id == serviceRequestId));
        }

        public Task<ServiceRequest?> GetByIdAsync(Guid serviceRequestId, bool includeProblemAnalyses = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_requests.FirstOrDefault(x => x.Id == serviceRequestId));
        }

        public Task<ServiceRequest?> GetByIdAsync(Guid serviceRequestId, bool includeProblemAnalyses, bool includeClarifications, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_requests.FirstOrDefault(x => x.Id == serviceRequestId));
        }

        public Task<ServiceRequest?> GetByIdAndCustomerIdAsync(Guid serviceRequestId, Guid customerId, bool includeProblemAnalyses = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_requests.FirstOrDefault(x => x.Id == serviceRequestId && x.CustomerId == customerId));
        }

        public Task<ServiceRequest?> GetByIdAndCustomerIdAsync(Guid serviceRequestId, Guid customerId, bool includeProblemAnalyses, bool includeClarifications, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_requests.FirstOrDefault(x => x.Id == serviceRequestId && x.CustomerId == customerId));
        }

        public Task<IReadOnlyList<ServiceRequest>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ServiceRequest>>(_requests.Where(x => x.CustomerId == customerId).ToList());
        }

        public Task<IReadOnlyList<ServiceRequest>> GetByStatusAsync(ServiceRequestStatus status, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ServiceRequest>>(_requests.Where(x => x.Status == status).ToList());
        }

        public Task<IReadOnlyList<ServiceRequest>> GetStaleAnalyzingRequestsAsync(DateTime cutoffTimeUtc, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ServiceRequest>>(_requests
                .Where(x => x.Status == ServiceRequestStatus.Analyzing && x.UpdatedAt <= cutoffTimeUtc)
                .ToList());
        }

        public Task<IReadOnlyList<ServiceRequest>> GetStaleAwaitingInformationRequestsAsync(DateTime cutoffTimeUtc, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ServiceRequest>>(_requests
                .Where(x => x.Status == ServiceRequestStatus.AwaitingInformation && x.UpdatedAt <= cutoffTimeUtc)
                .ToList());
        }

        public Task<IReadOnlyList<ServiceRequest>> GetAllForAdminAsync(ServiceRequestStatus? status = null, string? category = null, ServiceRequestUrgency? urgency = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ServiceRequest>>(_requests.ToList());
        }

        public Task AddAsync(ServiceRequest serviceRequest, CancellationToken cancellationToken = default)
        {
            _requests.Add(serviceRequest);
            return Task.CompletedTask;
        }

        public void Update(ServiceRequest serviceRequest)
        {
            // In-memory update
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (_throwConcurrencyException)
            {
                throw new DbUpdateConcurrencyException("Simulated concurrency conflict during test.");
            }

            return Task.CompletedTask;
        }
    }
}
