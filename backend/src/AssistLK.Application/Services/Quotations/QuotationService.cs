using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AssistLK.Application.Interfaces;
using AssistLK.Application.Quotations;
using AssistLK.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AssistLK.Application.Services.Quotations;

public class QuotationService : IQuotationService
{
    private readonly IQuotationRepository _quotationRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IServiceRequestLookup _serviceRequestLookup;
    private readonly IConfiguration _configuration;
    private readonly IServiceJobsDbContext _serviceJobsDbContext;

    public QuotationService(
        IQuotationRepository quotationRepository,
        IBookingRepository bookingRepository,
        IServiceRequestLookup serviceRequestLookup,
        IConfiguration configuration,
        IServiceJobsDbContext serviceJobsDbContext)
    {
        _quotationRepository = quotationRepository;
        _bookingRepository = bookingRepository;
        _serviceRequestLookup = serviceRequestLookup;
        _configuration = configuration;
        _serviceJobsDbContext = serviceJobsDbContext;
    }

    // ------------------------------------------------------------------
    // 1. Create a quotation (Provider)
    // ------------------------------------------------------------------
    public async Task<QuotationDto> CreateAsync(
        CreateQuotationDto dto,
        string providerUserId,
        CancellationToken cancellationToken = default)
    {
        if (dto.Items is null || dto.Items.Count == 0)
            throw new ArgumentException("Quotation must contain at least one item.");

        if (!Guid.TryParse(providerUserId, out var providerId))
            throw new ArgumentException("providerUserId must be a valid GUID.");

        foreach (var item in dto.Items)
        {
            if (item.Amount <= 0)
                throw new ArgumentException($"Item '{item.Description}' amount must be positive.");
            if (item.Quantity <= 0)
                throw new ArgumentException($"Item '{item.Description}' quantity must be positive.");
        }

        var total = dto.Items.Sum(i => i.Amount * i.Quantity);

        var quotation = new Quotation
        {
            ServiceRequestId = dto.ServiceRequestId,
            ProviderId = providerId,
            Status = QuotationStatus.Draft,
            TotalAmount = total,
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Items = dto.Items.Select(i => new QuotationItem
            {
                Description = i.Description,
                Amount = i.Amount,
                Quantity = i.Quantity,
            }).ToList(),
        };

        await _quotationRepository.AddAsync(quotation, cancellationToken);
        await _quotationRepository.SaveChangesAsync(cancellationToken);

        return MapToDto(quotation);
    }

    // ------------------------------------------------------------------
    // 2. Read a quotation by id
    // ------------------------------------------------------------------
    public async Task<QuotationDto?> GetByIdAsync(int id)
    {
        var quotation = await _quotationRepository.GetByIdAsync(id, includeItems: true);
        return quotation is null ? null : MapToDto(quotation);
    }

    // ------------------------------------------------------------------
    // 3. List quotations for a service request
    // ------------------------------------------------------------------
    public async Task<IEnumerable<QuotationDto>> GetByServiceRequestAsync(Guid serviceRequestId)
    {
        var quotations = await _quotationRepository.GetByServiceRequestIdAsync(serviceRequestId);
        return quotations.Select(MapToDto);
    }

    // ------------------------------------------------------------------
    // 4. Start the Agentic AI approval workflow
    // ------------------------------------------------------------------
    public async Task<QuotationApprovalWorkflowDto> StartApprovalWorkflowAsync(
        int quotationId,
        string providerUserId,
        CancellationToken cancellationToken = default)
    {
        var quotation = await _quotationRepository.GetByIdAsync(
            quotationId, includeItems: true, cancellationToken: cancellationToken)
            ?? throw new KeyNotFoundException($"Quotation {quotationId} not found.");

        if (Guid.TryParse(providerUserId, out var providerId) &&
            quotation.ProviderId != providerId)
            throw new UnauthorizedAccessException("Only the owning provider can start the approval workflow.");

        if (quotation.Status == QuotationStatus.Draft)
        {
            quotation.Status = QuotationStatus.WaitingForCustomerApproval;
            quotation.UpdatedAt = DateTime.UtcNow;
            _quotationRepository.Update(quotation);
            await _quotationRepository.SaveChangesAsync(cancellationToken);
        }

        var agentBaseUrl = _configuration["AgentServices:QuotationBookingUrl"]
                           ?? "http://localhost:8002";

        var payload = new
        {
            quotation_id = quotation.Id,
            service_request_id = quotation.ServiceRequestId,
            provider_id = quotation.ProviderId,
            items = quotation.Items.Select(i => new
            {
                description = i.Description,
                amount = i.Amount,
                quantity = i.Quantity
            }).ToList(),
            notes = quotation.Notes
        };

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var response = await httpClient.PostAsJsonAsync(
            $"{agentBaseUrl.TrimEnd('/')}/workflows/start",
            payload,
            cancellationToken);

        var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Agent service returned {(int)response.StatusCode} {response.StatusCode}. " +
                $"Body: {rawBody}");
        }

        var result = System.Text.Json.JsonSerializer.Deserialize<StartWorkflowResponse>(
            rawBody,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        if (result is null)
            throw new InvalidOperationException("Agent service returned an empty response.");

        if (string.IsNullOrWhiteSpace(result.ThreadId))
            throw new InvalidOperationException(
                "Agent service returned a response without a thread_id. " +
                $"Raw body: {rawBody}");

        // Persist the LangGraph thread ID so clients can resume the workflow.
        quotation.WorkflowThreadId = result.ThreadId;
        quotation.UpdatedAt = DateTime.UtcNow;
        _quotationRepository.Update(quotation);
        await _quotationRepository.SaveChangesAsync(cancellationToken);

        // Build the risk assessment DTO if the Python agent returned one.
        QuotationRiskAssessmentDto? riskDto = null;
        if (result.ApprovalRequest?.RiskAssessment is { } ra)
        {
            riskDto = new QuotationRiskAssessmentDto(
                ra.RiskLevel,
                ra.Confidence,
                ra.Rationale,
                ra.SuggestedConcerns,
                ra.Recommendation,
                ra.Model,
                ra.PromptTokens,
                ra.CompletionTokens,
                ra.TotalTokens);
        }

        return new QuotationApprovalWorkflowDto(
            MapToDto(quotation),
            result.ThreadId,
            result.Status ?? "waiting_for_approval",
            new QuotationApprovalRequestDto(
                result.ApprovalRequest?.Type ?? "quotation_approval",
                quotation.Id,
                quotation.ServiceRequestId,
                quotation.ProviderId,
                quotation.TotalAmount,
                result.ApprovalRequest?.AllowedActions ?? new List<string> { "approve", "reject" },
                result.ApprovalRequest?.Message ?? "Customer must approve or reject this quotation.",
                riskDto));                                   // ← NEW
    }

    // ------------------------------------------------------------------
    // 5. Resume the Agentic AI approval workflow
    // ------------------------------------------------------------------
    public async Task ResumeApprovalWorkflowAsync(
        int quotationId,
        string threadId,
        string decision,
        string? remarks,
        CancellationToken cancellationToken = default)
    {
        if (decision is not ("approve" or "reject"))
            throw new ArgumentException("decision must be 'approve' or 'reject'.");

        var agentBaseUrl = _configuration["AgentServices:QuotationBookingUrl"]
                           ?? "http://localhost:8002";

        var payload = new
        {
            thread_id = threadId,
            decision,
            remarks
        };

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var response = await httpClient.PostAsJsonAsync(
            $"{agentBaseUrl.TrimEnd('/')}/workflows/resume",
            payload,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Agent service resume returned {(int)response.StatusCode} {response.StatusCode}. " +
                $"Body: {body}");
        }
    }

    // ------------------------------------------------------------------
    // 6. Approve — atomic: create booking + history + location snapshot
    // ------------------------------------------------------------------
    public async Task<BookingDto> ApproveAsync(
        int quotationId,
        ApproveQuotationDto dto,
        string customerUserId,
        CancellationToken cancellationToken = default)
    {
        var quotation = await _quotationRepository.GetByIdAsync(
            quotationId, includeItems: true, cancellationToken: cancellationToken)
            ?? throw new KeyNotFoundException($"Quotation {quotationId} not found.");

        if (quotation.Status != QuotationStatus.WaitingForCustomerApproval)
            throw new InvalidOperationException(
                $"Quotation must be 'WaitingForCustomerApproval', but is '{quotation.Status}'.");

        if (!Guid.TryParse(customerUserId, out var customerId))
            throw new ArgumentException("customerUserId must be a valid GUID.");

        var serviceRequest = await _serviceRequestLookup.GetByIdAsync(
            quotation.ServiceRequestId, cancellationToken);

        quotation.Status = QuotationStatus.Approved;
        quotation.UpdatedAt = DateTime.UtcNow;

        var now = DateTime.UtcNow;

        var booking = new Booking
        {
            QuotationId = quotation.Id,
            CustomerId = customerId,
            ProviderId = quotation.ProviderId,
            Status = BookingStatus.Confirmed,
            ScheduledAt = now,

            LocationText = serviceRequest?.LocationText,
            Latitude = serviceRequest?.Latitude,
            Longitude = serviceRequest?.Longitude,

            CreatedAt = now,
            UpdatedAt = now,
        };

        await _bookingRepository.AddAsync(booking, cancellationToken);
        await _bookingRepository.SaveChangesAsync(cancellationToken);

        var history = new BookingStatusHistory
        {
            BookingId = booking.Id,
            PreviousStatus = BookingStatus.Confirmed,
            NewStatus = BookingStatus.Confirmed,
            ChangedByUserId = customerId,
            Reason = dto.CustomerRemarks,
            ChangedAt = now,
        };

        await _bookingRepository.AddStatusHistoryAsync(history, cancellationToken);
        _quotationRepository.Update(quotation);
        await _bookingRepository.SaveChangesAsync(cancellationToken);

        // ------------------------------------------------------------------
        // Component 3 -> Component 4 Hand-off: Idempotently create ServiceJob
        // ------------------------------------------------------------------
        var existingJob = await _serviceJobsDbContext.ServiceJobs
            .FirstOrDefaultAsync(j => j.BookingId == booking.Id, cancellationToken);

        ServiceJob serviceJob;
        if (existingJob != null)
        {
            serviceJob = existingJob;
        }
        else
        {
            serviceJob = new ServiceJob
            {
                BookingId = booking.Id,
                ServiceRequestId = quotation.ServiceRequestId,
                ProviderId = quotation.ProviderId,
                Status = ServiceJobStatus.Assigned,
                CreatedAt = now
            };
            _serviceJobsDbContext.ServiceJobs.Add(serviceJob);
            await _serviceJobsDbContext.SaveChangesAsync(cancellationToken);
        }

        return MapBookingToDto(booking, serviceJob.Id);
    }

    // ------------------------------------------------------------------
    // 7. Reject
    // ------------------------------------------------------------------
    public async Task<QuotationDto> RejectAsync(
        int quotationId,
        RejectQuotationDto dto,
        string customerUserId,
        CancellationToken cancellationToken = default)
    {
        var quotation = await _quotationRepository.GetByIdAsync(
            quotationId, includeItems: true, cancellationToken: cancellationToken)
            ?? throw new KeyNotFoundException($"Quotation {quotationId} not found.");

        if (quotation.Status != QuotationStatus.WaitingForCustomerApproval)
            throw new InvalidOperationException(
                $"Quotation must be 'WaitingForCustomerApproval', but is '{quotation.Status}'.");

        quotation.Status = QuotationStatus.Rejected;
        quotation.UpdatedAt = DateTime.UtcNow;

        _quotationRepository.Update(quotation);
        await _quotationRepository.SaveChangesAsync(cancellationToken);

        return MapToDto(quotation);
    }

    // ------------------------------------------------------------------
    // 8. Booking read
    // ------------------------------------------------------------------
    public async Task<BookingDto?> GetBookingByIdAsync(int id)
    {
        var booking = await _bookingRepository.GetByIdAsync(id);
        return booking is null ? null : MapBookingToDto(booking);
    }

    // ------------------------------------------------------------------
    // 9. Booking status history
    // ------------------------------------------------------------------
    public async Task<IEnumerable<BookingStatusHistoryDto>> GetBookingStatusHistoryAsync(int bookingId)
    {
        var history = await _bookingRepository.GetStatusHistoryAsync(bookingId);

        return history.Select(h => new BookingStatusHistoryDto(
            h.Id,
            h.BookingId,
            h.PreviousStatus.ToString(),
            h.NewStatus.ToString(),
            h.ChangedByUserId.ToString(),
            h.Reason,
            h.ChangedAt));
    }

    // ------------------------------------------------------------------
    // Mapping helpers
    // ------------------------------------------------------------------
    private static QuotationDto MapToDto(Quotation q) => new(
        q.Id,
        q.ServiceRequestId,
        q.ProviderId,
        q.Status.ToString(),
        q.TotalAmount,
        q.Items.Select(i => new QuotationItemDto(
            i.Id, i.Description, i.Amount, i.Quantity)).ToList(),
        q.WorkflowThreadId,
        q.CreatedAt,
        q.UpdatedAt);

    private static BookingDto MapBookingToDto(Booking b, Guid? serviceJobId = null) => new(
        b.Id,
        b.QuotationId,
        b.CustomerId,
        b.ProviderId,
        b.Status.ToString(),
        b.ScheduledAt,
        b.LocationText,
        b.Latitude,
        b.Longitude,
        b.CreatedAt,
        b.UpdatedAt,
        serviceJobId);
}

// ------------------------------------------------------------------
// Internal helper records for the Python agent responses.
// The Python service uses snake_case. We map via JsonPropertyName.
// ------------------------------------------------------------------
internal sealed record StartWorkflowResponse(
    [property: JsonPropertyName("thread_id")]
    string ThreadId,

    [property: JsonPropertyName("status")]
    string? Status,

    [property: JsonPropertyName("approval_request")]
    ApprovalRequestResponse? ApprovalRequest);

internal sealed record ApprovalRequestResponse(
    [property: JsonPropertyName("type")]
    string? Type,

    [property: JsonPropertyName("allowed_actions")]
    List<string>? AllowedActions,

    [property: JsonPropertyName("message")]
    string? Message,

    [property: JsonPropertyName("risk_assessment")]
    RiskAssessmentResponse? RiskAssessment);            // ← NEW

internal sealed record RiskAssessmentResponse(
    [property: JsonPropertyName("risk_level")]
    string? RiskLevel,

    [property: JsonPropertyName("confidence")]
    double? Confidence,

    [property: JsonPropertyName("rationale")]
    string? Rationale,

    [property: JsonPropertyName("suggested_concerns")]
    List<string>? SuggestedConcerns,

    [property: JsonPropertyName("recommendation")]
    string? Recommendation,

    [property: JsonPropertyName("model")]
    string? Model,

    [property: JsonPropertyName("prompt_tokens")]
    int? PromptTokens,

    [property: JsonPropertyName("completion_tokens")]
    int? CompletionTokens,

    [property: JsonPropertyName("total_tokens")]
    int? TotalTokens);