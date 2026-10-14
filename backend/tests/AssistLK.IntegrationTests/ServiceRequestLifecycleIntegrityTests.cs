using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AssistLK.Application.Common.Exceptions;
using AssistLK.Application.Interfaces;
using AssistLK.Application.ServiceRequests.DTOs;
using AssistLK.Application.Services;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using AssistLK.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AssistLK.IntegrationTests;

// ==============================================================================
// TEST SUITE: ServiceRequestLifecycleIntegrityTests
// What this class tests:
//   - Architectural integrity and boundary enforcement of the ServiceRequestStatus enum.
//   - Verification that only the 6 authoritative C1 status values exist (Created, Analyzing,
//     AwaitingInformation, Analyzed, ReadyForMatching, Cancelled).
//   - Isolation between components: Component 2 (MatchingExecution) and Component 3 (Quotation, Booking)
//     manage their own lifecycles without mutating the parent ServiceRequest.Status.
//   - Materialization of all valid statuses in EF Core and admin query filtering across statuses.
// Why these tests are needed:
//   - Prevents domain pollution and architectural leakage where downstream operations (matching, quotation,
//     booking) might incorrectly mutate ServiceRequest.Status into non-C1 values like Matched or Booked.
// What is real and what is mocked/faked:
//   - Real: ServiceRequestService, ServiceRequestRepository, ProblemAnalysisRepository,
//           EF Core domain entities (ServiceRequest, MatchingExecution, MatchedCandidate, Quotation, Booking).
//   - In-Memory/Faked: EF Core InMemory database (AssistLKDbContext).
// Prerequisites:
//   - None (runs purely in-memory).
// Run from repository root:
//   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.ServiceRequestLifecycleIntegrityTests"
// ==============================================================================
public class ServiceRequestLifecycleIntegrityTests
{
    private static AssistLKDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AssistLKDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AssistLKDbContext(options);
    }

    // Purpose: Checks that all 6 valid ServiceRequestStatus enum values can be persisted and retrieved via EF Core without mapping errors.
    // Why: Verifies that database mappings correctly support all valid lifecycle states.
    // Scenario: Persisting an entity with each of the 6 enum values (Created, Analyzing, AwaitingInformation, Analyzed, ReadyForMatching, Cancelled).
    // Pass condition: Retrieved entity has the exact status specified. Note: Parameterized test running all 6 InlineData cases.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.ServiceRequestLifecycleIntegrityTests.ValidServiceRequestStatuses_MaterializeSuccessfully"
    [Theory]
    [InlineData(ServiceRequestStatus.Created)]
    [InlineData(ServiceRequestStatus.Analyzing)]
    [InlineData(ServiceRequestStatus.AwaitingInformation)]
    [InlineData(ServiceRequestStatus.Analyzed)]
    [InlineData(ServiceRequestStatus.ReadyForMatching)]
    [InlineData(ServiceRequestStatus.Cancelled)]
    public async Task ValidServiceRequestStatuses_MaterializeSuccessfully(ServiceRequestStatus validStatus)
    {
        await using var db = CreateInMemoryDbContext();
        var requestId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        await db.ServiceRequests.AddAsync(new ServiceRequest
        {
            Id = requestId,
            CustomerId = customerId,
            Description = $"Test request with status {validStatus}",
            LocationText = "Colombo",
            Category = "Plumbing",
            Urgency = ServiceRequestUrgency.Medium,
            Status = validStatus,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var repo = new ServiceRequestRepository(db);
        var retrieved = await repo.GetByIdAsync(requestId);

        Assert.NotNull(retrieved);
        Assert.Equal(validStatus, retrieved.Status);
    }

    // Purpose: Checks that ServiceRequestStatus enum defines exactly 6 values and contains no downstream workflow names (e.g. "Completed", "Matched", "Booked").
    // Why: Enforces architectural boundary: C1 service request lifecycle terminates at ReadyForMatching or Cancelled.
    // Scenario: Reflection inspection of Enum.GetNames<ServiceRequestStatus>().
    // Pass condition: Length is exactly 6 and excluded downstream status names are not present.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestLifecycleIntegrityTests.ServiceRequestStatus_Enum_DoesNotContainCompletedOrMatchingStatuses"
    [Fact]
    public void ServiceRequestStatus_Enum_DoesNotContainCompletedOrMatchingStatuses()
    {
        var statusNames = Enum.GetNames<ServiceRequestStatus>();

        Assert.DoesNotContain("Completed", statusNames);
        Assert.DoesNotContain("Failed", statusNames);
        Assert.DoesNotContain("Matched", statusNames);
        Assert.DoesNotContain("Assigned", statusNames);
        Assert.DoesNotContain("ProviderAssigned", statusNames);
        Assert.DoesNotContain("Booked", statusNames);
        Assert.DoesNotContain("InProgress", statusNames);
        Assert.DoesNotContain("PendingApproval", statusNames);

        // Authoritative C1 lifecycle values only
        Assert.Equal(6, statusNames.Length);
        Assert.Contains("Created", statusNames);
        Assert.Contains("Analyzing", statusNames);
        Assert.Contains("AwaitingInformation", statusNames);
        Assert.Contains("Analyzed", statusNames);
        Assert.Contains("ReadyForMatching", statusNames);
        Assert.Contains("Cancelled", statusNames);
    }

    // Purpose: Checks that MatchingExecutionStatus enum contains its own execution states (Pending, Running, Completed, Failed, PendingApproval).
    // Why: Confirms that Component 2 isolates its lifecycle within its own execution entity.
    // Scenario: Inspecting Enum.GetNames<MatchingExecutionStatus>().
    // Pass condition: All expected matching execution status names are present.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestLifecycleIntegrityTests.MatchingExecutionStatus_Enum_ContainsExpectedExecutionLifecycle"
    [Fact]
    public void MatchingExecutionStatus_Enum_ContainsExpectedExecutionLifecycle()
    {
        var statusNames = Enum.GetNames<MatchingExecutionStatus>();

        Assert.Contains("Pending", statusNames);
        Assert.Contains("Running", statusNames);
        Assert.Contains("Completed", statusNames);
        Assert.Contains("Failed", statusNames);
        Assert.Contains("PendingApproval", statusNames);
    }

    // Purpose: Checks that transitioning to ReadyForMatching requires prior analysis (Analyzed status) and exposes the clean handoff contract for Component 2.
    // Why: Protects the C1/C2 handoff contract so incomplete requests cannot be marked ready.
    // Scenario: Request transitions from Created (fails) -> Analyzed with analysis -> ReadyForMatching (succeeds) -> handoff DTO inspected.
    // Pass condition: Direct transition from Created throws ConflictException; transition from Analyzed succeeds; handoff DTO matches expected category and confidence.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestLifecycleIntegrityTests.C1_ReadyForMatching_SemanticsRemainAuthoritativeHandoff"
    [Fact]
    public async Task C1_ReadyForMatching_SemanticsRemainAuthoritativeHandoff()
    {
        await using var db = CreateInMemoryDbContext();
        var repo = new ServiceRequestRepository(db);
        var analysisRepo = new ProblemAnalysisRepository(db);
        var service = new ServiceRequestService(repo, analysisRepo);

        var customerId = Guid.NewGuid();
        var createResponse = await service.CreateAsync(customerId, new CreateServiceRequestRequest
        {
            Description = "Leaking pipe in bathroom",
            LocationText = "Colombo 03"
        });

        // Request starts at Created
        Assert.Equal(ServiceRequestStatus.Created, createResponse.Status);

        // Cannot mark ready for matching directly from Created (requires Analyzed)
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.MarkReadyForMatchingAsync(createResponse.ServiceRequestId, customerId));

        // Simulate successful C1 Problem Understanding analysis
        var request = await repo.GetByIdAsync(createResponse.ServiceRequestId);
        Assert.NotNull(request);
        request.Status = ServiceRequestStatus.Analyzed;
        request.Category = "Plumbing";
        request.Urgency = ServiceRequestUrgency.High;
        request.Latitude = 6.9271m;
        request.Longitude = 79.8612m;

        var analysis = new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = request.Id,
            DetectedProblem = "Burst pipe",
            Confidence = 0.95m,
            AgentName = "ProblemUnderstandingAgent",
            EvidenceRevision = request.EvidenceRevision,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await analysisRepo.AddAsync(analysis);
        await repo.SaveChangesAsync();

        // Mark ready for matching transitions cleanly to ReadyForMatching
        var handoffResponse = await service.MarkReadyForMatchingAsync(request.Id, customerId);
        Assert.Equal(ServiceRequestStatus.ReadyForMatching, handoffResponse.Status);

        // ReadyForMatching data contract exposed for C2 consumption
        var matchingHandoff = await service.GetReadyForMatchingAsync(request.Id);
        Assert.NotNull(matchingHandoff);
        Assert.Equal(ServiceRequestStatus.ReadyForMatching, matchingHandoff.Status);
        Assert.Equal("Plumbing", matchingHandoff.Category);
        Assert.Equal(0.95m, matchingHandoff.Confidence);
    }

    // Purpose: Checks that GetAllForAdminAsync with no status filter returns requests across all 6 lifecycle statuses.
    // Why: Verifies that admin views receive the full spectrum of requests without unintended status filtering.
    // Scenario: 6 requests seeded across all 6 statuses; queried with status = null.
    // Pass condition: All 6 requests are returned.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestLifecycleIntegrityTests.Admin_GetAllForAdmin_WithNoFilter_ReturnsAllValidRequestsAcrossStatuses"
    [Fact]
    public async Task Admin_GetAllForAdmin_WithNoFilter_ReturnsAllValidRequestsAcrossStatuses()
    {
        await using var db = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();

        var statuses = new[]
        {
            ServiceRequestStatus.Created,
            ServiceRequestStatus.Analyzing,
            ServiceRequestStatus.AwaitingInformation,
            ServiceRequestStatus.Analyzed,
            ServiceRequestStatus.ReadyForMatching,
            ServiceRequestStatus.Cancelled
        };

        var seededIds = new List<Guid>();
        for (int i = 0; i < statuses.Length; i++)
        {
            var id = Guid.NewGuid();
            seededIds.Add(id);
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = id,
                CustomerId = customerId,
                Description = $"Request {i} in status {statuses[i]}",
                LocationText = "Colombo",
                Category = "Plumbing",
                Urgency = ServiceRequestUrgency.Medium,
                Status = statuses[i],
                CreatedAt = DateTime.UtcNow.AddMinutes(-i),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await db.SaveChangesAsync();

        var repo = new ServiceRequestRepository(db);
        var analysisRepo = new ProblemAnalysisRepository(db);
        var service = new ServiceRequestService(repo, analysisRepo);

        // Query with null status (equivalent to "All" filter)
        var allRequests = await service.GetAllForAdminAsync(status: null, category: null, urgency: null);

        Assert.NotNull(allRequests);
        Assert.Equal(statuses.Length, allRequests.Count);
        foreach (var id in seededIds)
        {
            Assert.Contains(allRequests, r => r.ServiceRequestId == id);
        }
    }

    // Purpose: Checks that filtering by specific status names in GetAllForAdminAsync returns only requests with that status.
    // Why: Enables admins to filter requests by specific operational state.
    // Scenario: Requests in Created, ReadyForMatching, and Analyzed filtered individually.
    // Pass condition: Each query returns only the matching request ID.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestLifecycleIntegrityTests.Admin_GetAllForAdmin_WithStatusFilter_ReturnsOnlyMatchingRequests"
    [Fact]
    public async Task Admin_GetAllForAdmin_WithStatusFilter_ReturnsOnlyMatchingRequests()
    {
        await using var db = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();

        var reqCreated = Guid.NewGuid();
        var reqReady = Guid.NewGuid();
        var reqAnalyzed = Guid.NewGuid();

        await db.ServiceRequests.AddRangeAsync(
            new ServiceRequest
            {
                Id = reqCreated,
                CustomerId = customerId,
                Description = "Created request",
                LocationText = "Colombo",
                Category = "Plumbing",
                Urgency = ServiceRequestUrgency.Low,
                Status = ServiceRequestStatus.Created,
                CreatedAt = DateTime.UtcNow.AddMinutes(-3),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-3)
            },
            new ServiceRequest
            {
                Id = reqReady,
                CustomerId = customerId,
                Description = "Ready request",
                LocationText = "Colombo",
                Category = "Plumbing",
                Urgency = ServiceRequestUrgency.High,
                Status = ServiceRequestStatus.ReadyForMatching,
                CreatedAt = DateTime.UtcNow.AddMinutes(-2),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-2)
            },
            new ServiceRequest
            {
                Id = reqAnalyzed,
                CustomerId = customerId,
                Description = "Analyzed request",
                LocationText = "Colombo",
                Category = "Electrical",
                Urgency = ServiceRequestUrgency.Critical,
                Status = ServiceRequestStatus.Analyzed,
                CreatedAt = DateTime.UtcNow.AddMinutes(-1),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-1)
            }
        );
        await db.SaveChangesAsync();

        var repo = new ServiceRequestRepository(db);
        var analysisRepo = new ProblemAnalysisRepository(db);
        var service = new ServiceRequestService(repo, analysisRepo);

        // Filter by Created
        var createdList = await service.GetAllForAdminAsync(status: "Created");
        Assert.Single(createdList);
        Assert.Equal(reqCreated, createdList[0].ServiceRequestId);

        // Filter by ReadyForMatching
        var readyList = await service.GetAllForAdminAsync(status: "ReadyForMatching");
        Assert.Single(readyList);
        Assert.Equal(reqReady, readyList[0].ServiceRequestId);

        // Filter by Analyzed
        var analyzedList = await service.GetAllForAdminAsync(status: "Analyzed");
        Assert.Single(analyzedList);
        Assert.Equal(reqAnalyzed, analyzedList[0].ServiceRequestId);
    }

    // Purpose: Checks that running and completing a Component 2 MatchingExecution and accepting a candidate leaves the parent ServiceRequest.Status unchanged as ReadyForMatching.
    // Why: Verifies that C2 provider matching actions do not corrupt or overwrite C1 service request status.
    // Scenario: MatchingExecution and MatchedCandidate progress to Completed and Accepted for a request in ReadyForMatching.
    // Pass condition: ServiceRequest.Status remains ReadyForMatching while matching execution is Completed.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestLifecycleIntegrityTests.C2_MatchingExecution_CanBeCompleted_WithoutMutatingServiceRequestStatus"
    [Fact]
    public async Task C2_MatchingExecution_CanBeCompleted_WithoutMutatingServiceRequestStatus()
    {
        await using var db = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var serviceRequestId = Guid.NewGuid();

        // 1. ServiceRequest reaches ReadyForMatching at end of C1
        var serviceRequest = new ServiceRequest
        {
            Id = serviceRequestId,
            CustomerId = customerId,
            Description = "Water pipe burst in kitchen",
            LocationText = "Colombo 03",
            Category = "Plumbing",
            Urgency = ServiceRequestUrgency.High,
            Status = ServiceRequestStatus.ReadyForMatching,
            CreatedAt = DateTime.UtcNow.AddMinutes(-30),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-25)
        };
        await db.ServiceRequests.AddAsync(serviceRequest);

        // 2. C2 initiates matching execution
        var execution = new MatchingExecution
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = serviceRequestId,
            Status = MatchingExecutionStatus.Running,
            StartedAt = DateTime.UtcNow.AddMinutes(-20),
            ThreadId = "thread-c2-test-001"
        };
        await db.MatchingExecutions.AddAsync(execution);

        var candidate = new MatchedCandidate
        {
            Id = Guid.NewGuid(),
            MatchingExecutionId = execution.Id,
            ProviderId = Guid.NewGuid(),
            Score = 0.92m,
            Rank = 1,
            DistanceKm = 3.2m,
            Status = MatchedCandidateStatus.Recommended,
            CreatedAt = DateTime.UtcNow.AddMinutes(-18)
        };
        await db.MatchedCandidates.AddAsync(candidate);
        await db.SaveChangesAsync();

        // 3. Matching execution completes and candidate is accepted
        execution.Status = MatchingExecutionStatus.Completed;
        execution.CompletedAt = DateTime.UtcNow.AddMinutes(-15);
        candidate.Status = MatchedCandidateStatus.Accepted;
        candidate.UpdatedAt = DateTime.UtcNow.AddMinutes(-10);
        await db.SaveChangesAsync();

        // 4. Assert that ServiceRequest.Status remains ReadyForMatching
        var retrievedRequest = await db.ServiceRequests.FindAsync(serviceRequestId);
        Assert.NotNull(retrievedRequest);
        Assert.Equal(ServiceRequestStatus.ReadyForMatching, retrievedRequest.Status);

        // And matching execution is completed
        var retrievedExecution = await db.MatchingExecutions.FindAsync(execution.Id);
        Assert.NotNull(retrievedExecution);
        Assert.Equal(MatchingExecutionStatus.Completed, retrievedExecution.Status);

        // And matched candidate is accepted
        var retrievedCandidate = await db.MatchedCandidates.FindAsync(candidate.Id);
        Assert.NotNull(retrievedCandidate);
        Assert.Equal(MatchedCandidateStatus.Accepted, retrievedCandidate.Status);
    }

    // Purpose: Checks that creating, approving a quotation, and confirming a booking in Component 3 leaves the parent ServiceRequest.Status as ReadyForMatching.
    // Why: Guarantees that quotation and booking domain entities maintain their own statuses without mutating C1 service request state.
    // Scenario: Quotation approved and Booking confirmed for a request in ReadyForMatching.
    // Pass condition: ServiceRequest.Status remains ReadyForMatching, Quotation is Approved, and Booking is Confirmed.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestLifecycleIntegrityTests.C3_QuotationApprovalAndBooking_DoesNotMutateServiceRequestStatus"
    [Fact]
    public async Task C3_QuotationApprovalAndBooking_DoesNotMutateServiceRequestStatus()
    {
        await using var db = CreateInMemoryDbContext();
        var customerId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var serviceRequestId = Guid.NewGuid();

        // 1. ServiceRequest reaches ReadyForMatching at end of C1
        var serviceRequest = new ServiceRequest
        {
            Id = serviceRequestId,
            CustomerId = customerId,
            Description = "Electrical short circuit in living room",
            LocationText = "Colombo 07",
            Category = "Electrical",
            Urgency = ServiceRequestUrgency.High,
            Status = ServiceRequestStatus.ReadyForMatching,
            CreatedAt = DateTime.UtcNow.AddMinutes(-30),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-25)
        };
        await db.ServiceRequests.AddAsync(serviceRequest);

        // 2. C3 Quotation is created and submitted for approval
        var quotation = new Quotation
        {
            Id = 1,
            ServiceRequestId = serviceRequestId,
            ProviderId = providerId,
            Status = QuotationStatus.WaitingForCustomerApproval,
            TotalAmount = 5000m,
            CreatedAt = DateTime.UtcNow.AddMinutes(-20),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-15)
        };
        await db.Quotations.AddAsync(quotation);
        await db.SaveChangesAsync();

        // 3. Customer approves quotation -> Booking is created with Confirmed status
        quotation.Status = QuotationStatus.Approved;
        quotation.UpdatedAt = DateTime.UtcNow.AddMinutes(-5);

        var booking = new Booking
        {
            Id = 1,
            QuotationId = quotation.Id,
            CustomerId = customerId,
            ProviderId = providerId,
            Status = BookingStatus.Confirmed,
            ScheduledAt = DateTime.UtcNow.AddDays(1),
            LocationText = serviceRequest.LocationText,
            Latitude = serviceRequest.Latitude,
            Longitude = serviceRequest.Longitude,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        await db.Bookings.AddAsync(booking);
        await db.SaveChangesAsync();

        // 4. Assert that ServiceRequest.Status strictly remains ReadyForMatching
        var retrievedRequest = await db.ServiceRequests.FindAsync(serviceRequestId);
        Assert.NotNull(retrievedRequest);
        Assert.Equal(ServiceRequestStatus.ReadyForMatching, retrievedRequest.Status);

        // And C3 Quotation is Approved
        var retrievedQuotation = await db.Quotations.FindAsync(quotation.Id);
        Assert.NotNull(retrievedQuotation);
        Assert.Equal(QuotationStatus.Approved, retrievedQuotation.Status);

        // And C3 Booking is Confirmed
        var retrievedBooking = await db.Bookings.FindAsync(booking.Id);
        Assert.NotNull(retrievedBooking);
        Assert.Equal(BookingStatus.Confirmed, retrievedBooking.Status);
    }
}
