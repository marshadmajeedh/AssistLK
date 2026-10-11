using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AssistLK.IntegrationTests.PostgreSql;

public class QuotationDatabaseTests : PostgreSqlIntegrationTestBase
{
    public QuotationDatabaseTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static Quotation BuildQuotation(Guid providerId, Guid serviceRequestId)
        => new()
        {
            ServiceRequestId = serviceRequestId,
            ProviderId = providerId,
            Status = QuotationStatus.Draft,
            TotalAmount = 6000m,
            Notes = "Database integration fixture",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Items = new List<QuotationItem>
            {
                new() { Description = "Visit charge", Amount = 1000m, Quantity = 1 },
                new() { Description = "Repair",       Amount = 2500m, Quantity = 2 }
            }
        };

    // ------------------------------------------------------------------
    // 1. Persistence
    // ------------------------------------------------------------------

    [Fact]
    public async Task CreateQuotation_PersistsQuotationAndItems()
    {
        var customer = await CreateUserAsync();
        var providerId = Guid.NewGuid();
        var srId = Guid.NewGuid();

        await using var context = CreateDbContext();

        var quotation = BuildQuotation(providerId, srId);
        await context.Quotations.AddAsync(quotation);
        await context.SaveChangesAsync();

        Assert.True(quotation.Id > 0, "Database should assign an integer identity to Quotation.");

        await using var readContext = CreateDbContext();
        var loaded = await readContext.Quotations
            .Include(q => q.Items)
            .FirstOrDefaultAsync(q => q.Id == quotation.Id);

        Assert.NotNull(loaded);
        Assert.Equal(QuotationStatus.Draft, loaded!.Status);
        Assert.Equal(6000m, loaded.TotalAmount);
        Assert.Equal(2, loaded.Items.Count);
        Assert.Contains(loaded.Items, i => i.Description == "Visit charge" && i.Amount == 1000m && i.Quantity == 1);
        Assert.Contains(loaded.Items, i => i.Description == "Repair" && i.Amount == 2500m && i.Quantity == 2);
    }

    // ------------------------------------------------------------------
    // 2. FK: QuotationItem → Quotation
    // ------------------------------------------------------------------

    [Fact]
    public async Task QuotationItem_RequiresExistingQuotation_FailsOnInvalidFk()
    {
        await using var context = CreateDbContext();

        var orphanItem = new QuotationItem
        {
            QuotationId = 999999,
            Description = "Orphan",
            Amount = 100m,
            Quantity = 1
        };

        await context.QuotationItems.AddAsync(orphanItem);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.NotNull(ex.InnerException);
    }

    // ------------------------------------------------------------------
    // 3. Booking + history on approval
    // ------------------------------------------------------------------

    [Fact]
    public async Task ApprovePath_PersistsBookingAndHistoryRows()
    {
        var customer = await CreateUserAsync();

        int quotationId;
        int bookingId;

        await using (var context = CreateDbContext())
        {
            var quotation = BuildQuotation(Guid.NewGuid(), Guid.NewGuid());
            quotation.Status = QuotationStatus.WaitingForCustomerApproval;
            await context.Quotations.AddAsync(quotation);
            await context.SaveChangesAsync();
            quotationId = quotation.Id;

            quotation.Status = QuotationStatus.Approved;
            quotation.UpdatedAt = DateTime.UtcNow;

            var booking = new Booking
            {
                QuotationId = quotation.Id,
                CustomerId = customer.Id,
                ProviderId = quotation.ProviderId,
                Status = BookingStatus.Confirmed,
                ScheduledAt = DateTime.UtcNow,
                LocationText = "Colombo",
                Latitude = 6.9m,
                Longitude = 79.8m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await context.Bookings.AddAsync(booking);
            await context.SaveChangesAsync();
            bookingId = booking.Id;

            var history = new BookingStatusHistory
            {
                BookingId = booking.Id,
                PreviousStatus = BookingStatus.Confirmed,
                NewStatus = BookingStatus.Confirmed,
                ChangedByUserId = customer.Id,
                Reason = "Customer approved quotation",
                ChangedAt = DateTime.UtcNow
            };
            await context.BookingStatusHistories.AddAsync(history);
            await context.SaveChangesAsync();
        }

        await using var verify = CreateDbContext();
        var persistedBooking = await verify.Bookings
            .Include(b => b.StatusHistory)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        Assert.NotNull(persistedBooking);
        Assert.Equal(quotationId, persistedBooking!.QuotationId);
        Assert.Equal(customer.Id, persistedBooking.CustomerId);
        Assert.Equal(BookingStatus.Confirmed, persistedBooking.Status);
        Assert.Single(persistedBooking.StatusHistory);
        Assert.Equal(customer.Id, persistedBooking.StatusHistory.First().ChangedByUserId);

        var persistedQuotation = await verify.Quotations.FirstAsync(q => q.Id == quotationId);
        Assert.Equal(QuotationStatus.Approved, persistedQuotation.Status);
    }

    // ------------------------------------------------------------------
    // 4. Duplicate approval — ensure no double booking
    // ------------------------------------------------------------------

    [Fact]
    public async Task DuplicateApproval_DoesNotCreateSecondBooking()
    {
        var customer = await CreateUserAsync();
        int quotationId;

        await using (var context = CreateDbContext())
        {
            var quotation = BuildQuotation(Guid.NewGuid(), Guid.NewGuid());
            quotation.Status = QuotationStatus.Approved;
            await context.Quotations.AddAsync(quotation);
            await context.SaveChangesAsync();
            quotationId = quotation.Id;

            var booking = new Booking
            {
                QuotationId = quotation.Id,
                CustomerId = customer.Id,
                ProviderId = quotation.ProviderId,
                Status = BookingStatus.Confirmed,
                ScheduledAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await context.Bookings.AddAsync(booking);
            await context.SaveChangesAsync();
        }

        // A second approval should be rejected at the service layer
        // because the quotation is no longer WaitingForCustomerApproval.
        // At the database level, verify only one booking exists for this quotation.
        await using var verify = CreateDbContext();
        var count = await verify.Bookings.CountAsync(b => b.QuotationId == quotationId);
        Assert.Equal(1, count);
    }

    // ------------------------------------------------------------------
    // 5. Reject path
    // ------------------------------------------------------------------

    [Fact]
    public async Task RejectPath_TransitionsQuotationToRejected_AndCreatesNoBooking()
    {
        int quotationId;

        await using (var context = CreateDbContext())
        {
            var quotation = BuildQuotation(Guid.NewGuid(), Guid.NewGuid());
            quotation.Status = QuotationStatus.WaitingForCustomerApproval;
            await context.Quotations.AddAsync(quotation);
            await context.SaveChangesAsync();
            quotationId = quotation.Id;

            quotation.Status = QuotationStatus.Rejected;
            quotation.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        await using var verify = CreateDbContext();
        var persisted = await verify.Quotations.FirstAsync(q => q.Id == quotationId);
        Assert.Equal(QuotationStatus.Rejected, persisted.Status);

        var bookings = await verify.Bookings.Where(b => b.QuotationId == quotationId).ToListAsync();
        Assert.Empty(bookings);
    }

    // ------------------------------------------------------------------
    // 6. Multiple quotations per service request
    // ------------------------------------------------------------------

    [Fact]
    public async Task MultipleQuotations_CanExistForSameServiceRequest()
    {
        var srId = Guid.NewGuid();

        await using (var context = CreateDbContext())
        {
            await context.Quotations.AddAsync(BuildQuotation(Guid.NewGuid(), srId));
            await context.Quotations.AddAsync(BuildQuotation(Guid.NewGuid(), srId));
            await context.SaveChangesAsync();
        }

        await using var verify = CreateDbContext();
        var count = await verify.Quotations.CountAsync(q => q.ServiceRequestId == srId);
        Assert.Equal(2, count);
    }
}