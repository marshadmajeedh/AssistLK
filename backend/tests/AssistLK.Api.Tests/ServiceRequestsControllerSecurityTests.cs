using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AssistLK.Api.DTOs.ServiceRequests;
using AssistLK.Application.ServiceRequests.DTOs;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;

using System.Text.Json.Serialization;
using Xunit;

namespace AssistLK.Api.Tests;

/// <summary>
/// ServiceRequestsControllerSecurityTests verifies authorization, security, lifecycle transitions,
/// and API contracts for the Customer Service Requests controller endpoints (/api/service-requests).
///
/// Viva Preparation Notes:
/// - What this class tests: Security policies (anonymous rejection, RBAC customer-vs-provider separation,
///   JWT-based customer ownership), lifecycle state machine rules (Created -> Analyzing -> Analyzed / AwaitingInformation
///   -> ReadyForMatching), cancellation rules, and clean architecture controller boundaries.
/// - Why these tests are needed: Service requests contain sensitive personal location and home maintenance issues.
///   The system must guarantee that unauthorized users cannot inspect, tamper with, or advance another customer's request.
/// - What is real vs mocked:
///   * Real: ASP.NET Core WebApplicationFactory pipeline, controller routing, JWT authentication handler,
///     authorization filters, model validation, and EF Core InMemory database.
///   * Mocked/Faked: External Python AI agent (faked by FakeProblemUnderstandingClient) and SMS gateway (TestSmsService).
/// - Prerequisites: None (runs completely in-memory with .NET 8 SDK).
/// - PowerShell command to run class from repository root:
///   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.Api.Tests.ServiceRequestsControllerSecurityTests"
/// </summary>
[Collection("EnvironmentTests")]
public class ServiceRequestsControllerSecurityTests : IClassFixture<AssistLKApiTestFactory>
{
    private readonly AssistLKApiTestFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ServiceRequestsControllerSecurityTests(AssistLKApiTestFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Purpose: Checks that unauthenticated calls to all service request endpoints return 401 Unauthorized.
    /// Why: Protects customer data and operations from anonymous public access.
    /// Scenario: Anonymous requests sent with various HTTP methods (GET, POST, PUT) across 7 endpoints.
    /// Pass condition: Every endpoint returns HTTP 401 Unauthorized.
    /// Note: Parameterized test (Theory). The command below executes all 7 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.Api.Tests.ServiceRequestsControllerSecurityTests.Requirement01_UnauthenticatedCalls_AreRejectedWith401"
    /// </summary>
    [Theory]
    [InlineData("POST", "/api/service-requests")]
    [InlineData("GET", "/api/service-requests/my")]
    [InlineData("GET", "/api/service-requests/00000000-0000-0000-0000-000000000001")]
    [InlineData("PUT", "/api/service-requests/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/service-requests/00000000-0000-0000-0000-000000000001/cancel")]
    [InlineData("POST", "/api/service-requests/00000000-0000-0000-0000-000000000001/analyze")]
    [InlineData("POST", "/api/service-requests/00000000-0000-0000-0000-000000000001/ready-for-matching")]
    public async Task Requirement01_UnauthenticatedCalls_AreRejectedWith401(string method, string url)
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), url);

        if (method is "POST" or "PUT")
        {
            request.Content = JsonContent.Create(new { description = "test", locationText = "Colombo" });
        }

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Purpose: Checks that an authenticated Customer can create a new service request and receives HTTP 201 Created.
    /// Why: Tests the primary creation entry point, default status (Created), and initial unclassified taxonomy.
    /// Scenario: Customer submits a valid problem description and GPS coordinates.
    /// Pass condition: Status is 201 Created with Location header, CustomerId matches authenticated user, Status is Created, Category is "Unclassified".
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.ServiceRequestsControllerSecurityTests.Requirement02_CustomerCanCreateRequest_Returns201CreatedWithLocationHeader"
    /// </summary>
    [Fact]
    public async Task Requirement02_CustomerCanCreateRequest_Returns201CreatedWithLocationHeader()
    {
        var customerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);

        var payload = new CreateServiceRequestRequest
        {
            Description = "Car engine overheated and smoking on the roadside",
            LocationText = "Kandy Road, Kadawatha",
            Latitude = 7.0012m,
            Longitude = 79.9543m
        };

        var response = await client.PostAsJsonAsync("/api/service-requests", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.ServiceRequestId);
        Assert.Equal(customerId, body.CustomerId);
        Assert.Equal(ServiceRequestStatus.Created, body.Status);
        Assert.Null(body.CategoryHint);
        Assert.Equal("Unclassified", body.Category);
        Assert.Equal(ServiceRequestUrgency.Unknown, body.Urgency);
    }

    /// <summary>
    /// Purpose: Checks that submitting a valid CategoryHint ("Plumbing") persists the hint while leaving Category "Unclassified".
    /// Why: User hints assist the agent pipeline but must not bypass automated taxonomy classification.
    /// Scenario: Customer posts request with CategoryHint = "Plumbing" and fetches it back via GET.
    /// Pass condition: Status is 201 Created, CategoryHint is persisted as "Plumbing", Category remains "Unclassified".
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.ServiceRequestsControllerSecurityTests.Requirement02B_CustomerCanCreateRequest_WithValidCategoryHint_PersistsHint_AndLeavesCategoryUnclassified"
    /// </summary>
    [Fact]
    public async Task Requirement02B_CustomerCanCreateRequest_WithValidCategoryHint_PersistsHint_AndLeavesCategoryUnclassified()
    {
        var customerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);

        var payload = new CreateServiceRequestRequest
        {
            Description = "Water pipe has burst and is flooding the entire kitchen floor",
            LocationText = "Kandy Road, Kadawatha",
            CategoryHint = "Plumbing"
        };

        var response = await client.PostAsJsonAsync("/api/service-requests", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(body);
        Assert.Equal("Plumbing", body.CategoryHint);
        Assert.Equal("Unclassified", body.Category);

        // Verify GET details returns CategoryHint
        var getResponse = await client.GetAsync($"/api/service-requests/{body.ServiceRequestId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var getBody = await getResponse.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(getBody);
        Assert.Equal("Plumbing", getBody.CategoryHint);
        Assert.Equal("Unclassified", getBody.Category);
    }

    /// <summary>
    /// Purpose: Checks that invalid or unrecognized category hints are rejected with 400 Bad Request.
    /// Why: Prevents arbitrary, misspelled, or non-whitelisted domain hints from entering the system.
    /// Scenario: Customer sends hints: "Vehicle Assistance", "Cleaning", "random text".
    /// Pass condition: The API returns HTTP 400 Bad Request for each case.
    /// Note: Parameterized test (Theory). The command below executes all 3 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.Api.Tests.ServiceRequestsControllerSecurityTests.Requirement02C_CustomerCreateRequest_WithInvalidCategoryHint_Returns400BadRequest"
    /// </summary>
    [Theory]
    [InlineData("Vehicle Assistance")]
    [InlineData("Cleaning")]
    [InlineData("random text")]
    public async Task Requirement02C_CustomerCreateRequest_WithInvalidCategoryHint_Returns400BadRequest(string invalidHint)
    {
        var customerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);

        var payload = new CreateServiceRequestRequest
        {
            Description = "Need urgent help",
            LocationText = "Colombo",
            CategoryHint = invalidHint
        };

        var response = await client.PostAsJsonAsync("/api/service-requests", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Purpose: Checks that users with Provider role cannot call Customer-only service request endpoints.
    /// Why: Enforces Role-Based Access Control (RBAC) preventing service providers from masquerading as consumers.
    /// Scenario: An authenticated Provider posts a new service request to /api/service-requests.
    /// Pass condition: The API responds with HTTP 403 Forbidden.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.ServiceRequestsControllerSecurityTests.Requirement03_ServiceProviderCannotAccessCustomerEndpoints_Returns403Forbidden"
    /// </summary>
    [Fact]
    public async Task Requirement03_ServiceProviderCannotAccessCustomerEndpoints_Returns403Forbidden()
    {
        var providerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(providerId, UserRole.Provider);

        var payload = new CreateServiceRequestRequest

        {
            Description = "Need mechanic assistance",
            LocationText = "Colombo"
        };

        var response = await client.PostAsJsonAsync("/api/service-requests", payload);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Purpose: Checks that CustomerId is extracted exclusively from the JWT claims, ignoring any spoofed ID in JSON.
    /// Why: Critical security guarantee against ID injection and privilege escalation.
    /// Scenario: Authenticated customer submits a request payload containing a different spoofed customerId Guid.
    /// Pass condition: Status is 201 Created and the created record's CustomerId matches the JWT, not the spoofed body ID.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.ServiceRequestsControllerSecurityTests.Requirement04_And_05_CustomerIdIsDerivedFromJwt_AndCannotBeOverriddenViaJson"
    /// </summary>
    [Fact]
    public async Task Requirement04_And_05_CustomerIdIsDerivedFromJwt_AndCannotBeOverriddenViaJson()
    {
        var authenticatedCustomerId = Guid.NewGuid();
        var spoofedCustomerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(authenticatedCustomerId, UserRole.Customer);

        // Attempt to pass spoofed customerId in request body
        var payloadWithSpoofedId = new
        {
            customerId = spoofedCustomerId,
            description = "Pipe burst in bathroom floor",
            locationText = "Galle Road, Colombo 03"
        };

        var response = await client.PostAsJsonAsync("/api/service-requests", payloadWithSpoofedId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(body);
        // CustomerId MUST be the authenticated user's ID, ignoring the body
        Assert.Equal(authenticatedCustomerId, body.CustomerId);
        Assert.NotEqual(spoofedCustomerId, body.CustomerId);
    }

    /// <summary>
    /// Purpose: Checks that a customer can retrieve their own existing service request by ID.
    /// Why: Customers must be able to inspect details and track progress of their own requests.
    /// Scenario: Customer A requests /api/service-requests/{requestId} for a record they own.
    /// Pass condition: HTTP 200 OK and returned record matches the seeded entity.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.ServiceRequestsControllerSecurityTests.Requirement06_CustomerCanRetrieveOwnRequest_Returns200Ok"
    /// </summary>
    [Fact]
    public async Task Requirement06_CustomerCanRetrieveOwnRequest_Returns200Ok()
    {
        var customerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = requestId,
                CustomerId = customerId,
                Description = "AC stopped cooling",
                LocationText = "Nugegoda",
                Category = "Unclassified",
                Urgency = ServiceRequestUrgency.Unknown,
                Status = ServiceRequestStatus.Created
            });
        });

        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);
        var response = await client.GetAsync($"/api/service-requests/{requestId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(body);
        Assert.Equal(requestId, body.ServiceRequestId);
        Assert.Equal("AC stopped cooling", body.Description);
    }

    /// <summary>
    /// Purpose: Checks that Customer B receives 404 Not Found when attempting to access Customer A's service request.
    /// Why: Prevents data leaks between customers and hides existence of other customers' requests.
    /// Scenario: Customer B requests /api/service-requests/{requestId} for a record owned by Customer A.
    /// Pass condition: API responds with HTTP 404 Not Found.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.ServiceRequestsControllerSecurityTests.Requirement07_CustomerCannotRetrieveAnotherCustomersRequest_Returns404NotFound"
    /// </summary>
    [Fact]
    public async Task Requirement07_CustomerCannotRetrieveAnotherCustomersRequest_Returns404NotFound()
    {
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = requestId,
                CustomerId = customerA,
                Description = "Roof leak during heavy rain",
                LocationText = "Negombo",
                Status = ServiceRequestStatus.Created
            });
        });

        var clientB = _factory.CreateAuthenticatedClient(customerB, UserRole.Customer);
        var response = await clientB.GetAsync($"/api/service-requests/{requestId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Purpose: Checks that GET /api/service-requests/my returns exclusively the authenticated customer's requests.
    /// Why: Guarantees multi-tenant isolation on personal request listing endpoints.
    /// Scenario: Requests seeded for Customer A and Customer B; Customer A calls /my.
    /// Pass condition: HTTP 200 OK; result list contains only Customer A's items and none of Customer B's items.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.ServiceRequestsControllerSecurityTests.Requirement08_GetMyRequests_ReturnsOnlyCurrentCustomersRequests"
    /// </summary>
    [Fact]
    public async Task Requirement08_GetMyRequests_ReturnsOnlyCurrentCustomersRequests()
    {
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddRangeAsync(
                new ServiceRequest
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customerA,
                    Description = "Customer A request 1",
                    LocationText = "Colombo",
                    Status = ServiceRequestStatus.Created
                },
                new ServiceRequest
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customerA,
                    Description = "Customer A request 2",
                    LocationText = "Colombo",
                    Status = ServiceRequestStatus.Created
                },
                new ServiceRequest
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customerB,
                    Description = "Customer B request",
                    LocationText = "Kandy",
                    Status = ServiceRequestStatus.Created
                }
            );
        });

        var clientA = _factory.CreateAuthenticatedClient(customerA, UserRole.Customer);
        var response = await clientA.GetAsync("/api/service-requests/my");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var requests = await response.Content.ReadFromJsonAsync<List<ServiceRequestResponse>>(_jsonOptions);
        Assert.NotNull(requests);
        Assert.All(requests, r => Assert.Equal(customerA, r.CustomerId));
        Assert.Contains(requests, r => r.Description == "Customer A request 1");
        Assert.Contains(requests, r => r.Description == "Customer A request 2");
        Assert.DoesNotContain(requests, r => r.Description == "Customer B request");
    }

    /// <summary>
    /// Purpose: Checks that a customer can edit description and location on a request in Created state.
    /// Why: Users should be allowed to refine information before analysis begins.
    /// Scenario: Customer issues PUT request with revised description and location coordinates.
    /// Pass condition: HTTP 200 OK with response containing the updated description and locationText.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.ServiceRequestsControllerSecurityTests.Requirement09_CustomerCanUpdatePermittedRequest_Returns200Ok"
    /// </summary>
    [Fact]
    public async Task Requirement09_CustomerCanUpdatePermittedRequest_Returns200Ok()
    {
        var customerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = requestId,
                CustomerId = customerId,
                Description = "Initial description",
                LocationText = "Colombo 03",
                Status = ServiceRequestStatus.Created
            });
        });

        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);
        var updatePayload = new UpdateServiceRequestRequest
        {
            Description = "Updated description with more details",
            LocationText = "Colombo 07",
            Latitude = 6.9010m,
            Longitude = 79.8600m
        };

        var response = await client.PutAsJsonAsync($"/api/service-requests/{requestId}", updatePayload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Updated description with more details", updated.Description);
        Assert.Equal("Colombo 07", updated.LocationText);
    }

    /// <summary>
    /// Purpose: Checks that updating CategoryHint preserves Category as "Unclassified" during edit.
    /// Why: Ensures manual category hints cannot unilaterally overwrite system classification.
    /// Scenario: Customer issues PUT request changing CategoryHint to "Electrical" on a Created request.
    /// Pass condition: HTTP 200 OK with CategoryHint updated to "Electrical" and Category remaining "Unclassified".
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.ServiceRequestsControllerSecurityTests.Requirement09B_CustomerCanUpdateCategoryHint_Returns200Ok_AndLeavesCategoryUntouched"
    /// </summary>
    [Fact]
    public async Task Requirement09B_CustomerCanUpdateCategoryHint_Returns200Ok_AndLeavesCategoryUntouched()
    {
        var customerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = requestId,
                CustomerId = customerId,
                CategoryHint = "Plumbing",
                Category = "Unclassified",
                Description = "Initial description",
                LocationText = "Colombo 03",
                Status = ServiceRequestStatus.Created
            });
        });

        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);
        var updatePayload = new UpdateServiceRequestRequest
        {
            Description = "Updated description with electrical focus",
            LocationText = "Colombo 07",
            CategoryHint = "Electrical"
        };

        var response = await client.PutAsJsonAsync($"/api/service-requests/{requestId}", updatePayload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Electrical", updated.CategoryHint);
        Assert.Equal("Unclassified", updated.Category);
    }

    /// <summary>
    /// Purpose: Checks that service requests in protected states (Analyzing, Analyzed, ReadyForMatching) cannot be updated.
    /// Why: Prevents data corruption and race conditions during active agent analysis or provider dispatch.
    /// Scenario: A customer attempts to send a PUT update on a request that has advanced beyond Created.
    /// Pass condition: The API returns HTTP 409 Conflict.
    /// Note: Parameterized test (Theory). The command below executes all 3 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.Api.Tests.ServiceRequestsControllerSecurityTests.Requirement10_CustomerCannotUpdateProtectedLifecycleState_Returns409Conflict"
    /// </summary>
    [Theory]
    [InlineData(ServiceRequestStatus.Analyzing)]
    [InlineData(ServiceRequestStatus.Analyzed)]
    [InlineData(ServiceRequestStatus.ReadyForMatching)]
    public async Task Requirement10_CustomerCannotUpdateProtectedLifecycleState_Returns409Conflict(ServiceRequestStatus protectedStatus)
    {
        var customerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = requestId,
                CustomerId = customerId,
                Description = "Protected request",
                LocationText = "Colombo",
                Status = protectedStatus
            });
        });

        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);
        var updatePayload = new UpdateServiceRequestRequest
        {
            Description = "Illegal modification attempt",
            LocationText = "Colombo"
        };

        var response = await client.PutAsJsonAsync($"/api/service-requests/{requestId}", updatePayload);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>
    /// Purpose: Checks that a customer can cancel requests in early stages (Created, Analyzing, AwaitingInformation, Analyzed).
    /// Why: Allows customers to safely withdraw their problem before matching occurs.
    /// Scenario: Customer issues POST /cancel on requests in each permitted status.
    /// Pass condition: HTTP 200 OK and response status is updated to Cancelled.
    /// Note: Parameterized test (Theory). The command below executes all 4 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.Api.Tests.ServiceRequestsControllerSecurityTests.Requirement11_CustomerCanCancelAllowedRequest_Returns200Ok"
    /// </summary>
    [Theory]
    [InlineData(ServiceRequestStatus.Created)]
    [InlineData(ServiceRequestStatus.Analyzing)]
    [InlineData(ServiceRequestStatus.AwaitingInformation)]
    [InlineData(ServiceRequestStatus.Analyzed)]
    public async Task Requirement11_CustomerCanCancelAllowedRequest_Returns200Ok(ServiceRequestStatus cancellableStatus)
    {
        var customerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = requestId,
                CustomerId = customerId,
                Description = "Request to be cancelled",
                LocationText = "Colombo",
                Status = cancellableStatus
            });
        });

        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);
        var response = await client.PostAsync($"/api/service-requests/{requestId}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cancelled = await response.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(cancelled);
        Assert.Equal(ServiceRequestStatus.Cancelled, cancelled.Status);
    }

    /// <summary>
    /// Purpose: Checks that requests in ReadyForMatching status cannot be cancelled via customer endpoint.
    /// Why: Once matching begins, cancellation requires dedicated provider release and coordination logic.
    /// Scenario: Customer calls POST /cancel on a request already transitioned to ReadyForMatching.
    /// Pass condition: The API responds with HTTP 409 Conflict.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.ServiceRequestsControllerSecurityTests.Requirement12_CustomerCannotCancelAfterReadyForMatching_Returns409Conflict"
    /// </summary>
    [Fact]
    public async Task Requirement12_CustomerCannotCancelAfterReadyForMatching_Returns409Conflict()
    {
        var customerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = requestId,
                CustomerId = customerId,
                Description = "Locked matching request",
                LocationText = "Colombo",
                Status = ServiceRequestStatus.ReadyForMatching
            });
        });

        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);
        var response = await client.PostAsync($"/api/service-requests/{requestId}/cancel", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>
    /// Purpose: Checks that triggering /analyze executes problem understanding and returns structured analysis.
    /// Why: Verifies that customer requests are properly analyzed into categorized problem records.
    /// Scenario: Customer calls POST /analyze for a vehicle breakdown issue.
    /// Pass condition: HTTP 200 OK, Status is Analyzed, Category is "Vehicle Repair", ProblemSummary is populated, confidence > 0.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.ServiceRequestsControllerSecurityTests.Requirement13_CustomerCanAnalyzeTheirRequest_Returns200WithStructuredResponse"
    /// </summary>
    [Fact]
    public async Task Requirement13_CustomerCanAnalyzeTheirRequest_Returns200WithStructuredResponse()
    {
        var customerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = requestId,
                CustomerId = customerId,
                Description = "Car engine will not start and makes clicking sound when turning key",
                LocationText = "Kandy, Central Province",
                Status = ServiceRequestStatus.Created
            });
        });

        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);
        var response = await client.PostAsync($"/api/service-requests/{requestId}/analyze", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProblemUnderstandingResponseDto>(_jsonOptions);
        Assert.NotNull(result);
        Assert.Equal(requestId, result.ServiceRequestId);
        Assert.Equal(ServiceRequestStatus.Analyzed, result.Status);
        Assert.Equal("Vehicle Repair", result.Category);
        Assert.False(string.IsNullOrWhiteSpace(result.ProblemSummary));
        Assert.True(result.Confidence > 0m);
        Assert.False(result.NeedsMoreInformation);
    }

    /// <summary>
    /// Purpose: Checks that Customer B cannot trigger AI analysis on Customer A's request.
    /// Why: Prevents unauthorized execution of agent workflows and unauthorized status transitions.
    /// Scenario: Customer B calls POST /analyze on Customer A's serviceRequestId.
    /// Pass condition: The API responds with HTTP 404 Not Found.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.ServiceRequestsControllerSecurityTests.Requirement14_CustomerCannotAnalyzeAnotherCustomersRequest_Returns404NotFound"
    /// </summary>
    [Fact]
    public async Task Requirement14_CustomerCannotAnalyzeAnotherCustomersRequest_Returns404NotFound()
    {
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = requestId,
                CustomerId = customerA,
                Description = "Water leaking from ceiling",
                LocationText = "Colombo",
                Status = ServiceRequestStatus.Created
            });
        });

        var clientB = _factory.CreateAuthenticatedClient(customerB, UserRole.Customer);
        var response = await clientB.PostAsync($"/api/service-requests/{requestId}/analyze", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Purpose: Checks that ambiguous descriptions yield AwaitingInformation status with follow-up clarification questions.
    /// Why: When customer input lacks necessary detail, the system must prompt for clarification instead of guessing incorrectly.
    /// Scenario: Customer analyzes a vague request ("Machine broken").
    /// Pass condition: Status is AwaitingInformation, NeedsMoreInformation is True, and FollowUpQuestions contains questions.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.ServiceRequestsControllerSecurityTests.Requirement16_AwaitingInformationResponse_ContainsSafeFollowUpQuestions"
    /// </summary>
    [Fact]
    public async Task Requirement16_AwaitingInformationResponse_ContainsSafeFollowUpQuestions()
    {
        var customerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = requestId,
                CustomerId = customerId,
                Description = "Machine broken", // Ambiguous input triggers AwaitingInformation
                LocationText = "Colombo",
                Status = ServiceRequestStatus.Created
            });
        });

        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);
        var response = await client.PostAsync($"/api/service-requests/{requestId}/analyze", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProblemUnderstandingResponseDto>(_jsonOptions);
        Assert.NotNull(result);
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, result.Status);
        Assert.True(result.NeedsMoreInformation);
        Assert.NotEmpty(result.FollowUpQuestions);
    }

    /// <summary>
    /// Purpose: Checks that a detailed, unambiguous problem description yields high-confidence structured output.
    /// Why: Clear problems must be classified with urgency and high confidence ready for immediate matching.
    /// Scenario: Customer analyzes a specific plumbing emergency ("Water pipe has burst in the main bathroom").
    /// Pass condition: Status is Analyzed, Category is "Plumbing", Urgency is High, Confidence >= 0.7.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.ServiceRequestsControllerSecurityTests.Requirement17_SufficientAnalysisResponse_ContainsStructuredSafeOutput"
    /// </summary>
    [Fact]
    public async Task Requirement17_SufficientAnalysisResponse_ContainsStructuredSafeOutput()
    {
        var customerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = requestId,
                CustomerId = customerId,
                Description = "Water pipe has burst in the main bathroom and water is flooding",
                LocationText = "Galle, Southern Province",
                Status = ServiceRequestStatus.Created
            });
        });

        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);
        var response = await client.PostAsync($"/api/service-requests/{requestId}/analyze", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProblemUnderstandingResponseDto>(_jsonOptions);
        Assert.NotNull(result);
        Assert.Equal(ServiceRequestStatus.Analyzed, result.Status);
        Assert.Equal("Plumbing", result.Category);
        Assert.Equal(ServiceRequestUrgency.High, result.Urgency);
        Assert.True(result.Confidence >= 0.7m);
    }

    /// <summary>
    /// Purpose: Checks that internal AI reasoning traces, prompts, and memory keys are never exposed in API responses.
    /// Why: Protects system IP, prevents prompt leakage, and presents a clean customer-facing interface.
    /// Scenario: Customer analyzes a request and the raw JSON response text is verified for absence of sensitive keys.
    /// Pass condition: Strings like "chain_of_thought", "reasoning", "system_prompt", "ToolExecutor", "AgentMemory" are absent.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.ServiceRequestsControllerSecurityTests.Requirement18_ApiNeverExposesChainOfThoughtOrInternalMemory"
    /// </summary>
    [Fact]
    public async Task Requirement18_ApiNeverExposesChainOfThoughtOrInternalMemory()
    {
        var customerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = requestId,
                CustomerId = customerId,
                Description = "Air conditioner does not cool and compressor makes loud rattling noise",
                LocationText = "Colombo",
                Status = ServiceRequestStatus.Created
            });
        });

        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);
        var response = await client.PostAsync($"/api/service-requests/{requestId}/analyze", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();

        // Verify string response contains NO internal memory keys, prompts, reasoning or tool logs
        Assert.DoesNotContain("chain_of_thought", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reasoning", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("system_prompt", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("prompt", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ToolExecutor", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AgentMemory", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("problem.category", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("problem.summary", json, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Purpose: Checks the validation gate on /ready-for-matching, requiring requests to be fully analyzed.
    /// Why: Prevents unanalyzed or ambiguous requests from being dispatched to service providers.
    /// Scenario: Subcase A (AwaitingInformation -> 409 Conflict), Subcase B (Created -> 409 Conflict), Subcase C (Analyzed -> 200 OK & ReadyForMatching).
    /// Pass condition: Non-analyzed states return 409; fully analyzed state transitions to ReadyForMatching with 200 OK.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.ServiceRequestsControllerSecurityTests.Requirement19_ReadyForMatchingEndpoint_EnforcesApplicationValidationGate"
    /// </summary>
    [Fact]
    public async Task Requirement19_ReadyForMatchingEndpoint_EnforcesApplicationValidationGate()
    {
        var customerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);

        // Subcase A: AwaitingInformation request is rejected with 409
        var awaitingId = Guid.NewGuid();
        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = awaitingId,
                CustomerId = customerId,
                Description = "Ambiguous issue",
                LocationText = "Colombo",
                Status = ServiceRequestStatus.AwaitingInformation
            });
        });

        var awaitingResponse = await client.PostAsync($"/api/service-requests/{awaitingId}/ready-for-matching", null);
        Assert.Equal(HttpStatusCode.Conflict, awaitingResponse.StatusCode);

        // Subcase B: Created request without analysis is rejected with 409
        var createdId = Guid.NewGuid();
        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = createdId,
                CustomerId = customerId,
                Description = "Unanalyzed issue",
                LocationText = "Colombo",
                Status = ServiceRequestStatus.Created
            });
        });

        var createdResponse = await client.PostAsync($"/api/service-requests/{createdId}/ready-for-matching", null);
        Assert.Equal(HttpStatusCode.Conflict, createdResponse.StatusCode);

        // Subcase C: Analyzed request with complete structured fields succeeds with 200 OK
        var analyzedId = Guid.NewGuid();
        await _factory.SeedAsync(async db =>
        {
            var req = new ServiceRequest
            {
                Id = analyzedId,
                CustomerId = customerId,
                Description = "Water pipe burst in kitchen",
                LocationText = "Colombo 05",
                Latitude = 6.89m,
                Longitude = 79.87m,
                Category = "Plumbing",
                Urgency = ServiceRequestUrgency.High,
                Status = ServiceRequestStatus.Analyzed
            };
            var analysis = new ProblemAnalysis
            {
                Id = Guid.NewGuid(),
                ServiceRequestId = analyzedId,
                DetectedProblem = "Burst pipe flooding kitchen",
                Confidence = 0.9m,
                AgentName = "ProblemUnderstandingAgent"
            };
            await db.ServiceRequests.AddAsync(req);
            await db.ProblemAnalyses.AddAsync(analysis);
        });

        var successResponse = await client.PostAsync($"/api/service-requests/{analyzedId}/ready-for-matching", null);
        Assert.Equal(HttpStatusCode.OK, successResponse.StatusCode);

        var matchingReady = await successResponse.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(matchingReady);
        Assert.Equal(ServiceRequestStatus.ReadyForMatching, matchingReady.Status);
    }

    /// <summary>
    /// Purpose: Architectural test verifying that ServiceRequestsController depends on application interfaces, not direct DbContext or Agent types.
    /// Why: Enforces clean architecture / hexagonal boundaries preventing architectural leakage.
    /// Scenario: Reflection inspects the constructor parameter types of ServiceRequestsController.
    /// Pass condition: Controller constructor takes IServiceRequestService and ProblemUnderstandingWorkflowService; no DbContext or ProblemUnderstandingAgent.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.ServiceRequestsControllerSecurityTests.Requirement15_Controller_DoesNotDirectlyReferenceDbContextOrAgent_AndUsesWorkflowService"
    /// </summary>
    [Fact]
    public void Requirement15_Controller_DoesNotDirectlyReferenceDbContextOrAgent_AndUsesWorkflowService()
    {
        var controllerType = typeof(AssistLK.Api.Controllers.ServiceRequestsController);
        var constructors = controllerType.GetConstructors();
        Assert.Single(constructors);

        var paramTypes = constructors[0].GetParameters().Select(p => p.ParameterType).ToList();
        Assert.Contains(typeof(AssistLK.Application.Interfaces.IServiceRequestService), paramTypes);
        Assert.Contains(typeof(AssistLK.Application.Services.ProblemUnderstandingWorkflowService), paramTypes);

        Assert.DoesNotContain(paramTypes, t => t.Name.Contains("DbContext"));
        Assert.DoesNotContain(paramTypes, t => t.Name.Contains("ProblemUnderstandingAgent"));
    }

    /// <summary>
    /// Purpose: Tests end-to-end happy-path lifecycle: Created -> Analyzing -> Analyzed -> ReadyForMatching, and subsequent mutation locking.
    /// Why: Proves the complete multi-step customer workflow operates correctly as an integrated whole.
    /// Scenario: Customer creates detailed request, triggers analysis, verifies Analyzed, transitions to ReadyForMatching, and tests that update/cancel are locked.
    /// Pass condition: All lifecycle states transition as expected; after ReadyForMatching, both PUT update and POST cancel return 409 Conflict.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.ServiceRequestsControllerSecurityTests.ScenarioA_DetailedRequest_FullLifecycle_Created_Analyzing_Analyzed_ReadyForMatching"
    /// </summary>
    [Fact]
    public async Task ScenarioA_DetailedRequest_FullLifecycle_Created_Analyzing_Analyzed_ReadyForMatching()
    {
        var customerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);

        // 1. Create: Created
        var createPayload = new CreateServiceRequestRequest
        {
            Description = "Car engine overheats quickly and steam comes from the radiator while driving",
            LocationText = "Kandy Road, Kiribathgoda",
            Latitude = 6.9785m,
            Longitude = 79.9287m
        };

        var createResponse = await client.PostAsJsonAsync("/api/service-requests", createPayload);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(created);
        Assert.Equal(ServiceRequestStatus.Created, created.Status);
        var requestId = created.ServiceRequestId;

        // 2. Analyze: Created -> Analyzing -> Analyzed
        var analyzeResponse = await client.PostAsync($"/api/service-requests/{requestId}/analyze", null);
        Assert.Equal(HttpStatusCode.OK, analyzeResponse.StatusCode);
        var analyzed = await analyzeResponse.Content.ReadFromJsonAsync<ProblemUnderstandingResponseDto>(_jsonOptions);
        Assert.NotNull(analyzed);
        Assert.Equal(ServiceRequestStatus.Analyzed, analyzed.Status);
        Assert.Equal("Vehicle Repair", analyzed.Category);
        Assert.False(analyzed.NeedsMoreInformation);
        Assert.True(analyzed.Confidence > 0m);

        // 3. Verify status: Analyzed
        var getResponse = await client.GetAsync($"/api/service-requests/{requestId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(fetched);
        Assert.Equal(ServiceRequestStatus.Analyzed, fetched.Status);

        // 4. Ready for matching: Analyzed -> ReadyForMatching
        var readyResponse = await client.PostAsync($"/api/service-requests/{requestId}/ready-for-matching", null);
        Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);
        var ready = await readyResponse.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(ready);
        Assert.Equal(ServiceRequestStatus.ReadyForMatching, ready.Status);

        // 5. Verify status after handoff: ReadyForMatching
        var finalGet = await client.GetAsync($"/api/service-requests/{requestId}");
        Assert.Equal(HttpStatusCode.OK, finalGet.StatusCode);
        var finalFetched = await finalGet.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(finalFetched);
        Assert.Equal(ServiceRequestStatus.ReadyForMatching, finalFetched.Status);

        // 6. Verify subsequent mutations are locked: Update -> 409, Cancel -> 409
        var updateAttempt = await client.PutAsJsonAsync($"/api/service-requests/{requestId}", new UpdateServiceRequestRequest
        {
            Description = "Changed my mind",
            LocationText = "Colombo"
        });
        Assert.Equal(HttpStatusCode.Conflict, updateAttempt.StatusCode);

        var cancelAttempt = await client.PostAsync($"/api/service-requests/{requestId}/cancel", null);
        Assert.Equal(HttpStatusCode.Conflict, cancelAttempt.StatusCode);
    }

    /// <summary>
    /// Purpose: Tests ambiguous request lifecycle: Created -> Analyzing -> AwaitingInformation, and verifies ReadyForMatching is blocked.
    /// Why: Guarantees that requests requiring customer clarification cannot bypass the clarification gate into provider matching.
    /// Scenario: Customer creates vague request, triggers analysis, verifies AwaitingInformation, attempts ReadyForMatching.
    /// Pass condition: Analysis sets AwaitingInformation with questions, ReadyForMatching returns 409 Conflict, and status remains AwaitingInformation.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.ServiceRequestsControllerSecurityTests.ScenarioB_AmbiguousRequest_FullLifecycle_Created_Analyzing_AwaitingInformation_ReadyForMatchingFails"
    /// </summary>
    [Fact]
    public async Task ScenarioB_AmbiguousRequest_FullLifecycle_Created_Analyzing_AwaitingInformation_ReadyForMatchingFails()
    {
        var customerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);

        // 1. Create ambiguous request: Created
        var createPayload = new CreateServiceRequestRequest
        {
            Description = "Something broken please help",
            LocationText = "Colombo"
        };

        var createResponse = await client.PostAsJsonAsync("/api/service-requests", createPayload);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(created);
        Assert.Equal(ServiceRequestStatus.Created, created.Status);
        var requestId = created.ServiceRequestId;

        // 2. Analyze: Created -> Analyzing -> AwaitingInformation
        var analyzeResponse = await client.PostAsync($"/api/service-requests/{requestId}/analyze", null);
        Assert.Equal(HttpStatusCode.OK, analyzeResponse.StatusCode);
        var analyzed = await analyzeResponse.Content.ReadFromJsonAsync<ProblemUnderstandingResponseDto>(_jsonOptions);
        Assert.NotNull(analyzed);
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, analyzed.Status);
        Assert.True(analyzed.NeedsMoreInformation);
        Assert.NotEmpty(analyzed.FollowUpQuestions);

        // 3. Verify status: AwaitingInformation
        var getResponse = await client.GetAsync($"/api/service-requests/{requestId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(fetched);
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, fetched.Status);

        // 4. Ready for matching must FAIL from AwaitingInformation (409 Conflict)
        var readyResponse = await client.PostAsync($"/api/service-requests/{requestId}/ready-for-matching", null);
        Assert.Equal(HttpStatusCode.Conflict, readyResponse.StatusCode);

        // 5. Verify status remains AwaitingInformation
        var verifyGet = await client.GetAsync($"/api/service-requests/{requestId}");
        Assert.Equal(HttpStatusCode.OK, verifyGet.StatusCode);
        var verifyFetched = await verifyGet.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(verifyFetched);
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, verifyFetched.Status);
    }
}

