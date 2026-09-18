using FluentValidation;

namespace AssistLK.Application.Quotations;

public sealed class CreateQuotationDtoValidator : AbstractValidator<CreateQuotationDto>
{
    public CreateQuotationDtoValidator()
    {
        RuleFor(x => x.ServiceRequestId)
            .GreaterThan(0);

        RuleFor(x => x.Items)
            .NotEmpty();

        RuleForEach(x => x.Items)
            .ChildRules(item =>
            {
                item.RuleFor(x => x.Amount)
                    .GreaterThan(0);
            });
    }
}