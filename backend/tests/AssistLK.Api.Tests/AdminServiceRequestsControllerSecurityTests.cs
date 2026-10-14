using System.Net;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using Xunit;

namespace AssistLK.Api.Tests;

/// <summary>
/// AdminServiceRequestsControllerSecurityTests verifies Role-Based Access Control (RBAC) security
/// for the Admin Service Requests API (/api/admin/service-requests).
///
/// Viva Preparation Notes:
/// - What this class tests: Security authorization policies ensuring that only users with the Admin
///   role can access global service request management endpoints.
/// - Why these tests are needed: Admin endpoints provide broad access across all customers' personal
///   data. Anonymous users, Customers, and Providers must be strictly rejected.
/// - What is real vs mocked:
///   * Real: ASP.NET Core test server, JWT authentication handler, role claim verification,
///     authorization middleware, and EF Core InMemory database.
///   * Mocked/Faked: None.
/// - Prerequisites: None (runs completely in-memory with .NET 8 SDK).
/// - PowerShell command to run class from repository root:
///   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.Api.Tests.AdminServiceRequestsControllerSecurityTests"
/// </summary>
[Collection("EnvironmentTests")]
public class AdminServiceRequestsControllerSecurityTests : IClassFixture<AssistLKApiTestFactory>
{
    private readonly AssistLKApiTestFactory _factory;

    public AdminServiceRequestsControllerSecurityTests(AssistLKApiTestFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Purpose: Checks that unauthenticated requests to the admin listing endpoint return 401 Unauthorized.
    /// Why: Prevents anonymous internet users from querying the global request database.
    /// Scenario: Anonymous HTTP GET request to /api/admin/service-requests.
    /// Pass condition: HTTP 401 Unauthorized.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.AdminServiceRequestsControllerSecurityTests.GetAll_Unauthenticated_Returns401Unauthorized"
    /// </summary>
    [Fact]
    public async Task GetAll_Unauthenticated_Returns401Unauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/admin/service-requests");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Purpose: Checks that unauthenticated requests to admin request details return 401 Unauthorized.
    /// Why: Prevents anonymous access to individual customer request records.
    /// Scenario: Anonymous HTTP GET request to /api/admin/service-requests/{id}.
    /// Pass condition: HTTP 401 Unauthorized.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.AdminServiceRequestsControllerSecurityTests.GetById_Unauthenticated_Returns401Unauthorized"
    /// </summary>
    [Fact]
    public async Task GetById_Unauthenticated_Returns401Unauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/admin/service-requests/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Purpose: Checks that authenticated Customer users receive 403 Forbidden when calling admin listing.
    /// Why: Enforces RBAC preventing standard customer accounts from elevating privileges.
    /// Scenario: Customer issues GET /api/admin/service-requests.
    /// Pass condition: HTTP 403 Forbidden.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.AdminServiceRequestsControllerSecurityTests.GetAll_AuthenticatedAsCustomer_Returns403Forbidden"
    /// </summary>
    [Fact]
    public async Task GetAll_AuthenticatedAsCustomer_Returns403Forbidden()
    {
        var customerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);

        var response = await client.GetAsync("/api/admin/service-requests");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Purpose: Checks that authenticated Customer users receive 403 Forbidden on admin detail requests.
    /// Why: Enforces RBAC preventing customers from inspecting administrative metadata.
    /// Scenario: Customer issues GET /api/admin/service-requests/{id}.
    /// Pass condition: HTTP 403 Forbidden.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.AdminServiceRequestsControllerSecurityTests.GetById_AuthenticatedAsCustomer_Returns403Forbidden"
    /// </summary>
    [Fact]
    public async Task GetById_AuthenticatedAsCustomer_Returns403Forbidden()
    {
        var customerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);

        var response = await client.GetAsync($"/api/admin/service-requests/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Purpose: Checks that authenticated Provider users receive 403 Forbidden when calling admin listing.
    /// Why: Enforces RBAC preventing service providers from viewing global platform service requests.
    /// Scenario: Provider issues GET /api/admin/service-requests.
    /// Pass condition: HTTP 403 Forbidden.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.AdminServiceRequestsControllerSecurityTests.GetAll_AuthenticatedAsProvider_Returns403Forbidden"
    /// </summary>
    [Fact]
    public async Task GetAll_AuthenticatedAsProvider_Returns403Forbidden()
    {
        var providerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(providerId, UserRole.Provider);

        var response = await client.GetAsync("/api/admin/service-requests");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Purpose: Checks that authenticated Provider users receive 403 Forbidden on admin detail requests.
    /// Why: Enforces RBAC preventing service providers from bypassing provider dispatch rules.
    /// Scenario: Provider issues GET /api/admin/service-requests/{id}.
    /// Pass condition: HTTP 403 Forbidden.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.AdminServiceRequestsControllerSecurityTests.GetById_AuthenticatedAsProvider_Returns403Forbidden"
    /// </summary>
    [Fact]
    public async Task GetById_AuthenticatedAsProvider_Returns403Forbidden()
    {
        var providerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(providerId, UserRole.Provider);

        var response = await client.GetAsync($"/api/admin/service-requests/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Purpose: Checks that authenticated Admin users are permitted to access GET /api/admin/service-requests.
    /// Why: Validates that users with Admin role successfully satisfy the authorization policy.
    /// Scenario: Admin calls GET /api/admin/service-requests.
    /// Pass condition: HTTP 200 OK.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.AdminServiceRequestsControllerSecurityTests.GetAll_AuthenticatedAsAdmin_Returns200Ok"
    /// </summary>
    [Fact]
    public async Task GetAll_AuthenticatedAsAdmin_Returns200Ok()
    {
        var adminId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(adminId, UserRole.Admin);

        var response = await client.GetAsync("/api/admin/service-requests");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Purpose: Checks that authenticated Admin users are permitted to retrieve details of an existing request.
    /// Why: Validates that Admin users can inspect individual service requests.
    /// Scenario: Admin calls GET /api/admin/service-requests/{requestId} for a seeded record.
    /// Pass condition: HTTP 200 OK.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.AdminServiceRequestsControllerSecurityTests.GetById_AuthenticatedAsAdmin_WhenExists_Returns200Ok"
    /// </summary>
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

