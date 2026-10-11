using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AssistLK.Application.DTOs;
using AssistLK.Application.Services;
using AssistLK.Domain.Entities;
using AssistLK.Infrastructure.Data;

namespace AssistLK.Infrastructure.Services;

public class ProviderBookingQueryService : IProviderBookingQueryService
{
    private readonly AssistLKDbContext _context;

    public ProviderBookingQueryService(AssistLKDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProviderBookingListDto>> GetForProviderAsync(
        Guid providerUserId,
        bool completed)
    {
        var providerId = await _context.ProviderProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == providerUserId)
            .Select(profile => (Guid?)profile.Id)
            .SingleOrDefaultAsync();

        if (providerId is null)
            return new List<ProviderBookingListDto>();

        // ── Step 1: ServiceJobs assigned to this provider ─────────────
        var jobs = await _context.ServiceJobs
            .Where(j => j.ProviderId == providerId.Value)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();

        // ── Step 2: Load related ServiceRequests + Customers ──────────
        var serviceRequestIds = jobs
            .Where(j => j.ServiceRequestId.HasValue)
            .Select(j => j.ServiceRequestId!.Value)
            .Distinct()
            .ToList();

        var serviceRequests = await _context.ServiceRequests
            .Where(sr => serviceRequestIds.Contains(sr.Id))
            .Include(sr => sr.Customer)
            .ToDictionaryAsync(sr => sr.Id);

        // ── Step 3: Load Quotations + Bookings for those requests ─────
        var quotations = await _context.Quotations
            .Where(q =>
                serviceRequestIds.Contains(q.ServiceRequestId) &&
                q.ProviderId == providerId.Value)
            .Include(q => q.Booking)
            .ToListAsync();

        var quotationsByRequest = quotations
            .GroupBy(q => q.ServiceRequestId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(q => q.UpdatedAt).First());

        // ── Step 4: Shape into DTOs ───────────────────────────────────
        var results = new List<ProviderBookingListDto>();

        foreach (var job in jobs)
        {
            serviceRequests.TryGetValue(
                job.ServiceRequestId ?? Guid.Empty, out var sr);

            Quotation? quotation = null;
            if (sr != null)
                quotationsByRequest.TryGetValue(sr.Id, out quotation);

            var booking = quotation?.Booking;

            var dto = new ProviderBookingListDto
            {
                Id = job.Id,
                ServiceRequestId = job.ServiceRequestId,
                ProviderId = providerId.Value,
                BookingId = job.BookingId,
                CustomerName = sr?.Customer?.FullName ?? "Customer",
                ServiceTitle = sr?.Category ?? "Service",
                LocationText = sr?.LocationText ?? string.Empty,
                Amount = quotation?.TotalAmount ?? 0m,
                StatusCode = MapStatusCode(booking),
                StatusLabel = MapStatusLabel(quotation, booking),
                ScheduledAt = booking?.ScheduledAt ?? job.CreatedAt,
                IsCompleted = job.Status == ServiceJobStatus.Completed
                              || job.Status == ServiceJobStatus.Cancelled,
                IsPendingQuote = quotation is null ||
                                 quotation.Status is QuotationStatus.Draft or QuotationStatus.Rejected,
            };

            if (completed != dto.IsCompleted) continue;

            results.Add(dto);
        }

        return results;
    }

    // ───────────────────────────────────────────────────────────────
    // Mapping helpers
    // ───────────────────────────────────────────────────────────────

    private static int MapStatusCode(Booking? booking)
    {
        if (booking == null) return 0; // Pending quote → treat as Confirmed code
        return booking.Status switch
        {
            BookingStatus.Confirmed => 0,
            BookingStatus.ProviderAssigned => 1,
            BookingStatus.ProviderOnTheWay => 2,
            BookingStatus.ProviderArrived => 3,
            BookingStatus.WorkStarted => 4,
            BookingStatus.WorkCompleted => 5,
            BookingStatus.Cancelled => 6,
            _ => 0,
        };
    }

    private static string MapStatusLabel(Quotation? quotation, Booking? booking)
    {
        if (booking == null)
        {
            return quotation?.Status switch
            {
                QuotationStatus.Draft => "DRAFT QUOTE",
                QuotationStatus.WaitingForCustomerApproval => "AWAITING CUSTOMER",
                QuotationStatus.Approved => "APPROVED",
                QuotationStatus.Rejected => "QUOTE REJECTED",
                QuotationStatus.Expired => "QUOTE EXPIRED",
                _ => "PENDING QUOTE",
            };
        }

        return booking.Status switch
        {
            BookingStatus.Confirmed => "CONFIRMED",
            BookingStatus.ProviderAssigned => "ASSIGNED",
            BookingStatus.ProviderOnTheWay => "ON THE WAY",
            BookingStatus.ProviderArrived => "ARRIVED",
            BookingStatus.WorkStarted => "IN PROGRESS",
            BookingStatus.WorkCompleted => "COMPLETED",
            BookingStatus.Cancelled => "CANCELLED",
            _ => "CONFIRMED",
        };
    }
}