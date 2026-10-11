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

public class BookingsControllerTests
{
    private readonly Mock<IQuotationService> _service = new();

    private BookingsController CreateController()
    {
        var controller = new BookingsController(_service.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
                }, "TestAuth"))
            }
        };
        return controller;
    }

    [Fact]
    public async Task GetById_Found_Returns200()
    {
        var booking = new BookingDto(1, 1, Guid.NewGuid(), Guid.NewGuid(), "Confirmed",
            DateTime.UtcNow, "Colombo", 6.9m, 79.8m, DateTime.UtcNow, DateTime.UtcNow);
        _service.Setup(s => s.GetBookingByIdAsync(1)).ReturnsAsync(booking);

        var result = await CreateController().GetById(1, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        _service.Setup(s => s.GetBookingByIdAsync(99)).ReturnsAsync((BookingDto?)null);

        var result = await CreateController().GetById(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetStatusHistory_Returns200WithList()
    {
        _service.Setup(s => s.GetBookingStatusHistoryAsync(1))
            .ReturnsAsync(new List<BookingStatusHistoryDto>
            {
                new(1, 1, "Confirmed", "Confirmed", Guid.NewGuid().ToString(), "created", DateTime.UtcNow)
            });

        var result = await CreateController().GetStatusHistory(1, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }
}