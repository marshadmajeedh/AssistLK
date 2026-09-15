namespace AssistLK.Domain.Entities;

public class QuotationItem
{
    public int Id { get; set; }
    public int QuotationId { get; set; }
    public string Description { get; set; } = string.Empty; // "Visit charge"
    public decimal Amount { get; set; }
    public int Quantity { get; set; } = 1;

    public Quotation Quotation { get; set; } = null!;
}