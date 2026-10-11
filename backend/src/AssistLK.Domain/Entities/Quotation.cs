using System;
using System.Collections.Generic;

namespace AssistLK.Domain.Entities;

public class Quotation
{
    public int Id { get; set; }
    public Guid ServiceRequestId { get; set; }
    public Guid ProviderId { get; set; }
    public QuotationStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }

    // -----------------------------------------------------------------
    // The LangGraph thread ID for the currently paused approval workflow.
    // Set when /api/quotations/{id}/send-for-approval is called. Clients
    // (React staff, Flutter customer) read this and pass it back to
    // /api/quotations/{id}/approve or /reject to resume the workflow.
    // -----------------------------------------------------------------
    public string? WorkflowThreadId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<QuotationItem> Items { get; set; } = new();
    public Booking? Booking { get; set; }
}

public enum QuotationStatus
{
    Draft = 0,
    Sent = 1,
    WaitingForCustomerApproval = 2,
    Approved = 3,
    Rejected = 4,
    Expired = 5
}