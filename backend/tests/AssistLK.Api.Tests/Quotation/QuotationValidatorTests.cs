using System;
using System.Collections.Generic;
using AssistLK.Application.Quotations;
using AssistLK.Application.Quotations.DTOs;
using Xunit;

namespace AssistLK.Api.Tests.Quotation;

public class QuotationValidatorTests
{
    private readonly CreateQuotationDtoValidator _validator = new();

    private static CreateQuotationDto Valid() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        new List<CreateQuotationItemDto> { new("Repair", 5000m, 1) },
        null);

    [Fact]
    public void Valid_Passes()
    {
        var result = _validator.Validate(Valid());
        Assert.True(result.IsValid);
    }

    [Fact]
    public void EmptyServiceRequestId_Fails()
    {
        var dto = Valid() with { ServiceRequestId = Guid.Empty };
        Assert.False(_validator.Validate(dto).IsValid);
    }

    [Fact]
    public void EmptyProviderId_Fails()
    {
        var dto = Valid() with { ProviderId = Guid.Empty };
        Assert.False(_validator.Validate(dto).IsValid);
    }

    [Fact]
    public void NullItems_Fails()
    {
        var dto = Valid() with { Items = null! };
        Assert.False(_validator.Validate(dto).IsValid);
    }

    [Fact]
    public void EmptyItems_Fails()
    {
        var dto = Valid() with { Items = new List<CreateQuotationItemDto>() };
        Assert.False(_validator.Validate(dto).IsValid);
    }

    [Fact]
    public void ZeroAmount_Fails()
    {
        var dto = Valid() with { Items = new List<CreateQuotationItemDto> { new("x", 0m, 1) } };
        Assert.False(_validator.Validate(dto).IsValid);
    }

    [Fact]
    public void NegativeAmount_Fails()
    {
        var dto = Valid() with { Items = new List<CreateQuotationItemDto> { new("x", -1m, 1) } };
        Assert.False(_validator.Validate(dto).IsValid);
    }

    [Fact]
    public void ZeroQuantity_Fails()
    {
        var dto = Valid() with { Items = new List<CreateQuotationItemDto> { new("x", 10m, 0) } };
        Assert.False(_validator.Validate(dto).IsValid);
    }

    [Fact]
    public void QuantityOver1000_Fails()
    {
        var dto = Valid() with { Items = new List<CreateQuotationItemDto> { new("x", 10m, 1001) } };
        Assert.False(_validator.Validate(dto).IsValid);
    }

    [Fact]
    public void EmptyDescription_Fails()
    {
        var dto = Valid() with { Items = new List<CreateQuotationItemDto> { new("", 10m, 1) } };
        Assert.False(_validator.Validate(dto).IsValid);
    }

    [Fact]
    public void DescriptionOver500_Fails()
    {
        var dto = Valid() with { Items = new List<CreateQuotationItemDto> { new(new string('x', 501), 10m, 1) } };
        Assert.False(_validator.Validate(dto).IsValid);
    }

    [Fact]
    public void NotesOver1000_Fails()
    {
        var dto = Valid() with { Notes = new string('x', 1001) };
        Assert.False(_validator.Validate(dto).IsValid);
    }

    [Fact]
    public void BoundaryQuantity1000_Passes()
    {
        var dto = Valid() with { Items = new List<CreateQuotationItemDto> { new("x", 1m, 1000) } };
        Assert.True(_validator.Validate(dto).IsValid);
    }
}