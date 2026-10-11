using System.Net;
using AssistLK.Domain.Enums;
using Xunit;

namespace AssistLK.Api.Tests;

[Collection("EnvironmentTests")]
public class AgentMonitoringControllerSecurityTests : IClassFixture<AssistLKApiTestFactory>
{
    private readonly AssistLKApiTestFactory _factory;

    public AgentMonitoringControllerSecurityTests(AssistLKApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetMetrics_Unauthenticated_Returns401Unauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/agent-monitoring");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMetrics_AuthenticatedAsCustomer_Returns403Forbidden()
    {
        var customerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);

        var response = await client.GetAsync("/api/agent-monitoring");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMetrics_AuthenticatedAsProvider_Returns403Forbidden()
    {
        var providerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(providerId, UserRole.Provider);

        var response = await client.GetAsync("/api/agent-monitoring");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMetrics_AuthenticatedAsAdmin_Returns200Ok()
    {
        var adminId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(adminId, UserRole.Admin);

        var response = await client.GetAsync("/api/agent-monitoring");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
