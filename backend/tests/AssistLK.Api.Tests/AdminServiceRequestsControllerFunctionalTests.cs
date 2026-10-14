using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AssistLK.Application.ServiceRequests.DTOs;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using Xunit;

namespace AssistLK.Api.Tests;

/// <summary>
/// AdminServiceRequestsControllerFunctionalTests verifies functional behavior, query filtering,
/// detail projection, and ordering for the Admin Service Requests API (/api/admin/service-requests).
///
/// Viva Preparation Notes:
/// - What this class tests: Administrative cross-customer request listing, detailed retrieval
///   including AI analysis and clarification history, query filtering by status, category, urgency,
///   combined multi-attribute filtering, validation of filter parameters, and newest-first ordering.
/// - Why these tests are needed: Administrators need a comprehensive, authoritative control plane
///   to monitor, triage, and audit all customer service requests across the platform.
/// - What is real vs mocked:
///   * Real: ASP.NET Core test server, routing, Admin role authorization, EF Core InMemory database,
///     LINQ query execution, and JSON serialization.
///   * Mocked/Faked: None needed directly in these tests (data is seeded via EF Core).
/// - Prerequisites: None (runs completely in-memory with .NET 8 SDK).
/// - PowerShell command to run class from repository root:
///   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.Api.Tests.AdminServiceRequestsControllerFunctionalTests"
/// </summary>
[Collection("EnvironmentTests")]
public class AdminServiceRequestsControllerFunctionalTests : IClassFixture<AssistLKApiTestFactory>
{
    private readonly AssistLKApiTestFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AdminServiceRequestsControllerFunctionalTests(AssistLKApiTestFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Purpose: Checks that querying the admin list endpoint on an empty database returns 200 OK with an empty array.
    /// Why: Guarantees graceful response formatting when no service requests have been submitted.
    /// Scenario: An Admin requests GET /api/admin/service-requests against an empty database.
    /// Pass condition: HTTP 200 OK and response list is empty.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.AdminServiceRequestsControllerFunctionalTests.Admin_EmptyDatabase_ReturnsEmptyList"
    /// </summary>
    [Fact]
    public async Task Admin_EmptyDatabase_ReturnsEmptyList()
    {
        using var factory = new AssistLKApiTestFactory();
        var client = factory.CreateAuthenticatedClient(Guid.NewGuid(), UserRole.Admin);

        var response = await client.GetAsync("/api/admin/service-requests");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var requests = await response.Content.ReadFromJsonAsync<List<ServiceRequestResponse>>(_jsonOptions);
        Assert.NotNull(requests);
        Assert.Empty(requests);
    }

    /// <summary>
    /// Purpose: Checks that an Admin can see requests belonging to different customers in a single global list.
    /// Why: Unlike customers who only see their own requests, admins must have cross-customer system visibility.
    /// Scenario: Requests created for Customer 1 and Customer 2; Admin lists all requests.
    /// Pass condition: HTTP 200 OK; the returned list contains both Customer 1 and Customer 2 requests.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.AdminServiceRequestsControllerFunctionalTests.Admin_CanListAllServiceRequestsAcrossCustomers"
    /// </summary>
    [Fact]
    public async Task Admin_CanListAllServiceRequestsAcrossCustomers()
    {
        var customer1 = Guid.NewGuid();
        var customer2 = Guid.NewGuid();
        var req1 = Guid.NewGuid();
        var req2 = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddRangeAsync(
                new ServiceRequest
                {
                    Id = req1,
                    CustomerId = customer1,
                    Description = "Kitchen pipe burst",
                    LocationText = "Colombo 03",
                    Category = "Plumbing",
                    Urgency = ServiceRequestUrgency.High,
                    Status = ServiceRequestStatus.Created,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                    UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
                },
                new ServiceRequest
                {
                    Id = req2,
                    CustomerId = customer2,
                    Description = "Fuse box sparking",
                    LocationText = "Kandy",
                    Category = "Electrical",
                    Urgency = ServiceRequestUrgency.Critical,
                    Status = ServiceRequestStatus.Created,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                    UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
                }
            );
        });

        var adminClient = _factory.CreateAuthenticatedClient(Guid.NewGuid(), UserRole.Admin);

        var response = await adminClient.GetAsync("/api/admin/service-requests");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var requests = await response.Content.ReadFromJsonAsync<List<ServiceRequestResponse>>(_jsonOptions);
        Assert.NotNull(requests);
        Assert.Contains(requests, r => r.ServiceRequestId == req1 && r.CustomerId == customer1);
        Assert.Contains(requests, r => r.ServiceRequestId == req2 && r.CustomerId == customer2);
    }

    /// <summary>
    /// Purpose: Checks that the admin detail endpoint returns all authoritative fields including AI analysis and clarifications.
    /// Why: Administrators require complete diagnostic and history information to review AI problem classifications.
    /// Scenario: Admin requests detail for a request with ProblemAnalysis and 2 answered clarification questions.
    /// Pass condition: Status 200 OK; core fields match; LatestAnalysis contains confidence & agentName; Clarifications contains 2 ordered items.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.AdminServiceRequestsControllerFunctionalTests.Admin_CanRetrieveDetail_WithAllAuthoritativeFields"
    /// </summary>
    [Fact]
    public async Task Admin_CanRetrieveDetail_WithAllAuthoritativeFields()
    {
        var customerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var analysisId = Guid.NewGuid();
        var clarId1 = Guid.NewGuid();
        var clarId2 = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            var request = new ServiceRequest
            {
                Id = requestId,
                CustomerId = customerId,
                CategoryHint = "Plumbing",
                Category = "Plumbing",
                Description = "Major leak under kitchen sink flooding cabinet",
                LocationText = "Colombo 07",
                Latitude = 6.9100m,
                Longitude = 79.8700m,
                Urgency = ServiceRequestUrgency.High,
                Status = ServiceRequestStatus.Analyzed,
                CreatedAt = DateTime.UtcNow.AddHours(-1),
                UpdatedAt = DateTime.UtcNow.AddHours(-1)
            };

            var analysis = new ProblemAnalysis
            {
                Id = analysisId,
                ServiceRequestId = requestId,
                DetectedProblem = "Burst pipe joint under sink",
                Confidence = 0.95m,
                AgentName = "ProblemUnderstandingAgent",
                CreatedAt = DateTime.UtcNow.AddMinutes(-30),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-30)
            };

            var clar1 = new ServiceRequestClarification
            {
                Id = clarId1,
                ServiceRequestId = requestId,
                ClarificationRound = 1,
                Sequence = 1,
                Question = "Is the main valve shut off?",
                Answer = "Yes, shut off completely",
                AnsweredAt = DateTime.UtcNow.AddMinutes(-40),
                CreatedAt = DateTime.UtcNow.AddMinutes(-50),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-40)
            };

            var clar2 = new ServiceRequestClarification
            {
                Id = clarId2,
                ServiceRequestId = requestId,
                ClarificationRound = 1,
                Sequence = 2,
                Question = "Is it hot or cold water?",
                Answer = "Cold water pipe",
                AnsweredAt = DateTime.UtcNow.AddMinutes(-35),
                CreatedAt = DateTime.UtcNow.AddMinutes(-50),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-35)
            };

            await db.ServiceRequests.AddAsync(request);
            await db.ProblemAnalyses.AddAsync(analysis);
            await db.ServiceRequestClarifications.AddRangeAsync(clar1, clar2);
        });

        var adminClient = _factory.CreateAuthenticatedClient(Guid.NewGuid(), UserRole.Admin);

        var response = await adminClient.GetAsync($"/api/admin/service-requests/{requestId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(requestId, detail.ServiceRequestId);
        Assert.Equal(customerId, detail.CustomerId);
        Assert.Equal("Plumbing", detail.CategoryHint);
        Assert.Equal("Plumbing", detail.Category);
        Assert.Equal("Major leak under kitchen sink flooding cabinet", detail.Description);
        Assert.Equal("Colombo 07", detail.LocationText);
        Assert.Equal(6.9100m, detail.Latitude);
        Assert.Equal(79.8700m, detail.Longitude);
        Assert.Equal(ServiceRequestUrgency.High, detail.Urgency);
        Assert.Equal(ServiceRequestStatus.Analyzed, detail.Status);

        // LatestAnalysis verification
        Assert.NotNull(detail.LatestAnalysis);
        Assert.Equal(analysisId, detail.LatestAnalysis.Id);
        Assert.Equal("Burst pipe joint under sink", detail.LatestAnalysis.DetectedProblem);
        Assert.Equal(0.95m, detail.LatestAnalysis.Confidence);
        Assert.Equal("ProblemUnderstandingAgent", detail.LatestAnalysis.AgentName);

        // Clarifications verification
        Assert.NotNull(detail.Clarifications);
        Assert.Equal(2, detail.Clarifications.Count);
        Assert.Equal(1, detail.Clarifications[0].Sequence);
        Assert.Equal("Is the main valve shut off?", detail.Clarifications[0].Question);
        Assert.Equal("Yes, shut off completely", detail.Clarifications[0].Answer);
        Assert.Equal(2, detail.Clarifications[1].Sequence);
        Assert.Equal("Is it hot or cold water?", detail.Clarifications[1].Question);
        Assert.Equal("Cold water pipe", detail.Clarifications[1].Answer);
    }

    /// <summary>
    /// Purpose: Checks that requesting a non-existent request ID returns HTTP 404 Not Found.
    /// Why: Standard REST error handling for missing resources.
    /// Scenario: Admin requests a randomly generated Guid that does not exist in the database.
    /// Pass condition: The API responds with HTTP 404 Not Found.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.AdminServiceRequestsControllerFunctionalTests.Admin_MissingRequest_Returns404NotFound"
    /// </summary>
    [Fact]
    public async Task Admin_MissingRequest_Returns404NotFound()
    {
        var adminClient = _factory.CreateAuthenticatedClient(Guid.NewGuid(), UserRole.Admin);

        var response = await adminClient.GetAsync($"/api/admin/service-requests/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Purpose: Checks that the status query parameter returns exclusively matching service requests.
    /// Why: Admins need to filter requests by lifecycle state to monitor queues.
    /// Scenario: Database has Analyzed and Cancelled requests; Admin requests ?status=Analyzed.
    /// Pass condition: HTTP 200 OK; result list contains only Analyzed requests.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.AdminServiceRequestsControllerFunctionalTests.Admin_FilterByStatus_ReturnsOnlyMatchingRequests"
    /// </summary>
    [Fact]
    public async Task Admin_FilterByStatus_ReturnsOnlyMatchingRequests()
    {
        var statusMatchId = Guid.NewGuid();
        var otherStatusId = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddRangeAsync(
                new ServiceRequest
                {
                    Id = statusMatchId,
                    CustomerId = Guid.NewGuid(),
                    Description = "Status match target",
                    LocationText = "Colombo",
                    Category = "Unclassified",
                    Urgency = ServiceRequestUrgency.Unknown,
                    Status = ServiceRequestStatus.Analyzed
                },
                new ServiceRequest
                {
                    Id = otherStatusId,
                    CustomerId = Guid.NewGuid(),
                    Description = "Other status request",
                    LocationText = "Colombo",
                    Category = "Unclassified",
                    Urgency = ServiceRequestUrgency.Unknown,
                    Status = ServiceRequestStatus.Cancelled
                }
            );
        });

        var adminClient = _factory.CreateAuthenticatedClient(Guid.NewGuid(), UserRole.Admin);

        var response = await adminClient.GetAsync("/api/admin/service-requests?status=Analyzed");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var requests = await response.Content.ReadFromJsonAsync<List<ServiceRequestResponse>>(_jsonOptions);
        Assert.NotNull(requests);
        Assert.Contains(requests, r => r.ServiceRequestId == statusMatchId);
        Assert.DoesNotContain(requests, r => r.ServiceRequestId == otherStatusId);
        Assert.All(requests, r => Assert.Equal(ServiceRequestStatus.Analyzed, r.Status));
    }

    /// <summary>
    /// Purpose: Checks that the category query parameter filters results to only matching service categories.
    /// Why: Enables domain-specific triage (Vehicle Repair, Appliance Repair, Plumbing, etc.).
    /// Scenario: Database has Vehicle Repair and Appliance Repair requests; Admin queries ?category=Vehicle Repair.
    /// Pass condition: HTTP 200 OK; result list contains only Vehicle Repair requests.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.AdminServiceRequestsControllerFunctionalTests.Admin_FilterByCategory_ReturnsOnlyMatchingRequests"
    /// </summary>
    [Fact]
    public async Task Admin_FilterByCategory_ReturnsOnlyMatchingRequests()
    {
        var catMatchId = Guid.NewGuid();
        var otherCatId = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddRangeAsync(
                new ServiceRequest
                {
                    Id = catMatchId,
                    CustomerId = Guid.NewGuid(),
                    Description = "Brake pads grinding",
                    LocationText = "Colombo",
                    Category = "Vehicle Repair",
                    Urgency = ServiceRequestUrgency.Medium,
                    Status = ServiceRequestStatus.Analyzed
                },
                new ServiceRequest
                {
                    Id = otherCatId,
                    CustomerId = Guid.NewGuid(),
                    Description = "Microwave sparking",
                    LocationText = "Colombo",
                    Category = "Appliance Repair",
                    Urgency = ServiceRequestUrgency.Medium,
                    Status = ServiceRequestStatus.Analyzed
                }
            );
        });

        var adminClient = _factory.CreateAuthenticatedClient(Guid.NewGuid(), UserRole.Admin);

        var response = await adminClient.GetAsync("/api/admin/service-requests?category=Vehicle%20Repair");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var requests = await response.Content.ReadFromJsonAsync<List<ServiceRequestResponse>>(_jsonOptions);
        Assert.NotNull(requests);
        Assert.Contains(requests, r => r.ServiceRequestId == catMatchId);
        Assert.DoesNotContain(requests, r => r.ServiceRequestId == otherCatId);
        Assert.All(requests, r => Assert.Equal("Vehicle Repair", r.Category));
    }

    /// <summary>
    /// Purpose: Checks that the urgency query parameter filters results to only matching urgency levels.
    /// Why: Allows administrators to quickly locate critical emergencies requiring immediate attention.
    /// Scenario: Database contains Critical and Low urgency requests; Admin requests ?urgency=Critical.
    /// Pass condition: HTTP 200 OK; result list contains only Critical requests.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.AdminServiceRequestsControllerFunctionalTests.Admin_FilterByUrgency_ReturnsOnlyMatchingRequests"
    /// </summary>
    [Fact]
    public async Task Admin_FilterByUrgency_ReturnsOnlyMatchingRequests()
    {
        var urgencyMatchId = Guid.NewGuid();
        var otherUrgencyId = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddRangeAsync(
                new ServiceRequest
                {
                    Id = urgencyMatchId,
                    CustomerId = Guid.NewGuid(),
                    Description = "Gas line leaking dangerously",
                    LocationText = "Galle",
                    Category = "Plumbing",
                    Urgency = ServiceRequestUrgency.Critical,
                    Status = ServiceRequestStatus.Analyzed
                },
                new ServiceRequest
                {
                    Id = otherUrgencyId,
                    CustomerId = Guid.NewGuid(),
                    Description = "Slight drip from garden hose",
                    LocationText = "Galle",
                    Category = "Plumbing",
                    Urgency = ServiceRequestUrgency.Low,
                    Status = ServiceRequestStatus.Analyzed
                }
            );
        });

        var adminClient = _factory.CreateAuthenticatedClient(Guid.NewGuid(), UserRole.Admin);

        var response = await adminClient.GetAsync("/api/admin/service-requests?urgency=Critical");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var requests = await response.Content.ReadFromJsonAsync<List<ServiceRequestResponse>>(_jsonOptions);
        Assert.NotNull(requests);
        Assert.Contains(requests, r => r.ServiceRequestId == urgencyMatchId);
        Assert.DoesNotContain(requests, r => r.ServiceRequestId == otherUrgencyId);
        Assert.All(requests, r => Assert.Equal(ServiceRequestUrgency.Critical, r.Urgency));
    }

    /// <summary>
    /// Purpose: Checks that combining status, category, and urgency filters enforces AND intersection logic.
    /// Why: Admins need multi-dimensional filtering to pinpoint specific requests.
    /// Scenario: Three requests with differing fields; Admin queries with ?status=Analyzed&category=Electrical&urgency=High.
    /// Pass condition: HTTP 200 OK; only the single record matching all 3 criteria is returned.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.AdminServiceRequestsControllerFunctionalTests.Admin_CombinedFilters_ReturnOnlyIntersection"
    /// </summary>
    [Fact]
    public async Task Admin_CombinedFilters_ReturnOnlyIntersection()
    {
        var perfectMatchId = Guid.NewGuid();
        var partialMatchId1 = Guid.NewGuid();
        var partialMatchId2 = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddRangeAsync(
                new ServiceRequest
                {
                    Id = perfectMatchId,
                    CustomerId = Guid.NewGuid(),
                    Description = "Main power trip with burning smell",
                    LocationText = "Colombo",
                    Category = "Electrical",
                    Urgency = ServiceRequestUrgency.High,
                    Status = ServiceRequestStatus.Analyzed
                },
                new ServiceRequest
                {
                    Id = partialMatchId1,
                    CustomerId = Guid.NewGuid(),
                    Description = "Light switch loose",
                    LocationText = "Colombo",
                    Category = "Electrical",
                    Urgency = ServiceRequestUrgency.Low, // Different urgency
                    Status = ServiceRequestStatus.Analyzed
                },
                new ServiceRequest
                {
                    Id = partialMatchId2,
                    CustomerId = Guid.NewGuid(),
                    Description = "Water pipe burst",
                    LocationText = "Colombo",
                    Category = "Plumbing", // Different category
                    Urgency = ServiceRequestUrgency.High,
                    Status = ServiceRequestStatus.Analyzed
                }
            );
        });

        var adminClient = _factory.CreateAuthenticatedClient(Guid.NewGuid(), UserRole.Admin);

        var response = await adminClient.GetAsync("/api/admin/service-requests?status=Analyzed&category=Electrical&urgency=High");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var requests = await response.Content.ReadFromJsonAsync<List<ServiceRequestResponse>>(_jsonOptions);
        Assert.NotNull(requests);
        Assert.Contains(requests, r => r.ServiceRequestId == perfectMatchId);
        Assert.DoesNotContain(requests, r => r.ServiceRequestId == partialMatchId1);
        Assert.DoesNotContain(requests, r => r.ServiceRequestId == partialMatchId2);
    }

    /// <summary>
    /// Purpose: Checks that invalid status filter values are rejected with HTTP 400 Bad Request.
    /// Why: Prevents silent failure or undefined behavior when invalid status strings are queried.
    /// Scenario: Admin passes invalid statuses: "Pending", "InProgress", "Done", "123".
    /// Pass condition: HTTP 400 Bad Request with response body mentioning "Status filter".
    /// Note: Parameterized test (Theory). The command below executes all 4 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.Api.Tests.AdminServiceRequestsControllerFunctionalTests.Admin_InvalidStatus_Returns400BadRequest"
    /// </summary>
    [Theory]
    [InlineData("Pending")]
    [InlineData("InProgress")]
    [InlineData("Done")]
    [InlineData("123")]
    public async Task Admin_InvalidStatus_Returns400BadRequest(string invalidStatus)
    {
        var adminClient = _factory.CreateAuthenticatedClient(Guid.NewGuid(), UserRole.Admin);

        var response = await adminClient.GetAsync($"/api/admin/service-requests?status={invalidStatus}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Status filter", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Purpose: Checks that invalid category filter values are rejected with HTTP 400 Bad Request.
    /// Why: Enforces strict adherence to the supported service category taxonomy.
    /// Scenario: Admin passes invalid categories: "Carpentry", "Cleaning", "Gardening", "RandomCategory".
    /// Pass condition: HTTP 400 Bad Request with response body mentioning "Category filter".
    /// Note: Parameterized test (Theory). The command below executes all 4 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.Api.Tests.AdminServiceRequestsControllerFunctionalTests.Admin_InvalidCategory_Returns400BadRequest"
    /// </summary>
    [Theory]
    [InlineData("Carpentry")]
    [InlineData("Cleaning")]
    [InlineData("Gardening")]
    [InlineData("RandomCategory")]
    public async Task Admin_InvalidCategory_Returns400BadRequest(string invalidCategory)
    {
        var adminClient = _factory.CreateAuthenticatedClient(Guid.NewGuid(), UserRole.Admin);

        var response = await adminClient.GetAsync($"/api/admin/service-requests?category={invalidCategory}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Category filter", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Purpose: Checks that invalid urgency filter values are rejected with HTTP 400 Bad Request.
    /// Why: Enforces validation of allowed urgency enumeration strings.
    /// Scenario: Admin passes invalid urgency values: "Immediate", "Urgent", "Severe", "999".
    /// Pass condition: HTTP 400 Bad Request with response body mentioning "Urgency filter".
    /// Note: Parameterized test (Theory). The command below executes all 4 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.Api.Tests.AdminServiceRequestsControllerFunctionalTests.Admin_InvalidUrgency_Returns400BadRequest"
    /// </summary>
    [Theory]
    [InlineData("Immediate")]
    [InlineData("Urgent")]
    [InlineData("Severe")]
    [InlineData("999")]
    public async Task Admin_InvalidUrgency_Returns400BadRequest(string invalidUrgency)
    {
        var adminClient = _factory.CreateAuthenticatedClient(Guid.NewGuid(), UserRole.Admin);

        var response = await adminClient.GetAsync($"/api/admin/service-requests?urgency={invalidUrgency}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Urgency filter", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Purpose: Checks that admin search results are returned in descending order of CreatedAt (newest first).
    /// Why: Ensures administrators see the latest incoming service requests at the top of the list.
    /// Scenario: Records created 3 hours ago, 1 hour ago, and 5 minutes ago are retrieved via GET.
    /// Pass condition: HTTP 200 OK and each item's CreatedAt is greater than or equal to the next item's CreatedAt.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.AdminServiceRequestsControllerFunctionalTests.Admin_ResultsAreOrderedNewestFirst"
    /// </summary>
    [Fact]
    public async Task Admin_ResultsAreOrderedNewestFirst()
    {
        var olderId = Guid.NewGuid();
        var newerId = Guid.NewGuid();
        var newestId = Guid.NewGuid();
        var baseTime = DateTime.UtcNow;

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddRangeAsync(
                new ServiceRequest
                {
                    Id = olderId,
                    CustomerId = Guid.NewGuid(),
                    Description = "Older request",
                    LocationText = "Colombo",
                    CreatedAt = baseTime.AddHours(-3)
                },
                new ServiceRequest
                {
                    Id = newestId,
                    CustomerId = Guid.NewGuid(),
                    Description = "Newest request",
                    LocationText = "Colombo",
                    CreatedAt = baseTime.AddMinutes(-5)
                },
                new ServiceRequest
                {
                    Id = newerId,
                    CustomerId = Guid.NewGuid(),
                    Description = "Newer request",
                    LocationText = "Colombo",
                    CreatedAt = baseTime.AddHours(-1)
                }
            );
        });

        var adminClient = _factory.CreateAuthenticatedClient(Guid.NewGuid(), UserRole.Admin);

        var response = await adminClient.GetAsync("/api/admin/service-requests");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var requests = await response.Content.ReadFromJsonAsync<List<ServiceRequestResponse>>(_jsonOptions);
        Assert.NotNull(requests);
        Assert.True(requests.Count >= 3);

        // Verify descending order
        for (int i = 0; i < requests.Count - 1; i++)
        {
            Assert.True(requests[i].CreatedAt >= requests[i + 1].CreatedAt,
                $"Expected CreatedAt at index {i} ({requests[i].CreatedAt}) to be >= index {i + 1} ({requests[i + 1].CreatedAt})");
        }
    }
}

