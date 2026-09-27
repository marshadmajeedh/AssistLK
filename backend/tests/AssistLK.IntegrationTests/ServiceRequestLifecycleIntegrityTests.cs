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

public class ServiceRequestLifecycleIntegrityTests
{
    private static AssistLKDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AssistLKDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AssistLKDbContext(options);
    }

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

    [Fact]
    public void ServiceRequestStatus_Enum_DoesNotContainCompletedOrMatchingStatuses()
    {
        var statusNames = Enum.GetNames<ServiceRequestStatus>();

        Assert.DoesNotContain("Completed", statusNames);
        Assert.DoesNotContain("Failed", statusNames);
        Assert.DoesNotContain("Matched", statusNames);
        Assert.DoesNotContain("Assigned", statusNames);
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
}
