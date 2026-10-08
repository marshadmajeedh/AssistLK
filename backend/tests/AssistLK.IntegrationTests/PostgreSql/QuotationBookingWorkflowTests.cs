using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AssistLK.Application.Quotations;
using AssistLK.Application.Services.Quotations;
using AssistLK.Domain.Entities;
using AssistLK.Infrastructure.Data;
using AssistLK.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AssistLK.IntegrationTests.PostgreSql;

public class QuotationBookingWorkflowTests : PostgreSqlIntegrationTestBase
{
    public QuotationBookingWorkflowTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    private static IConfiguration BuildConfig()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AgentServices:QuotationBookingUrl"] = "http://localhost:8002"
            })
            .Build();

    // Each call creates a fresh DbContext so we mirror the scoped-request
    // lifetime used in production. Never reuse a service across contexts.
    private QuotationService CreateService(AssistLKDbContext context)
    {
        var quotationRepo = new QuotationRepository(context);
        var bookingRepo = new BookingRepository(context);
        var lookup = new ServiceRequestLookup(context);
        return new QuotationService(quotationRepo, bookingRepo, lookup, BuildConfig());
    }

    private async Task TransitionToWaitingAsync(int quotationId)
    {
        await using var ctx = CreateDbContext();
        var quotation = await ctx.Quotations.FirstAsync(q => q.Id == quotationId);
        quotation.Status = QuotationStatus.WaitingForCustomerApproval;
        quotation.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    // ------------------------------------------------------------------
    // 1. Create → Waiting → Approve → Booking + History
    // ------------------------------------------------------------------

    [Fact]
    public async Task Create_ThenApprove_CreatesBookingAndHistory_AndSetsApproved()
    {
        var customer = await CreateUserAsync();
        var providerId = Guid.NewGuid();
        var srId = Guid.NewGuid();

        // Phase 1: create the quotation with a fresh context/service.
        QuotationDto created;
        await using (var ctx = CreateDbContext())
        {
            var service = CreateService(ctx);
            created = await service.CreateAsync(
                new CreateQuotationDto(
                    srId,
                    providerId,
                    new List<CreateQuotationItemDto>
                    {
                        new("Visit charge", 1000m, 1),
                        new("Repair",       2500m, 2)
                    },
                    "Bring spare parts"),
                providerId.ToString());
        }

        Assert.Equal("Draft", created.Status);
        Assert.Equal(6000m, created.TotalAmount);

        // Phase 2: transition to WaitingForCustomerApproval (simulates the
        // provider calling /send-for-approval, without hitting Python).
        await TransitionToWaitingAsync(created.Id);

        // Phase 3: approve with a fresh context/service.
        BookingDto booking;
        await using (var ctx = CreateDbContext())
        {
            var service = CreateService(ctx);
            booking = await service.ApproveAsync(
                created.Id,
                new ApproveQuotationDto("Looks good", "thread-integration-1"),
                customer.Id.ToString());
        }

        Assert.NotNull(booking);
        Assert.Equal(BookingStatus.Confirmed.ToString(), booking.Status);
        Assert.Equal(customer.Id, booking.CustomerId);

        // Phase 4: verify persistence with yet another fresh context.
        await using var verify = CreateDbContext();

        var persistedQuotation = await verify.Quotations
            .Include(q => q.Items)
            .FirstAsync(q => q.Id == created.Id);

        Assert.Equal(QuotationStatus.Approved, persistedQuotation.Status);
        Assert.Equal(6000m, persistedQuotation.TotalAmount);
        Assert.Equal(2, persistedQuotation.Items.Count);

        var persistedBooking = await verify.Bookings
            .Include(b => b.StatusHistory)
            .FirstAsync(b => b.Id == booking.Id);

        Assert.Equal(created.Id, persistedBooking.QuotationId);
        Assert.Equal(customer.Id, persistedBooking.CustomerId);
        Assert.Single(persistedBooking.StatusHistory);
        Assert.Equal(customer.Id, persistedBooking.StatusHistory.First().ChangedByUserId);
    }

    // ------------------------------------------------------------------
    // 2. Duplicate approval
    // ------------------------------------------------------------------

    [Fact]
    public async Task Approve_Twice_SecondAttemptThrowsAndDoesNotCreateSecondBooking()
    {
        var customer = await CreateUserAsync();
        var providerId = Guid.NewGuid();

        QuotationDto created;
        await using (var ctx = CreateDbContext())
        {
            var service = CreateService(ctx);
            created = await service.CreateAsync(
                new CreateQuotationDto(
                    Guid.NewGuid(),
                    providerId,
                    new List<CreateQuotationItemDto> { new("Repair", 1000m, 1) },
                    null),
                providerId.ToString());
        }

        await TransitionToWaitingAsync(created.Id);

        // First approval
        await using (var ctx = CreateDbContext())
        {
            var service = CreateService(ctx);
            await service.ApproveAsync(
                created.Id,
                new ApproveQuotationDto("ok", "t1"),
                customer.Id.ToString());
        }

        // Second approval — must throw because status is now Approved
        await using (var ctx = CreateDbContext())
        {
            var service = CreateService(ctx);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ApproveAsync(
                    created.Id,
                    new ApproveQuotationDto("again", "t1"),
                    customer.Id.ToString()));
        }

        await using var verify = CreateDbContext();
        var count = await verify.Bookings.CountAsync(b => b.QuotationId == created.Id);
        Assert.Equal(1, count);
    }

    // ------------------------------------------------------------------
    // 3. Reject path
    // ------------------------------------------------------------------

    [Fact]
    public async Task Reject_AfterCreate_TransitionsToRejected_AndCreatesNoBooking()
    {
        var customer = await CreateUserAsync();
        var providerId = Guid.NewGuid();

        QuotationDto created;
        await using (var ctx = CreateDbContext())
        {
            var service = CreateService(ctx);
            created = await service.CreateAsync(
                new CreateQuotationDto(
                    Guid.NewGuid(),
                    providerId,
                    new List<CreateQuotationItemDto> { new("Repair", 8000m, 1) },
                    null),
                providerId.ToString());
        }

        await TransitionToWaitingAsync(created.Id);

        QuotationDto rejected;
        await using (var ctx = CreateDbContext())
        {
            var service = CreateService(ctx);
            rejected = await service.RejectAsync(
                created.Id,
                new RejectQuotationDto("Too expensive", "t1"),
                customer.Id.ToString());
        }

        Assert.Equal("Rejected", rejected.Status);

        await using var verify = CreateDbContext();
        Assert.Equal(QuotationStatus.Rejected, (await verify.Quotations.FirstAsync(q => q.Id == created.Id)).Status);
        Assert.Empty(await verify.Bookings.Where(b => b.QuotationId == created.Id).ToListAsync());
    }

    // ------------------------------------------------------------------
    // 4. Booking read APIs
    // ------------------------------------------------------------------

    [Fact]
    public async Task GetBookingAndHistory_AfterApproval_ReturnPersistedRows()
    {
        var customer = await CreateUserAsync();
        var providerId = Guid.NewGuid();

        QuotationDto created;
        await using (var ctx = CreateDbContext())
        {
            var service = CreateService(ctx);
            created = await service.CreateAsync(
                new CreateQuotationDto(
                    Guid.NewGuid(),
                    providerId,
                    new List<CreateQuotationItemDto> { new("Repair", 2000m, 1) },
                    null),
                providerId.ToString());
        }

        await TransitionToWaitingAsync(created.Id);

        BookingDto booking;
        await using (var ctx = CreateDbContext())
        {
            var service = CreateService(ctx);
            booking = await service.ApproveAsync(
                created.Id,
                new ApproveQuotationDto("ok", "t1"),
                customer.Id.ToString());
        }

        await using (var ctx = CreateDbContext())
        {
            var service = CreateService(ctx);

            var loaded = await service.GetBookingByIdAsync(booking.Id);
            Assert.NotNull(loaded);
            Assert.Equal(booking.Id, loaded!.Id);

            var history = (await service.GetBookingStatusHistoryAsync(booking.Id)).ToList();
            Assert.NotEmpty(history);
            Assert.Equal(BookingStatus.Confirmed.ToString(), history[0].NewStatus);
        }
    }
}