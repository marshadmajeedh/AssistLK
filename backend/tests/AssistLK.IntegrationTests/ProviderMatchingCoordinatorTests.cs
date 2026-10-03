using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssistLK.Application.Interfaces;
using AssistLK.Application.ServiceRequests.DTOs;
using AssistLK.Application.Services;
using AssistLK.Application.Services.Providers;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AssistLK.IntegrationTests;

public class ProviderMatchingCoordinatorTests
{
    private readonly Mock<IProviderMatchingService> _matchingServiceMock = new();
    private readonly Mock<IServiceRequestService> _serviceRequestServiceMock = new();
    private readonly Mock<ILogger<ProviderMatchingCoordinator>> _loggerMock = new();

    private AssistLKDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AssistLKDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AssistLKDbContext(options);
    }

    [Fact]
    public async Task ExecuteMatchForRequestAsync_WhenAlreadyAccepted_ReturnsAlreadyAccepted()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var requestId = Guid.NewGuid();

        var execution = new MatchingExecution
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            Status = MatchingExecutionStatus.Completed
        };
        db.MatchingExecutions.Add(execution);

        var candidate = new MatchedCandidate
        {
            Id = Guid.NewGuid(),
            MatchingExecutionId = execution.Id,
            ProviderId = Guid.NewGuid(),
            Status = MatchedCandidateStatus.Accepted
        };
        db.MatchedCandidates.Add(candidate);
        await db.SaveChangesAsync();

        var coordinator = new ProviderMatchingCoordinator(
            _matchingServiceMock.Object,
            _serviceRequestServiceMock.Object,
            db,
            _loggerMock.Object,
            new AgentWorkflowService(db),
            new AgentMonitoringService(db));

        // Act
        var result = await coordinator.ExecuteMatchForRequestAsync(requestId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("AlreadyAccepted", result.Status);
        Assert.Contains("already been accepted", result.Message);
        _matchingServiceMock.Verify(m => m.StartMatchingAsync(It.IsAny<MatchStartRequest>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteMatchForRequestAsync_WhenActiveExecutionExists_ReturnsAlreadyActive()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var requestId = Guid.NewGuid();

        var execution = new MatchingExecution
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            Status = MatchingExecutionStatus.PendingApproval
        };
        db.MatchingExecutions.Add(execution);
        await db.SaveChangesAsync();

        var coordinator = new ProviderMatchingCoordinator(
            _matchingServiceMock.Object,
            _serviceRequestServiceMock.Object,
            db,
            _loggerMock.Object,
            new AgentWorkflowService(db),
            new AgentMonitoringService(db));

        // Act
        var result = await coordinator.ExecuteMatchForRequestAsync(requestId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("AlreadyActive", result.Status);
        _matchingServiceMock.Verify(m => m.StartMatchingAsync(It.IsAny<MatchStartRequest>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteMatchForRequestAsync_WhenRequestNotReady_ReturnsNotReady()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var requestId = Guid.NewGuid();

        _serviceRequestServiceMock
            .Setup(s => s.GetReadyForMatchingAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequestForMatchingResponse?)null);

        var coordinator = new ProviderMatchingCoordinator(
            _matchingServiceMock.Object,
            _serviceRequestServiceMock.Object,
            db,
            _loggerMock.Object,
            new AgentWorkflowService(db),
            new AgentMonitoringService(db));

        // Act
        var result = await coordinator.ExecuteMatchForRequestAsync(requestId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("NotReady", result.Status);
        Assert.Contains("not found or not ready", result.Message);
    }

    [Fact]
    public async Task ExecuteMatchForRequestAsync_WhenNoEligibleProvidersInDb_ReturnsNoEligibleProviders()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var requestId = Guid.NewGuid();

        var response = new ServiceRequestForMatchingResponse
        {
            ServiceRequestId = requestId,
            Category = "Plumbing",
            Urgency = ServiceRequestUrgency.High,
            ProblemSummary = "Water pipe burst",
            LocationText = "Colombo 03",
            Latitude = 6.9270m,
            Longitude = 79.8610m,
            Status = ServiceRequestStatus.ReadyForMatching,
            MatchingExpiresAtUtc = DateTime.UtcNow.AddHours(24),
            IsMatchingEligible = true
        };

        _serviceRequestServiceMock
            .Setup(s => s.GetReadyForMatchingAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var coordinator = new ProviderMatchingCoordinator(
            _matchingServiceMock.Object,
            _serviceRequestServiceMock.Object,
            db,
            _loggerMock.Object,
            new AgentWorkflowService(db),
            new AgentMonitoringService(db));

        // Act
        var result = await coordinator.ExecuteMatchForRequestAsync(requestId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No_Eligible_Providers", result.Status);
        _matchingServiceMock.Verify(m => m.StartMatchingAsync(It.IsAny<MatchStartRequest>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteMatchForRequestAsync_ExcludesDeclinedProvidersFromCandidates()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var requestId = Guid.NewGuid();

        var response = new ServiceRequestForMatchingResponse
        {
            ServiceRequestId = requestId,
            Category = "Plumbing",
            Urgency = ServiceRequestUrgency.High,
            ProblemSummary = "Water pipe burst",
            LocationText = "Colombo",
            Latitude = 6.9270m,
            Longitude = 79.8610m,
            Status = ServiceRequestStatus.ReadyForMatching,
            MatchingExpiresAtUtc = DateTime.UtcNow.AddHours(24),
            IsMatchingEligible = true
        };

        _serviceRequestServiceMock
            .Setup(s => s.GetReadyForMatchingAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var provider1Id = Guid.NewGuid();
        var provider2Id = Guid.NewGuid();

        // Seed Provider 1 (Previously Declined)
        var p1 = new ProviderProfile
        {
            Id = provider1Id,
            UserId = Guid.NewGuid(),
            BusinessName = "Declined Plumber",
            IsOnline = true,
            VerificationStatus = ProviderVerificationStatus.Verified,
            Rating = 4.5m,
            MaxActiveJobs = 3,
            Skills = new List<ProviderSkill> { new() { Id = Guid.NewGuid(), Category = "Plumbing", SkillName = "Plumbing" } },
            Locations = new List<ProviderLocation> { new() { Id = Guid.NewGuid(), Latitude = 6.9270m, Longitude = 79.8610m, OperatingRadiusKm = 10m } }
        };

        // Seed Provider 2 (Available)
        var p2 = new ProviderProfile
        {
            Id = provider2Id,
            UserId = Guid.NewGuid(),
            BusinessName = "Available Plumber",
            IsOnline = true,
            VerificationStatus = ProviderVerificationStatus.Verified,
            Rating = 4.8m,
            MaxActiveJobs = 3,
            Skills = new List<ProviderSkill> { new() { Id = Guid.NewGuid(), Category = "Plumbing", SkillName = "Plumbing" } },
            Locations = new List<ProviderLocation> { new() { Id = Guid.NewGuid(), Latitude = 6.9270m, Longitude = 79.8610m, OperatingRadiusKm = 10m } }
        };

        db.ProviderProfiles.AddRange(p1, p2);

        // Previous execution where Provider 1 declined
        var prevExec = new MatchingExecution { Id = Guid.NewGuid(), ServiceRequestId = requestId, Status = MatchingExecutionStatus.Failed };
        db.MatchingExecutions.Add(prevExec);
        db.MatchedCandidates.Add(new MatchedCandidate
        {
            Id = Guid.NewGuid(),
            MatchingExecutionId = prevExec.Id,
            ProviderId = provider1Id,
            Status = MatchedCandidateStatus.Declined
        });

        await db.SaveChangesAsync();

        MatchStartRequest? capturedRequest = null;
        _matchingServiceMock
            .Setup(m => m.StartMatchingAsync(It.IsAny<MatchStartRequest>()))
            .Callback<MatchStartRequest>(r => capturedRequest = r)
            .ReturnsAsync(new MatchResponse
            {
                ThreadId = "thread-declined-test",
                Status = "Pending",
                RecommendedProvider = new RecommendedProvider
                {
                    Id = provider2Id.ToString(),
                    Name = "Available Plumber",
                    Score = 0.95m,
                    Rank = 1
                }
            });

        var coordinator = new ProviderMatchingCoordinator(
            _matchingServiceMock.Object,
            _serviceRequestServiceMock.Object,
            db,
            _loggerMock.Object,
            new AgentWorkflowService(db),
            new AgentMonitoringService(db));

        // Act
        var result = await coordinator.ExecuteMatchForRequestAsync(requestId);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Single(capturedRequest.EligibleProviders);
        Assert.Equal(provider2Id.ToString(), capturedRequest.EligibleProviders[0].ProviderId);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task ExecuteMatchForRequestAsync_MaxActiveJobsCapacityEnforcement_ExcludesBusyProviders()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var requestId = Guid.NewGuid();

        var response = new ServiceRequestForMatchingResponse
        {
            ServiceRequestId = requestId,
            Category = "Plumbing",
            Urgency = ServiceRequestUrgency.Medium,
            ProblemSummary = "Leaking pipe",
            LocationText = "Colombo",
            Latitude = 6.9270m,
            Longitude = 79.8610m,
            Status = ServiceRequestStatus.ReadyForMatching,
            MatchingExpiresAtUtc = DateTime.UtcNow.AddHours(24),
            IsMatchingEligible = true
        };

        _serviceRequestServiceMock
            .Setup(s => s.GetReadyForMatchingAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var busyProviderId = Guid.NewGuid();
        var freeProviderId = Guid.NewGuid();

        var pBusy = new ProviderProfile
        {
            Id = busyProviderId,
            UserId = Guid.NewGuid(),
            BusinessName = "Busy Plumber",
            IsOnline = true,
            VerificationStatus = ProviderVerificationStatus.Verified,
            Rating = 5.0m,
            MaxActiveJobs = 1, // Only allowed 1 active job
            Skills = new List<ProviderSkill> { new() { Id = Guid.NewGuid(), Category = "Plumbing", SkillName = "Plumbing" } },
            Locations = new List<ProviderLocation> { new() { Id = Guid.NewGuid(), Latitude = 6.9270m, Longitude = 79.8610m, OperatingRadiusKm = 10m } }
        };

        var pFree = new ProviderProfile
        {
            Id = freeProviderId,
            UserId = Guid.NewGuid(),
            BusinessName = "Free Plumber",
            IsOnline = true,
            VerificationStatus = ProviderVerificationStatus.Verified,
            Rating = 4.2m,
            MaxActiveJobs = 2,
            Skills = new List<ProviderSkill> { new() { Id = Guid.NewGuid(), Category = "Plumbing", SkillName = "Plumbing" } },
            Locations = new List<ProviderLocation> { new() { Id = Guid.NewGuid(), Latitude = 6.9270m, Longitude = 79.8610m, OperatingRadiusKm = 10m } }
        };

        db.ProviderProfiles.AddRange(pBusy, pFree);

        // Give Busy Plumber an existing accepted job
        var otherExec = new MatchingExecution { Id = Guid.NewGuid(), ServiceRequestId = Guid.NewGuid(), Status = MatchingExecutionStatus.Completed };
        db.MatchingExecutions.Add(otherExec);
        db.MatchedCandidates.Add(new MatchedCandidate
        {
            Id = Guid.NewGuid(),
            MatchingExecutionId = otherExec.Id,
            ProviderId = busyProviderId,
            Status = MatchedCandidateStatus.Accepted // Busy!
        });

        await db.SaveChangesAsync();

        MatchStartRequest? capturedRequest = null;
        _matchingServiceMock
            .Setup(m => m.StartMatchingAsync(It.IsAny<MatchStartRequest>()))
            .Callback<MatchStartRequest>(r => capturedRequest = r)
            .ReturnsAsync(new MatchResponse
            {
                ThreadId = "thread-capacity-test",
                Status = "Pending",
                RecommendedProvider = new RecommendedProvider
                {
                    Id = freeProviderId.ToString(),
                    Name = "Free Plumber",
                    Score = 0.88m,
                    Rank = 1
                }
            });

        var coordinator = new ProviderMatchingCoordinator(
            _matchingServiceMock.Object,
            _serviceRequestServiceMock.Object,
            db,
            _loggerMock.Object,
            new AgentWorkflowService(db),
            new AgentMonitoringService(db));

        // Act
        var result = await coordinator.ExecuteMatchForRequestAsync(requestId);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Single(capturedRequest.EligibleProviders);
        Assert.Equal(freeProviderId.ToString(), capturedRequest.EligibleProviders[0].ProviderId);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task ExecuteMatchForRequestAsync_Success_CreatesPendingApprovalExecutionAndCandidate()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var requestId = Guid.NewGuid();

        var response = new ServiceRequestForMatchingResponse
        {
            ServiceRequestId = requestId,
            Category = "Plumbing",
            Urgency = ServiceRequestUrgency.High,
            ProblemSummary = "Water pipe burst",
            LocationText = "Colombo",
            Latitude = 6.9270m,
            Longitude = 79.8610m,
            Status = ServiceRequestStatus.ReadyForMatching,
            MatchingExpiresAtUtc = DateTime.UtcNow.AddHours(24),
            IsMatchingEligible = true
        };

        _serviceRequestServiceMock
            .Setup(s => s.GetReadyForMatchingAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var providerId = Guid.NewGuid();
        var provider = new ProviderProfile
        {
            Id = providerId,
            UserId = Guid.NewGuid(),
            BusinessName = "Apex Plumbing",
            IsOnline = true,
            VerificationStatus = ProviderVerificationStatus.Verified,
            Rating = 4.9m,
            MaxActiveJobs = 3,
            Skills = new List<ProviderSkill> { new() { Id = Guid.NewGuid(), Category = "Plumbing", SkillName = "Plumbing" } },
            Locations = new List<ProviderLocation> { new() { Id = Guid.NewGuid(), Latitude = 6.9270m, Longitude = 79.8610m, OperatingRadiusKm = 10m } }
        };

        db.ProviderProfiles.Add(provider);
        await db.SaveChangesAsync();

        _matchingServiceMock
            .Setup(m => m.StartMatchingAsync(It.IsAny<MatchStartRequest>()))
            .ReturnsAsync(new MatchResponse
            {
                ThreadId = "thread-hitl-001",
                Status = "Pending",
                RecommendedProvider = new RecommendedProvider
                {
                    Id = providerId.ToString(),
                    Name = "Apex Plumbing",
                    Score = 0.94m,
                    DistanceKm = 0.8m,
                    Rating = 4.9m,
                    Verified = true,
                    MatchRationale = "Top match based on proximity and 4.9 star rating.",
                    Rank = 1
                },
                TokensConsumed = new TokenUsageDto { TotalTokens = 50 }
            });

        var coordinator = new ProviderMatchingCoordinator(
            _matchingServiceMock.Object,
            _serviceRequestServiceMock.Object,
            db,
            _loggerMock.Object,
            new AgentWorkflowService(db),
            new AgentMonitoringService(db));

        // Act
        var result = await coordinator.ExecuteMatchForRequestAsync(requestId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Pending", result.Status);
        Assert.Equal("thread-hitl-001", result.ThreadId);

        var savedExec = await db.MatchingExecutions.Include(e => e.Candidates).FirstOrDefaultAsync(e => e.ServiceRequestId == requestId);
        Assert.NotNull(savedExec);
        Assert.Equal(MatchingExecutionStatus.PendingApproval, savedExec.Status);
        Assert.Equal("thread-hitl-001", savedExec.ThreadId);

        Assert.Single(savedExec.Candidates);
        var cand = savedExec.Candidates.First();
        Assert.Equal(providerId, cand.ProviderId);
        Assert.Equal(0.94m, cand.Score);
        Assert.Equal(MatchedCandidateStatus.Recommended, cand.Status);
    }

    [Fact]
    public async Task ExecuteMatchForRequestAsync_WhenMatchingExpired_ReturnsMatchingExpired()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var requestId = Guid.NewGuid();

        var response = new ServiceRequestForMatchingResponse
        {
            ServiceRequestId = requestId,
            Category = "Plumbing",
            Urgency = ServiceRequestUrgency.High,
            ProblemSummary = "Water pipe burst",
            LocationText = "Colombo",
            Latitude = 6.9270m,
            Longitude = 79.8610m,
            Status = ServiceRequestStatus.ReadyForMatching,
            MatchingExpiresAtUtc = DateTime.UtcNow.AddHours(-1),
            IsMatchingEligible = false
        };

        _serviceRequestServiceMock
            .Setup(s => s.GetReadyForMatchingAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var coordinator = new ProviderMatchingCoordinator(
            _matchingServiceMock.Object,
            _serviceRequestServiceMock.Object,
            db,
            _loggerMock.Object,
            new AgentWorkflowService(db),
            new AgentMonitoringService(db));

        // Act
        var result = await coordinator.ExecuteMatchForRequestAsync(requestId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("MatchingExpired", result.Status);
        Assert.Contains("expired", result.Message, StringComparison.OrdinalIgnoreCase);
        _matchingServiceMock.Verify(m => m.StartMatchingAsync(It.IsAny<MatchStartRequest>()), Times.Never);
    }
}
