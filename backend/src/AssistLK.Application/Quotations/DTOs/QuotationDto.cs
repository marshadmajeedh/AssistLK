using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AssistLK.Application.Quotations;

// ----------------------------------------------------------------
// Create (request) DTOs
// ----------------------------------------------------------------

public record CreateQuotationDto(
    Guid ServiceRequestId,
    Guid ProviderId,
    List<CreateQuotationItemDto> Items,
    string? Notes);

public record CreateQuotationItemDto(
    string Description,
    decimal Amount,
    int Quantity = 1);

// ----------------------------------------------------------------
// Response DTOs
// ----------------------------------------------------------------

public record QuotationDto(
    int Id,
    Guid ServiceRequestId,
    Guid ProviderId,
    string Status,
    decimal TotalAmount,
    List<QuotationItemDto> Items,
    string? WorkflowThreadId,        // ← new
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record QuotationItemDto(
    int Id,
    string Description,
    decimal Amount,
    int Quantity);

// ----------------------------------------------------------------
// Approval workflow DTOs (used by Python agent)
// ----------------------------------------------------------------

public record QuotationApprovalWorkflowDto(
    QuotationDto Quotation,
    string ThreadId,
    string Status,
    QuotationApprovalRequestDto ApprovalRequest);

public record QuotationApprovalRequestDto(
    string Type,
    int QuotationId,
    Guid? ServiceRequestId,
    Guid? ProviderId,
    decimal? TotalAmount,
    List<string> AllowedActions,
    string Message);

// ----------------------------------------------------------------
// Decision DTOs
// ----------------------------------------------------------------

public record ApproveQuotationDto(
    string? CustomerRemarks,
    [property: Required] string ThreadId);

public record RejectQuotationDto(
    string Reason,
    [property: Required] string ThreadId);