using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AssistLK.Api.Controllers;
using AssistLK.Application.Quotations;
using AssistLK.Application.Services.Quotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace AssistLK.Api.Tests.Quotation;

public class QuotationsControllerTests
{
    private readonly Mock<IQuotationService> _service = new();

    private QuotationsController CreateController(Guid? userId = null, string role = "Customer")
    {
        var controller = new QuotationsController(_service.Object);
        if (userId.HasValue)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()),
                new Claim(ClaimTypes.Role, role)
            };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
                }
            };
        }
        return controller;
    }

    private static QuotationDto SampleQuotation(int id = 1) =>
        new(id, Guid.NewGuid(), Guid.NewGuid(), "Draft", 1000m,
            new List<QuotationItemDto> { new(1, "Repair", 1000m, 1) },
            null, DateTime.UtcNow, DateTime.UtcNow);

    private static BookingDto SampleBooking(int id = 1, int quotationId = 1) =>
        new(id, quotationId, Guid.NewGuid(), Guid.NewGuid(), "Confirmed",
            DateTime.UtcNow, "Colombo", 6.9m, 79.8m, DateTime.UtcNow, DateTime.UtcNow);

    [Fact]
    public async Task Create_Returns201CreatedAtAction()
    {
        var providerId = Guid.NewGuid();
        _service.Setup(s => s.CreateAsync(It.IsAny<CreateQuotationDto>(), providerId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleQuotation(42));
        var controller = CreateController(providerId, "Provider");
        var dto = new CreateQuotationDto(Guid.NewGuid(), providerId,
            new List<CreateQuotationItemDto> { new("Repair", 1000m, 1) }, null);

        var result = await controller.Create(dto, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(201, created.StatusCode);
    }

    [Fact]
    public async Task GetById_Found_Returns200()
    {
        _service.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(SampleQuotation(1));
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.GetById(1, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        _service.Setup(s => s.GetByIdAsync(99)).ReturnsAsync((QuotationDto?)null);
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.GetById(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetByServiceRequest_ReturnsOkWithList()
    {
        var srId = Guid.NewGuid();
        _service.Setup(s => s.GetByServiceRequestAsync(srId))
            .ReturnsAsync(new List<QuotationDto> { SampleQuotation() });
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.GetByServiceRequest(srId, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task SendForApproval_ReturnsOkWorkflow()
    {
        var providerId = Guid.NewGuid();
        var workflow = new QuotationApprovalWorkflowDto(
            SampleQuotation(1), "thread-1", "waiting_for_approval",
            new QuotationApprovalRequestDto("quotation_approval", 1, Guid.NewGuid(), providerId, 1000m,
                new List<string> { "approve", "reject" }, "msg", null));

        _service.Setup(s => s.StartApprovalWorkflowAsync(1, providerId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflow);
        var controller = CreateController(providerId, "Provider");

        var result = await controller.SendForApproval(1, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task Approve_NullBody_Returns400()
    {
        var controller = CreateController(Guid.NewGuid());
        var result = await controller.Approve(1, null!, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Approve_MissingThreadId_Returns400()
    {
        var controller = CreateController(Guid.NewGuid());
        var result = await controller.Approve(1, new ApproveQuotationDto("ok", ""), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Approve_Valid_Returns200WithBooking()
    {
        var customerId = Guid.NewGuid();
        _service.Setup(s => s.ResumeApprovalWorkflowAsync(1, "thread-1", "approve", "ok", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _service.Setup(s => s.ApproveAsync(1, It.IsAny<ApproveQuotationDto>(), customerId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleBooking(7, 1));

        var controller = CreateController(customerId);
        var result = await controller.Approve(1, new ApproveQuotationDto("ok", "thread-1"), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task Reject_NullBody_Returns400()
    {
        var controller = CreateController(Guid.NewGuid());
        var result = await controller.Reject(1, null!, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Reject_MissingThreadId_Returns400()
    {
        var controller = CreateController(Guid.NewGuid());
        var result = await controller.Reject(1, new RejectQuotationDto("bad", ""), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Reject_Valid_Returns200()
    {
        var customerId = Guid.NewGuid();
        _service.Setup(s => s.ResumeApprovalWorkflowAsync(1, "thread-1", "reject", "no", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _service.Setup(s => s.RejectAsync(1, It.IsAny<RejectQuotationDto>(), customerId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleQuotation(1) with { Status = "Rejected" });

        var controller = CreateController(customerId);
        var result = await controller.Reject(1, new RejectQuotationDto("no", "thread-1"), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public void Controller_OnlyDependsOn_IQuotationService()
    {
        var ctors = typeof(QuotationsController).GetConstructors();
        Assert.Single(ctors);
        var paramTypes = ctors[0].GetParameters().Select(p => p.ParameterType).ToList();
        Assert.Single(paramTypes);
        Assert.Equal(typeof(IQuotationService), paramTypes[0]);
    }
}