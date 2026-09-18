public record CreateQuotationDto(
    int ServiceRequestId,
    int ProviderId,
    List<CreateQuotationItemDto> Items,
    string? Notes);

public record CreateQuotationItemDto(string Description, decimal Amount, int Quantity = 1);

public record QuotationDto(
    int Id,
    int ServiceRequestId,
    int ProviderId,
    string Status,
    decimal TotalAmount,
    List<QuotationItemDto> Items,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record QuotationItemDto(int Id, string Description, decimal Amount, int Quantity);

public record ApproveQuotationDto(string? CustomerRemarks);   // business-specific
public record RejectQuotationDto(string Reason);