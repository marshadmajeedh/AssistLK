using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssistLK.Application.Interfaces;
using AssistLK.Application.Quotations;
using AssistLK.Application.Services.Quotations;
using AssistLK.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using QuotationEntity = AssistLK.Domain.Entities.Quotation;

namespace AssistLK.Api.Tests.Quotation;

public class QuotationServiceTests
{
    private static QuotationService CreateService(
        Mock<IQuotationRepository>? quotationRepo = null,
        Mock<IBookingRepository>? bookingRepo = null,
        Mock<IServiceRequestLookup>? lookup = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AgentServices:QuotationBookingUrl"] = "http://localhost:8002"
            })
            .Build();

        return new QuotationService(
            (quotationRepo ?? new Mock<IQuotationRepository>()).Object,
            (bookingRepo ?? new Mock<IBookingRepository>()).Object,
            (lookup ?? new Mock<IServiceRequestLookup>()).Object,
            config);
    }

    // -----------------------------------------------------------------
    // CreateAsync
    // -----------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_WithValidItems_ComputesTotalAndPersists()
    {
        var repo = new Mock<IQuotationRepository>();
        QuotationEntity? captured = null;

        repo.Setup(r => r.AddAsync(It.IsAny<QuotationEntity>(), It.IsAny<CancellationToken>()))
            .Callback<QuotationEntity, CancellationToken>((q, _) => captured = q)
            .Returns(Task.CompletedTask);

        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(repo);
        var dto = new CreateQuotationDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new List<CreateQuotationItemDto>
            {
                new("Visit charge", 1000m, 1),
                new("Repair", 2500m, 2)
            },
            "Bring spare parts");

        var result = await service.CreateAsync(dto, Guid.NewGuid().ToString());

        Assert.NotNull(captured);
        Assert.Equal(6000m, captured!.TotalAmount);
        Assert.Equal(QuotationStatus.Draft, captured.Status);
        Assert.Equal(2, captured.Items.Count);
        Assert.Equal(6000m, result.TotalAmount);
        Assert.Equal("Draft", result.Status);
    }

    [Fact]
    public async Task CreateAsync_WithEmptyItems_ThrowsArgumentException()
    {
        var service = CreateService();
        var dto = new CreateQuotationDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new List<CreateQuotationItemDto>(),
            null);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(dto, Guid.NewGuid().ToString()));
    }

    [Fact]
    public async Task CreateAsync_WithNullItems_ThrowsArgumentException()
    {
        var service = CreateService();
        var dto = new CreateQuotationDto(Guid.NewGuid(), Guid.NewGuid(), null!, null);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(dto, Guid.NewGuid().ToString()));
    }

    [Fact]
    public async Task CreateAsync_WithNonPositiveAmount_ThrowsArgumentException()
    {
        var service = CreateService();
        var dto = new CreateQuotationDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new List<CreateQuotationItemDto> { new("Repair", 0m, 1) },
            null);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(dto, Guid.NewGuid().ToString()));
    }

    [Fact]
    public async Task CreateAsync_WithNonPositiveQuantity_ThrowsArgumentException()
    {
        var service = CreateService();
        var dto = new CreateQuotationDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new List<CreateQuotationItemDto> { new("Repair", 100m, 0) },
            null);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(dto, Guid.NewGuid().ToString()));
    }

    [Fact]
    public async Task CreateAsync_WithInvalidProviderGuid_ThrowsArgumentException()
    {
        var service = CreateService();
        var dto = new CreateQuotationDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new List<CreateQuotationItemDto> { new("Repair", 100m, 1) },
            null);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(dto, "not-a-guid"));
    }

    // -----------------------------------------------------------------
    // ApproveAsync
    // -----------------------------------------------------------------

    [Fact]
    public async Task ApproveAsync_WhenNotFound_ThrowsKeyNotFoundException()
    {
        var repo = new Mock<IQuotationRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((QuotationEntity?)null);

        var service = CreateService(repo);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ApproveAsync(1, new ApproveQuotationDto("ok", "thread-1"), Guid.NewGuid().ToString()));
    }

    [Fact]
    public async Task ApproveAsync_WhenStatusNotWaiting_ThrowsInvalidOperationException()
    {
        var quotation = new QuotationEntity
        {
            Id = 1,
            Status = QuotationStatus.Draft,
            Items = new List<QuotationItem>
            {
                new() { Description = "x", Amount = 1m, Quantity = 1 }
            }
        };

        var repo = new Mock<IQuotationRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quotation);

        var service = CreateService(repo);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApproveAsync(1, new ApproveQuotationDto("ok", "thread-1"), Guid.NewGuid().ToString()));
    }

    [Fact]
    public async Task ApproveAsync_HappyPath_CreatesBookingAndHistory_SetsApproved()
    {
        var quotation = new QuotationEntity
        {
            Id = 1,
            ProviderId = Guid.NewGuid(),
            ServiceRequestId = Guid.NewGuid(),
            Status = QuotationStatus.WaitingForCustomerApproval,
            TotalAmount = 5000m,
            Items = new List<QuotationItem>
            {
                new() { Description = "Repair", Amount = 5000m, Quantity = 1 }
            }
        };

        var quotationRepo = new Mock<IQuotationRepository>();
        quotationRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quotation);
        quotationRepo.Setup(r => r.Update(It.IsAny<QuotationEntity>()));
        quotationRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var bookingRepo = new Mock<IBookingRepository>();
        Booking? capturedBooking = null;
        BookingStatusHistory? capturedHistory = null;

        bookingRepo.Setup(r => r.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()))
            .Callback<Booking, CancellationToken>((b, _) => capturedBooking = b)
            .Returns(Task.CompletedTask);

        bookingRepo.Setup(r => r.AddStatusHistoryAsync(It.IsAny<BookingStatusHistory>(), It.IsAny<CancellationToken>()))
            .Callback<BookingStatusHistory, CancellationToken>((h, _) => capturedHistory = h)
            .Returns(Task.CompletedTask);

        bookingRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var lookup = new Mock<IServiceRequestLookup>();
        lookup.Setup(l => l.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceRequest?)null);

        var service = CreateService(quotationRepo, bookingRepo, lookup);
        var customerId = Guid.NewGuid();

        var result = await service.ApproveAsync(
            1,
            new ApproveQuotationDto("Looks good", "thread-1"),
            customerId.ToString());

        Assert.Equal(QuotationStatus.Approved, quotation.Status);
        Assert.NotNull(capturedBooking);
        Assert.Equal(quotation.Id, capturedBooking!.QuotationId);
        Assert.Equal(customerId, capturedBooking.CustomerId);
        Assert.Equal(BookingStatus.Confirmed, capturedBooking.Status);
        Assert.NotNull(capturedHistory);
        Assert.Equal(capturedBooking.Id, capturedHistory!.BookingId);
        Assert.Equal(customerId, capturedHistory.ChangedByUserId);
        Assert.Equal("Confirmed", result.Status);
    }

    // -----------------------------------------------------------------
    // RejectAsync
    // -----------------------------------------------------------------

    [Fact]
    public async Task RejectAsync_WhenNotFound_ThrowsKeyNotFoundException()
    {
        var repo = new Mock<IQuotationRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((QuotationEntity?)null);

        var service = CreateService(repo);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.RejectAsync(1, new RejectQuotationDto("Too expensive", "thread-1"), Guid.NewGuid().ToString()));
    }

    [Fact]
    public async Task RejectAsync_WhenStatusNotWaiting_ThrowsInvalidOperationException()
    {
        var quotation = new QuotationEntity
        {
            Id = 1,
            Status = QuotationStatus.Approved
        };

        var repo = new Mock<IQuotationRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quotation);

        var service = CreateService(repo);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RejectAsync(1, new RejectQuotationDto("no", "thread-1"), Guid.NewGuid().ToString()));
    }

    [Fact]
    public async Task RejectAsync_HappyPath_SetsRejected()
    {
        var quotation = new QuotationEntity
        {
            Id = 1,
            Status = QuotationStatus.WaitingForCustomerApproval,
            Items = new List<QuotationItem>
            {
                new() { Description = "x", Amount = 1m, Quantity = 1 }
            }
        };

        var repo = new Mock<IQuotationRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quotation);
        repo.Setup(r => r.Update(It.IsAny<QuotationEntity>()));
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(repo);

        var result = await service.RejectAsync(
            1,
            new RejectQuotationDto("Too expensive", "thread-1"),
            Guid.NewGuid().ToString());

        Assert.Equal(QuotationStatus.Rejected, quotation.Status);
        Assert.Equal("Rejected", result.Status);
    }
}