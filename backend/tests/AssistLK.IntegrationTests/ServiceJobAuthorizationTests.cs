using System.Security.Claims;
using AssistLK.Api.Controllers;
using AssistLK.Application.Services;
using AssistLK.Domain.Entities;
using AssistLK.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssistLK.IntegrationTests;

public class ServiceJobAuthorizationTests
{
    private static ServiceJobsController CreateController(
        AssistLKDbContext db, ClaimsPrincipal user)
    {
        var controller = new ServiceJobsController(
            db,
            new HttpClient(),
            proofOfWorkStorage: null!,
            trackingHubContext: null!,
            new FeedbackApplicationService(db, db),
            new AgentWorkflowService(db),
            new AgentMonitoringService(db),
            NullLogger<ServiceJobsController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
        return controller;
    }

    private static AssistLKDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AssistLKDbContext>()
            .UseInMemoryDatabase($"assistlk-{Guid.NewGuid()}")
            .Options);

    [Fact]
    public async Task UpdateJobStatus_UnknownJob_ReturnsNotFound()
    {
        await using var db = CreateDb();
        var controller = CreateController(db, new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "Admin")], "Test")));

        var result = await controller.UpdateJobStatus(
            Guid.NewGuid(),
            new StatusUpdateRequest { NewStatus = nameof(ServiceJobStatus.OnTheWay) });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task UpdateJobStatus_InvalidStatusValue_ReturnsBadRequest()
    {
        await using var db = CreateDb();
        var jobId = Guid.NewGuid();
        db.ServiceJobs.Add(new ServiceJob { Id = jobId });
        await db.SaveChangesAsync();
        var controller = CreateController(db, new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "Admin")], "Test")));

        var result = await controller.UpdateJobStatus(
            jobId, new StatusUpdateRequest { NewStatus = "Banana" });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateJobStatus_CustomerRole_IsForbidden()
    {
        await using var db = CreateDb();
        var jobId = Guid.NewGuid();
        db.ServiceJobs.Add(new ServiceJob { Id = jobId });
        await db.SaveChangesAsync();
        var controller = CreateController(db, new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "Customer"),
             new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "Test")));

        var result = await controller.UpdateJobStatus(
            jobId,
            new StatusUpdateRequest { NewStatus = nameof(ServiceJobStatus.OnTheWay) });

        Assert.IsType<ForbidResult>(result);
    }
}