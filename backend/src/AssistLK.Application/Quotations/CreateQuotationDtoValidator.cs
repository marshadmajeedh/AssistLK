using FluentValidation;
using AssistLK.Application.Quotations;

namespace AssistLK.Application.Quotations.DTOs;

/// <summary>
/// Validates a CreateQuotationDto before it reaches the service layer.
/// Enforces all business rules required by Component 3:
///   - A valid ServiceRequest and Provider must be referenced.
///   - At least one line item must be present.
///   - Every item must have a positive amount and quantity.
///   - Item descriptions must be non-empty and reasonably sized.
/// </summary>
public class CreateQuotationDtoValidator : AbstractValidator<CreateQuotationDto>
{
    public CreateQuotationDtoValidator()
    {
        RuleFor(x => x.ServiceRequestId)
            .NotEmpty()
            .WithMessage("ServiceRequestId is required.")
            .Must(id => id != Guid.Empty)
            .WithMessage("ServiceRequestId must be a valid GUID.");

        RuleFor(x => x.ProviderId)
            .NotEmpty()
            .WithMessage("ProviderId is required.")
            .Must(id => id != Guid.Empty)
            .WithMessage("ProviderId must be a valid GUID.");

        RuleFor(x => x.Items)
            .NotNull()
            .WithMessage("Items collection is required.")
            .Must(items => items != null && items.Count > 0)
            .WithMessage("Quotation must contain at least one item.");

        RuleForEach(x => x.Items)
            .SetValidator(new CreateQuotationItemDtoValidator());

        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .When(x => x.Notes is not null)
            .WithMessage("Notes cannot exceed 1000 characters.");
    }
}

/// <summary>
/// Validates a single line item inside a CreateQuotationDto.
/// </summary>
public class CreateQuotationItemDtoValidator : AbstractValidator<CreateQuotationItemDto>
{
    public CreateQuotationItemDtoValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Item description is required.")
            .MaximumLength(500)
            .WithMessage("Item description cannot exceed 500 characters.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Item amount must be greater than zero.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Item quantity must be greater than zero.");

        RuleFor(x => x.Quantity)
            .LessThanOrEqualTo(1000)
            .WithMessage("Item quantity cannot exceed 1000 per line.");
    }
}