using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AssistLK.Api.Features.Providers;
using AssistLK.Application.Interfaces;
using AssistLK.Application.Services.Providers;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AssistLK.Api.Tests;

public class ProviderMatchingControllerTests : IDisposable
{
    private readonly AssistLKDbContext _dbContext;
    private readonly Mock<IProviderMatchingService> _matchingServiceMock = new();
    private readonly Mock<IProviderMatchingCoordinator> _coordinatorMock = new();
    private readonly Mock<IServiceRequestService> _serviceRequestServiceMock = new();
    private readonly Mock<IServiceRequestRepository> _repoMock = new();
    private readonly Mock<ILogger<ProviderMatchingController>> _loggerMock = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();

    public ProviderMatchingControllerTests()
    {
        var options = new DbContextOptionsBuilder<AssistLKDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _dbContext = new AssistLKDbContext(options);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private ProviderMatchingController CreateController(string? adminId = null)
    {
        var controller = new ProviderMatchingController(
            _matchingServiceMock.Object,
            _coordinatorMock.Object,
            _serviceRequestServiceMock.Object,
            _repoMock.Object,
            _dbContext,
            _loggerMock.Object,
            _scopeFactoryMock.Object);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "Admin")
        };
        if (!string.IsNullOrEmpty(adminId))
        {
            claims.Add(new(ClaimTypes.NameIdentifier, adminId));
        }

        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        return controller;
    }

    [Fact]
    public async Task StartMatching_Success_ReturnsOkWithThreadId()
    {
        // Arrange
        var controller = CreateController();
        var requestId = Guid.NewGuid();

        _coordinatorMock
            .Setup(c => c.ExecuteMatchForRequestAsync(requestId, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(new MatchingExecutionResult
            {
                Success = true,
                Status = "Pending",
                ThreadId = "thread-test-123",
                Message = "Awaiting Admin Approval",
                TokensConsumed = new TokenUsageDto { TotalTokens = 42 }
            });

        // Act
        var result = await controller.StartMatching(requestId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(okResult.Value);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal("thread-test-123", doc.RootElement.GetProperty("threadId").GetString());
        Assert.Equal("Pending", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task StartMatching_WhenNotReady_ReturnsNotFound()
    {
        // Arrange
        var controller = CreateController();
        var requestId = Guid.NewGuid();

        _coordinatorMock
            .Setup(c => c.ExecuteMatchForRequestAsync(requestId, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(new MatchingExecutionResult
            {
                Success = false,
                Status = "NotReady",
                Message = "Request not ready"
            });

        // Act
        var result = await controller.StartMatching(requestId, CancellationToken.None);

        // Assert
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Contains("not found or not ready", notFound.Value?.ToString() ?? "");
    }

    [Fact]
    public async Task StartMatching_WhenFailed_ReturnsServiceUnavailable503()
    {
        // Arrange
        var controller = CreateController();
        var requestId = Guid.NewGuid();

        _coordinatorMock
            .Setup(c => c.ExecuteMatchForRequestAsync(requestId, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(new MatchingExecutionResult
            {
                Success = false,
                Status = "Failed",
                Message = "Engine down"
            });

        // Act
        var result = await controller.StartMatching(requestId, CancellationToken.None);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, statusResult.StatusCode);
        Assert.Equal("Engine down", statusResult.Value);
    }

    [Fact]
    public async Task SyncReadyRequests_WhenNoRequestsReady_ReturnsZeroDispatched()
    {
        // Arrange
        var controller = CreateController();
        _repoMock
            .Setup(r => r.GetByStatusAsync(ServiceRequestStatus.ReadyForMatching, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceRequest>());

        // Act
        var result = await controller.SyncReadyRequests(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(okResult.Value);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(0, doc.RootElement.GetProperty("totalDispatched").GetInt32());
    }

    [Fact]
    public async Task GetPendingApprovals_WhenNoPending_ReturnsEmptyList()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.GetPendingApprovals(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var list = okResult.Value as IEnumerable<object>;
        Assert.NotNull(list);
        Assert.Empty(list);
    }

    [Fact]
    public async Task GetPendingApprovals_WithPendingExecution_ReturnsFormattedReviewItems()
    {
        // Arrange
        var controller = CreateController();
        var providerUserId = Guid.NewGuid();
        var user = new User { Id = providerUserId, FullName = "Sunil Shantha", Email = "sunil@test.com", PhoneNumber = "0771112233" };
        var provider = new ProviderProfile { Id = Guid.NewGuid(), UserId = providerUserId, BusinessName = "Sunil Plumbing", Rating = 4.8m, User = user };

        _dbContext.Users.Add(user);
        _dbContext.ProviderProfiles.Add(provider);

        var execution = new MatchingExecution
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = Guid.NewGuid(),
            ThreadId = "thread-pending-999",
            Status = MatchingExecutionStatus.PendingApproval,
            StartedAt = DateTime.UtcNow
        };
        _dbContext.MatchingExecutions.Add(execution);

        var candidate = new MatchedCandidate
        {
            Id = Guid.NewGuid(),
            MatchingExecutionId = execution.Id,
            ProviderId = provider.Id,
            Provider = provider,
            Rank = 1,
            DistanceKm = 2.5m,
            Score = 0.91m,
            MatchRationale = "Closest top-rated verified plumber.",
            Status = MatchedCandidateStatus.Recommended
        };
        _dbContext.MatchedCandidates.Add(candidate);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await controller.GetPendingApprovals(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(okResult.Value);
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.GetArrayLength() > 0);
        var first = doc.RootElement[0];
        Assert.Equal("thread-pending-999", first.GetProperty("threadId").GetString());
        Assert.Equal("Closest top-rated verified plumber.", first.GetProperty("aiRationale").GetString());
        Assert.Equal("Sunil Shantha", first.GetProperty("candidate").GetProperty("technicianName").GetString());
        Assert.Equal(2.5m, first.GetProperty("candidate").GetProperty("distanceKm").GetDecimal());
    }

    [Fact]
    public async Task ResumeMatching_WhenThreadNotFound_ReturnsNotFound()
    {
        // Arrange
        var controller = CreateController(adminId: "admin-1");

        // Act
        var result = await controller.ResumeMatching("unknown-thread", new ResumeMatchRequest("Approve"), CancellationToken.None);

        // Assert
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Contains("not found", notFound.Value?.ToString() ?? "");
    }

    [Fact]
    public async Task ResumeMatching_Approve_UpdatesExecutionToCompleted()
    {
        // Arrange
        var controller = CreateController(adminId: "admin-42");
        var threadId = "thread-approve-test";

        var execution = new MatchingExecution
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = Guid.NewGuid(),
            ThreadId = threadId,
            Status = MatchingExecutionStatus.PendingApproval,
            StartedAt = DateTime.UtcNow
        };
        _dbContext.MatchingExecutions.Add(execution);
        await _dbContext.SaveChangesAsync();

        _matchingServiceMock
            .Setup(m => m.ResumeMatchingAsync(threadId, "Approve", "admin-42"))
            .ReturnsAsync(new MatchResponse
            {
                ThreadId = threadId,
                Status = "Approved"
            });

        // Act
        var result = await controller.ResumeMatching(threadId, new ResumeMatchRequest("Approve"), CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result);

        var updated = await _dbContext.MatchingExecutions.FirstOrDefaultAsync(e => e.ThreadId == threadId);
        Assert.NotNull(updated);
        Assert.Equal(MatchingExecutionStatus.Completed, updated.Status);
        Assert.NotNull(updated.CompletedAt);
    }

    [Fact]
    public async Task ResumeMatching_Reject_UpdatesExecutionToFailedAndMarksCandidatesDeclined()
    {
        // Arrange
        var controller = CreateController(adminId: "admin-42");
        var threadId = "thread-reject-test";

        var execution = new MatchingExecution
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = Guid.NewGuid(),
            ThreadId = threadId,
            Status = MatchingExecutionStatus.PendingApproval,
            StartedAt = DateTime.UtcNow
        };
        _dbContext.MatchingExecutions.Add(execution);

        var candidate = new MatchedCandidate
        {
            Id = Guid.NewGuid(),
            MatchingExecutionId = execution.Id,
            ProviderId = Guid.NewGuid(),
            Status = MatchedCandidateStatus.Recommended
        };
        _dbContext.MatchedCandidates.Add(candidate);
        await _dbContext.SaveChangesAsync();

        _matchingServiceMock
            .Setup(m => m.ResumeMatchingAsync(threadId, "Reject", "admin-42"))
            .ReturnsAsync(new MatchResponse
            {
                ThreadId = threadId,
                Status = "Rejected"
            });

        // Act
        var result = await controller.ResumeMatching(threadId, new ResumeMatchRequest("Reject"), CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result);

        var updated = await _dbContext.MatchingExecutions.Include(e => e.Candidates).FirstOrDefaultAsync(e => e.ThreadId == threadId);
        Assert.NotNull(updated);
        Assert.Equal(MatchingExecutionStatus.Failed, updated.Status);
        Assert.Equal(MatchedCandidateStatus.Declined, updated.Candidates.First().Status);
    }

    [Fact]
    public async Task ResumeMatching_WhenServiceUnavailable_Returns503()
    {
        // Arrange
        var controller = CreateController(adminId: "admin-42");
        var threadId = "thread-503-test";

        var execution = new MatchingExecution
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = Guid.NewGuid(),
            ThreadId = threadId,
            Status = MatchingExecutionStatus.PendingApproval
        };
        _dbContext.MatchingExecutions.Add(execution);
        await _dbContext.SaveChangesAsync();

        _matchingServiceMock
            .Setup(m => m.ResumeMatchingAsync(threadId, "Approve", "admin-42"))
            .ThrowsAsync(new ApplicationException("Matching engine unavailable"));

        // Act
        var result = await controller.ResumeMatching(threadId, new ResumeMatchRequest("Approve"), CancellationToken.None);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, statusResult.StatusCode);
        Assert.Contains("service unavailable", statusResult.Value?.ToString() ?? "");
    }
}
