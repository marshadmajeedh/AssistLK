using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AssistLK.Application.Quotations;
using AssistLK.Domain.Enums;
using Xunit;

namespace AssistLK.Api.Tests.Quotation;

[Collection("EnvironmentTests")]
public class QuotationControllerSecurityTests : IClassFixture<AssistLKApiTestFactory>
{
    private readonly AssistLKApiTestFactory _factory;

    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public QuotationControllerSecurityTests(AssistLKApiTestFactory factory)
    {
        _factory = factory;
    }

    // ------------------------------------------------------------------
    // 401 Unauthenticated matrix
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("POST", "/api/quotations")]
    [InlineData("GET",  "/api/quotations/1")]
    [InlineData("POST", "/api/quotations/1/send-for-approval")]
    [InlineData("POST", "/api/quotations/1/approve")]
    [InlineData("POST", "/api/quotations/1/reject")]
    [InlineData("GET",  "/api/bookings/1")]
    [InlineData("GET",  "/api/bookings/1/status-history")]
    public async Task Unauthenticated_Returns401(string method, string url)
    {
        var client = _factory.CreateClient();
        using var req = new HttpRequestMessage(new HttpMethod(method), url);

        if (method == "POST")
        {
            req.Content = JsonContent.Create(new { });
        }

        var response = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Role-based authorization
    // ------------------------------------------------------------------

    [Fact]
    public async Task Customer_CannotCreateQuotation_Returns403()
    {
        var client = _factory.CreateAuthenticatedClient(Guid.NewGuid(), UserRole.Customer);

        var dto = new CreateQuotationDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new List<CreateQuotationItemDto> { new("Repair", 1000m, 1) },
            null);

        var response = await client.PostAsJsonAsync("/api/quotations", dto);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Provider_CreateQuotation_Returns201AndComputedTotal()
    {
        var providerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(providerId, UserRole.Provider);

        var dto = new CreateQuotationDto(
            Guid.NewGuid(),
            providerId,
            new List<CreateQuotationItemDto>
            {
                new("Visit charge", 1000m, 1),
                new("Repair", 2500m, 2)
            },
            "Bring spare parts");

        var response = await client.PostAsJsonAsync("/api/quotations", dto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<QuotationDto>(_json);
        Assert.NotNull(body);
        Assert.Equal(6000m, body!.TotalAmount);
        Assert.Equal("Draft", body.Status);
    }

    // ------------------------------------------------------------------
    // Authenticated read
    // ------------------------------------------------------------------

    [Fact]
    public async Task GetById_ReturnsQuotationForAuthenticatedUser()
    {
        var providerId = Guid.NewGuid();
        var provider = _factory.CreateAuthenticatedClient(providerId, UserRole.Provider);

        var dto = new CreateQuotationDto(
            Guid.NewGuid(),
            providerId,
            new List<CreateQuotationItemDto> { new("Repair", 500m, 1) },
            null);

        var createResponse = await provider.PostAsJsonAsync("/api/quotations", dto);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<QuotationDto>(_json);
        Assert.NotNull(created);

        var customer = _factory.CreateAuthenticatedClient(Guid.NewGuid(), UserRole.Customer);
        var getResponse = await customer.GetAsync($"/api/quotations/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    // ------------------------------------------------------------------
    // No chain-of-thought / internal leakage
    // ------------------------------------------------------------------

    [Fact]
    public async Task NoChainOfThoughtOrInternalFieldsLeakedInQuotationResponse()
    {
        var providerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(providerId, UserRole.Provider);

        var dto = new CreateQuotationDto(
            Guid.NewGuid(),
            providerId,
            new List<CreateQuotationItemDto> { new("Repair", 500m, 1) },
            null);

        var response = await client.PostAsJsonAsync("/api/quotations", dto);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("chain_of_thought", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reasoning", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("system_prompt", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ToolExecutor", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AgentMemory", json, StringComparison.OrdinalIgnoreCase);
    }
}