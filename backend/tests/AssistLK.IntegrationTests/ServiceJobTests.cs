using System.Net;
using System.Security.Claims;
using System.Text;
using AssistLK.Api.Controllers;
using AssistLK.Application.Services;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssistLK.IntegrationTests;

public class ServiceJobTests
{
    [Fact]
    public async Task UpdateJobStatus_AssignedDirectlyToCompleted_IsRejectedByValidationAgent()
    {
        var databaseId = Guid.NewGuid().ToString();
        var applicationOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"service-jobs-{databaseId}")
            .Options;
        var assistLkOptions = new DbContextOptionsBuilder<AssistLKDbContext>()
            .UseInMemoryDatabase($"assistlk-{databaseId}")
            .Options;

        await using var applicationDbContext = new ApplicationDbContext(applicationOptions);
        await using var assistLkDbContext = new AssistLKDbContext(assistLkOptions);

        var jobId = Guid.NewGuid();
        applicationDbContext.ServiceJobs.Add(new ServiceJob
        {
            Id = jobId,
            Status = ServiceJobStatus.Assigned
        });
        await applicationDbContext.SaveChangesAsync();

        using var httpClient = new HttpClient(new FixedResponseHandler(
            HttpStatusCode.OK,
            "{\"status\":\"INVALID\",\"reason\":\"Assigned jobs cannot be completed directly.\"}"));
        var feedbackService = new FeedbackApplicationService(
            applicationDbContext,
            assistLkDbContext);
        var controller = new ServiceJobsController(
            applicationDbContext,
            assistLkDbContext,
            httpClient,
            proofOfWorkStorage: null!,
            trackingHubContext: null!,
            feedbackService,
            new AgentWorkflowService(assistLkDbContext),
            new AgentMonitoringService(assistLkDbContext),
            NullLogger<ServiceJobsController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.Role, "Admin")],
                    "Test"))
            }
        };

        var result = await controller.UpdateJobStatus(
            jobId,
            new StatusUpdateRequest
            {
                NewStatus = nameof(ServiceJobStatus.Completed)
            });

        Assert.IsType<BadRequestObjectResult>(result);
        var savedJob = await applicationDbContext.ServiceJobs
            .AsNoTracking()
            .SingleAsync(job => job.Id == jobId);
        Assert.Equal(ServiceJobStatus.Assigned, savedJob.Status);
        Assert.Empty(await applicationDbContext.ServiceStatusHistories
            .Where(history => history.ServiceJobId == jobId)
            .ToListAsync());
    }

    [Fact]
    public async Task SubmitFeedback_TwiceForSameJob_ThrowsDuplicateFeedbackException()
    {
        var databaseId = Guid.NewGuid().ToString();
        var applicationOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"service-jobs-{databaseId}")
            .Options;
        var assistLkOptions = new DbContextOptionsBuilder<AssistLKDbContext>()
            .UseInMemoryDatabase($"assistlk-{databaseId}")
            .Options;

        await using var applicationDbContext = new ApplicationDbContext(applicationOptions);
        await using var assistLkDbContext = new AssistLKDbContext(assistLkOptions);

        var providerId = Guid.NewGuid();
        var providerUserId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        assistLkDbContext.Users.Add(new User
        {
            Id = providerUserId,
            FullName = "Test Provider",
            Email = "provider@example.test",
            PasswordHash = "test-hash",
            Role = UserRole.Provider,
            CreatedAt = now,
            UpdatedAt = now
        });
        assistLkDbContext.ProviderProfiles.Add(new ProviderProfile
        {
            Id = providerId,
            UserId = providerUserId,
            BusinessName = "Test Services",
            CreatedAt = now,
            UpdatedAt = now
        });
        await assistLkDbContext.SaveChangesAsync();

        var jobId = Guid.NewGuid();
        applicationDbContext.ServiceJobs.Add(new ServiceJob
        {
            Id = jobId,
            ProviderId = providerId,
            Status = ServiceJobStatus.Completed
        });
        await applicationDbContext.SaveChangesAsync();

        var feedbackService = new FeedbackApplicationService(
            applicationDbContext,
            assistLkDbContext);
        var command = new SubmitFeedbackCommand(
            Guid.NewGuid(),
            Rating: 5,
            Comment: "Excellent service.",
            AutoEscalateToComplaint: false,
            Sentiment: "POSITIVE");

        await feedbackService.SubmitAsync(jobId, command);

        await Assert.ThrowsAsync<DuplicateFeedbackException>(
            () => feedbackService.SubmitAsync(jobId, command));
    }

    private sealed class FixedResponseHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _body;

        public FixedResponseHandler(HttpStatusCode statusCode, string body)
        {
            _statusCode = statusCode;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
        }
    }
}