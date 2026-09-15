namespace AssistLK.Domain.Entities;

public class Quotation
{
    public int Id { get; set; }
    public int ServiceRequestId { get; set; }        // FK from Component 1
    public int ProviderId { get; set; }               // FK from Component 2
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
    Draft,
    Sent,
    WaitingForCustomerApproval,   // 🚦 AI must pause here
    Approved,
    Rejected,
    Expired
}