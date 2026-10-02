using System;
using System.Collections.Generic;

namespace AssistLK.Domain.Entities;

public class Quotation
{
    public int Id { get; set; }
    public Guid ServiceRequestId { get; set; }        // ✅ Guid
    public Guid ProviderId { get; set; }               // ✅ Guid
    public QuotationStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }

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