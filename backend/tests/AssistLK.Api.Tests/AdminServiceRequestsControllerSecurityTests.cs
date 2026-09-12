using System.Net;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using Xunit;

namespace AssistLK.Api.Tests;

[Collection("EnvironmentTests")]
public class AdminServiceRequestsControllerSecurityTests : IClassFixture<AssistLKApiTestFactory>
{
    private readonly AssistLKApiTestFactory _factory;

    public AdminServiceRequestsControllerSecurityTests(AssistLKApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAll_Unauthenticated_Returns401Unauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/admin/service-requests");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_Unauthenticated_Returns401Unauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/admin/service-requests/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_AuthenticatedAsCustomer_Returns403Forbidden()
    {
        var customerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);

        var response = await client.GetAsync("/api/admin/service-requests");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetById_AuthenticatedAsCustomer_Returns403Forbidden()
    {
        var customerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);

        var response = await client.GetAsync($"/api/admin/service-requests/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_AuthenticatedAsProvider_Returns403Forbidden()
    {
        var providerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(providerId, UserRole.Provider);

        var response = await client.GetAsync("/api/admin/service-requests");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetById_AuthenticatedAsProvider_Returns403Forbidden()
    {
        var providerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(providerId, UserRole.Provider);

        var response = await client.GetAsync($"/api/admin/service-requests/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_AuthenticatedAsAdmin_Returns200Ok()
    {
        var adminId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(adminId, UserRole.Admin);

        var response = await client.GetAsync("/api/admin/service-requests");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetById_AuthenticatedAsAdmin_WhenExists_Returns200Ok()
    {
        var adminId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = requestId,
                CustomerId = Guid.NewGuid(),
                Description = "Pipe burst",
                LocationText = "Colombo",
                Category = "Plumbing",
                Urgency = ServiceRequestUrgency.High,
                Status = ServiceRequestStatus.Created
            });
        });

        var client = _factory.CreateAuthenticatedClient(adminId, UserRole.Admin);

        var response = await client.GetAsync($"/api/admin/service-requests/{requestId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
