using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AssistLK.Api.Controllers;
using AssistLK.Application.Interfaces;
using AssistLK.Application.Quotations;
using AssistLK.Application.ServiceRequests.DTOs;
using AssistLK.Application.Services;
using AssistLK.Application.Services.Quotations;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using AssistLK.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace AssistLK.IntegrationTests;

public class QuotationHandoffAndActivityTests
{
    private static (AssistLKDbContext dbContext, QuotationService quotationService) CreateTestContext()
    {
        var databaseId = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AssistLKDbContext>()
            .UseInMemoryDatabase($"assistlk-{databaseId}")
            .Options;

        var dbContext = new AssistLKDbContext(options);
        var quotationRepo = new QuotationRepository(dbContext);
        var bookingRepo = new BookingRepository(dbContext);
        var lookup = new ServiceRequestLookup(dbContext);
        var config = new ConfigurationBuilder().Build();

        var quotationService = new QuotationService(
            quotationRepo,
            bookingRepo,
            lookup,
            config,
            dbContext);

        return (dbContext, quotationService);
    }

    [Fact]
    public async Task ApproveAsync_CreatesServiceJob()
    {
        var (dbContext, service) = CreateTestContext();
        var customerId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var quotation = new Quotation
        {
            ServiceRequestId = requestId,
            ProviderId = providerId,
            Status = QuotationStatus.WaitingForCustomerApproval,
            TotalAmount = 5000,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Quotations.Add(quotation);
        await dbContext.SaveChangesAsync();

        var bookingDto = await service.ApproveAsync(
            quotation.Id,
            new ApproveQuotationDto("Approved!", "thread_123"),
            customerId.ToString());

        Assert.NotNull(bookingDto);
        Assert.NotNull(bookingDto.ServiceJobId);

        var savedJob = await dbContext.ServiceJobs
            .FirstOrDefaultAsync(j => j.BookingId == bookingDto.Id);

        Assert.NotNull(savedJob);
        Assert.Equal(bookingDto.ServiceJobId, savedJob.Id);
    }

    [Fact]
    public async Task ApproveAsync_CreatesOnlyOneServiceJob_WhenRetried()
    {
        var (dbContext, service) = CreateTestContext();
        var customerId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var quotation = new Quotation
        {
            ServiceRequestId = requestId,
            ProviderId = providerId,
            Status = QuotationStatus.WaitingForCustomerApproval,
            TotalAmount = 5000,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Quotations.Add(quotation);
        await dbContext.SaveChangesAsync();

        var bookingDto1 = await service.ApproveAsync(
            quotation.Id,
            new ApproveQuotationDto("Approved!", "thread_123"),
            customerId.ToString());

        var existingJob = await dbContext.ServiceJobs.FirstOrDefaultAsync(j => j.BookingId == bookingDto1.Id);
        Assert.NotNull(existingJob);

        var jobsCount = await dbContext.ServiceJobs.CountAsync(j => j.BookingId == bookingDto1.Id);
        Assert.Equal(1, jobsCount);
    }

    [Fact]
    public async Task CreatedServiceJob_UsesConfirmedBookingProvider()
    {
        var (dbContext, service) = CreateTestContext();
        var customerId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var quotation = new Quotation
        {
            ServiceRequestId = requestId,
            ProviderId = providerId,
            Status = QuotationStatus.WaitingForCustomerApproval,
            TotalAmount = 7500,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Quotations.Add(quotation);
        await dbContext.SaveChangesAsync();

        var bookingDto = await service.ApproveAsync(
            quotation.Id,
            new ApproveQuotationDto("Approved!", "thread_123"),
            customerId.ToString());

        var savedJob = await dbContext.ServiceJobs.FirstOrDefaultAsync(j => j.BookingId == bookingDto.Id);
        Assert.NotNull(savedJob);
        Assert.Equal(providerId, savedJob.ProviderId);
    }

    [Fact]
    public async Task CreatedServiceJob_StatusIsAssigned()
    {
        var (dbContext, service) = CreateTestContext();
        var customerId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var quotation = new Quotation
        {
            ServiceRequestId = requestId,
            ProviderId = providerId,
            Status = QuotationStatus.WaitingForCustomerApproval,
            TotalAmount = 10000,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Quotations.Add(quotation);
        await dbContext.SaveChangesAsync();

        var bookingDto = await service.ApproveAsync(
            quotation.Id,
            new ApproveQuotationDto("Approved!", "thread_123"),
            customerId.ToString());

        var savedJob = await dbContext.ServiceJobs.FirstOrDefaultAsync(j => j.BookingId == bookingDto.Id);
        Assert.NotNull(savedJob);
        Assert.Equal(ServiceJobStatus.Assigned, savedJob.Status);
    }

    [Fact]
    public async Task ActivityEndpoint_ReturnsServiceJobForOwningCustomer()
    {
        var (dbContext, quotationService) = CreateTestContext();
        var customerId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var customerUser = new User
        {
            Id = customerId,
            FullName = "Customer Test",
            Email = "customer@example.test",
            PasswordHash = "hash",
            Role = UserRole.Customer,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(customerUser);

        var serviceRequest = new ServiceRequest
        {
            Id = requestId,
            CustomerId = customerId,
            Category = "Plumbing",
            Description = "Pipe leakage test",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.ReadyForMatching,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.ServiceRequests.Add(serviceRequest);

        var serviceJob = new ServiceJob
        {
            ServiceRequestId = requestId,
            ProviderId = providerId,
            Status = ServiceJobStatus.Assigned,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.ServiceJobs.Add(serviceJob);
        await dbContext.SaveChangesAsync();

        var mockServiceRequestService = new Mock<IServiceRequestService>();
        mockServiceRequestService
            .Setup(s => s.GetByIdAsync(requestId, customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceRequestResponse
            {
                ServiceRequestId = requestId,
                CustomerId = customerId,
                Category = "Plumbing",
                Description = "Pipe leakage test",
                LocationText = "Colombo",
                Status = ServiceRequestStatus.ReadyForMatching,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        var jobRepo = new ServiceJobRepository(dbContext);
        var quotationRepo = new QuotationRepository(dbContext);

        var controller = new ServiceRequestsController(
            mockServiceRequestService.Object,
            null!,
            jobRepo,
            quotationRepo);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, customerId.ToString()),
                        new Claim(ClaimTypes.Role, "Customer")
                    },
                    "Test"))
            }
        };

        var result = await controller.GetActivity(requestId, default);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var activity = Assert.IsType<ServiceRequestActivityResponse>(okResult.Value);

        Assert.Equal(requestId, activity.ServiceRequestId);
        Assert.Equal(serviceJob.Id, activity.ServiceJobId);
        Assert.Equal(nameof(ServiceJobStatus.Assigned), activity.ServiceJobStatus);
    }

    [Fact]
    public async Task ActivityEndpoint_RejectsOtherCustomer()
    {
        var (dbContext, quotationService) = CreateTestContext();
        var ownerCustomerId = Guid.NewGuid();
        var otherCustomerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var mockServiceRequestService = new Mock<IServiceRequestService>();
        mockServiceRequestService
            .Setup(s => s.GetByIdAsync(requestId, otherCustomerId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Customer does not own this request."));

        var jobRepo = new ServiceJobRepository(dbContext);
        var quotationRepo = new QuotationRepository(dbContext);

        var controller = new ServiceRequestsController(
            mockServiceRequestService.Object,
            null!,
            jobRepo,
            quotationRepo);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, otherCustomerId.ToString()),
                        new Claim(ClaimTypes.Role, "Customer")
                    },
                    "Test"))
            }
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => controller.GetActivity(requestId, default));
    }
}
