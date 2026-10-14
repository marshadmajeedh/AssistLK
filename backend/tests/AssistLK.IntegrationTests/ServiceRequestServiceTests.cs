using AssistLK.Application.Common.Exceptions;
using AssistLK.Application.Interfaces;
using AssistLK.Application.ServiceRequests.DTOs;
using AssistLK.Application.Services;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;

namespace AssistLK.IntegrationTests;

// ==============================================================================
// TEST SUITE: ServiceRequestServiceTests
// What this class tests:
//   - ServiceRequestService business logic in the application layer.
//   - Request creation defaults (Unclassified, Unknown urgency, Created status) and category hint validation.
//   - Request updates: customer ownership enforcement, editable status checks (Created, AwaitingInformation),
//     and concurrency protection (blocking updates during Analyzing status).
//   - Cancellation lifecycle rules and record preservation.
//   - Applying problem analysis results (updating category, urgency, status, and confidence range validation).
//   - Readiness contract for provider matching (GetReadyForMatchingAsync customer data redaction).
//   - Admin listing, filtering (status, category, urgency), and single-item retrieval.
// Why these tests are needed:
//   - Guarantees core business rules and lifecycle constraints for service requests are strictly enforced
//     independently of the database engine or web API controllers.
// What is real and what is mocked/faked:
//   - Real: ServiceRequestService application service.
//   - In-Memory/Faked: InMemoryServiceRequestRepository and InMemoryProblemAnalysisRepository (in-memory C# lists).
// Prerequisites:
//   - None (runs purely in-memory).
// Run from repository root:
//   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.ServiceRequestServiceTests"
// ==============================================================================
public class ServiceRequestServiceTests
{
    // Purpose: Checks that creating a service request initializes default server-controlled values (Unclassified, Urgency.Unknown, Status.Created).
    // Why: Prevents customers or clients from artificially setting status, urgency, or category on creation.
    // Scenario: Customer creates request with only description and location text.
    // Pass condition: Category is "Unclassified", Urgency is Unknown, Status is Created, CategoryHint is null, and a non-empty GUID is assigned.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestServiceTests.CreateAsync_UsesInitialValuesAndDoesNotAcceptServerFields"
    [Fact]
    public async Task CreateAsync_UsesInitialValuesAndDoesNotAcceptServerFields()
    {
        var repositories = CreateService();
        var customerId = Guid.NewGuid();

        var response = await repositories.Service.CreateAsync(
            customerId,
            new CreateServiceRequestRequest
            {
                Description = "Vehicle will not start",
                LocationText = "Colombo"
            });

        var request = repositories.Requests.Single();
        Assert.Equal(customerId, request.CustomerId);
        Assert.Null(request.CategoryHint);
        Assert.Null(response.CategoryHint);
        Assert.Equal("Unclassified", response.Category);
        Assert.Equal(ServiceRequestUrgency.Unknown, response.Urgency);
        Assert.Equal(ServiceRequestStatus.Created, response.Status);
        Assert.NotEqual(Guid.Empty, response.ServiceRequestId);
    }

    // Purpose: Checks that valid category hints (e.g. "Plumbing", "Electrical", "Vehicle Repair", "Appliance Repair", or blank/null) are persisted while keeping Category as "Unclassified".
    // Why: Customer category hints provide context to the AI agent but do not override the official classification until analysis completes.
    // Scenario: Creation with null, whitespace, or valid category taxonomy strings.
    // Pass condition: Persisted CategoryHint matches expected string, while Category remains "Unclassified". Note: This parameterized test runs all 7 InlineData cases.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.ServiceRequestServiceTests.CreateAsync_WithValidCategoryHints_PersistsHint_AndLeavesCategoryUnclassified"
    //This EP
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("Plumbing", "Plumbing")]
    [InlineData("Electrical", "Electrical")]
    [InlineData("Vehicle Repair", "Vehicle Repair")]
    [InlineData("Appliance Repair", "Appliance Repair")]
    public async Task CreateAsync_WithValidCategoryHints_PersistsHint_AndLeavesCategoryUnclassified(
        string? inputHint,
        string? expectedHint)
    {
        var repositories = CreateService();
        var customerId = Guid.NewGuid();

        var response = await repositories.Service.CreateAsync(
            customerId,
            new CreateServiceRequestRequest
            {
                Description = "Water leaking under sink",
                LocationText = "Colombo",
                CategoryHint = inputHint
            });

        var persisted = repositories.Requests.Single();
        Assert.Equal(expectedHint, persisted.CategoryHint);
        Assert.Equal(expectedHint, response.CategoryHint);
        Assert.Equal("Unclassified", persisted.Category);
        Assert.Equal("Unclassified", response.Category);
    }

    // Purpose: Checks that non-standard category hints (e.g. "Cleaning", "Carpentry", "random text") are rejected with ArgumentException.
    // Why: Prevents invalid or out-of-scope categories from entering the system taxonomy.
    // Scenario: Customer submits unsupported category hint on creation.
    // Pass condition: Throws ArgumentException with message containing "invalid". Note: This parameterized test runs all 9 InlineData cases.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.ServiceRequestServiceTests.CreateAsync_WithInvalidCategoryHints_ThrowsArgumentException"
    //This EP
    [Theory]
    [InlineData("Vehicle Assistance")]
    [InlineData("Cleaning")]
    [InlineData("AC Service")]
    [InlineData("Carpentry")]
    [InlineData("Gardening")]
    [InlineData("Work")]
    [InlineData("Emergency")]
    [InlineData("vehicle")]
    [InlineData("random text")]
    public async Task CreateAsync_WithInvalidCategoryHints_ThrowsArgumentException(string invalidHint)
    {
        var repositories = CreateService();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            repositories.Service.CreateAsync(
                Guid.NewGuid(),
                new CreateServiceRequestRequest
                {
                    Description = "Valid problem description",
                    LocationText = "Colombo",
                    CategoryHint = invalidHint
                }));

        Assert.Contains("invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Purpose: Checks that customer can update category hint in editable statuses (Created, AwaitingInformation) without altering the AI-assigned Category.
    // Why: Allows customers to clarify their hint while keeping the verified analysis category distinct.
    // Scenario: Request updated in Created status to "Electrical", moved to AwaitingInformation, then updated to "Appliance Repair".
    // Pass condition: CategoryHint reflects new updates while Category remains untouched.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestServiceTests.UpdateAsync_CanUpdateCategoryHint_InEditableStatuses_WithoutChangingCategory"
    [Fact]
    public async Task UpdateAsync_CanUpdateCategoryHint_InEditableStatuses_WithoutChangingCategory()
    {
        var repositories = CreateService();
        var customerId = Guid.NewGuid();
        var created = await repositories.Service.CreateAsync(
            customerId,
            new CreateServiceRequestRequest
            {
                Description = "Initial description",
                LocationText = "Colombo",
                CategoryHint = "Plumbing"
            });

        Assert.Equal("Plumbing", created.CategoryHint);
        Assert.Equal("Unclassified", created.Category);

        // Update in Created status to Electrical
        var updatedInCreated = await repositories.Service.UpdateAsync(
            customerId,
            created.ServiceRequestId,
            new UpdateServiceRequestRequest
            {
                Description = "Updated description",
                LocationText = "Colombo",
                CategoryHint = "Electrical"
            });

        Assert.Equal("Electrical", updatedInCreated.CategoryHint);
        Assert.Equal("Unclassified", updatedInCreated.Category);

        // Transition to AwaitingInformation
        var entity = repositories.Requests.Single();
        entity.Status = ServiceRequestStatus.AwaitingInformation;
        entity.Category = "Electrical"; // Simulate prior analysis result

        // Update in AwaitingInformation status to Appliance Repair
        var updatedInAwaiting = await repositories.Service.UpdateAsync(
            customerId,
            created.ServiceRequestId,
            new UpdateServiceRequestRequest
            {
                Description = "Further updated description",
                LocationText = "Colombo",
                CategoryHint = "Appliance Repair"
            });

        Assert.Equal("Appliance Repair", updatedInAwaiting.CategoryHint);
        Assert.Equal("Electrical", updatedInAwaiting.Category); // Category untouched by hint edit
    }

    // Purpose: Checks that updating an existing request with an invalid category hint is rejected with ArgumentException.
    // Why: Ensures update validation is as strict as creation validation for category taxonomy.
    // Scenario: Customer attempts to update CategoryHint to unsupported strings.
    // Pass condition: Throws ArgumentException. Note: This parameterized test runs all 3 InlineData cases.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.ServiceRequestServiceTests.UpdateAsync_WithInvalidCategoryHint_ThrowsArgumentException"
    [Theory]
    [InlineData("Vehicle Assistance")]
    [InlineData("Cleaning")]
    [InlineData("invalid")]
    public async Task UpdateAsync_WithInvalidCategoryHint_ThrowsArgumentException(string invalidHint)
    {
        var repositories = CreateService();
        var customerId = Guid.NewGuid();
        var created = await repositories.Service.CreateAsync(
            customerId,
            new CreateServiceRequestRequest
            {
                Description = "Initial description",
                LocationText = "Colombo"
            });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repositories.Service.UpdateAsync(
                customerId,
                created.ServiceRequestId,
                new UpdateServiceRequestRequest
                {
                    Description = "Valid update",
                    LocationText = "Colombo",
                    CategoryHint = invalidHint
                }));
    }

    // Purpose: Checks that empty description or partial/out-of-range coordinates are rejected with ArgumentException.
    // Why: Enforces data validation rules (description required, latitude and longitude must both be provided within valid ranges).
    // Scenario: Creation with empty description or latitude=91 (out of range [-90, 90]).
    // Pass condition: Throws ArgumentException for each invalid input.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestServiceTests.CreateAsync_RejectsInvalidFieldsAndPartialCoordinates"
    //This is BVB
    [Fact]
    public async Task CreateAsync_RejectsInvalidFieldsAndPartialCoordinates()
    {
        var repositories = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repositories.Service.CreateAsync(
                Guid.NewGuid(),
                new CreateServiceRequestRequest
                {
                    Description = "",
                    LocationText = "Colombo",
                    Latitude = 6.9m
                }));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repositories.Service.CreateAsync(
                Guid.NewGuid(),
                new CreateServiceRequestRequest
                {
                    Description = "Valid description",
                    LocationText = "Colombo",
                    Latitude = 91m,
                    Longitude = 80m
                }));
    }

    // Purpose: Checks that customer queries return only requests belonging to that customer and updates by other customers throw KeyNotFoundException.
    // Why: Enforces strict multi-tenant customer data isolation and authorization.
    // Scenario: Customer A requests their list (sees only owned request), and Customer B attempts to update Customer A's request.
    // Pass condition: Customer A receives only their 1 request; Customer B's update throws KeyNotFoundException.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestServiceTests.CustomerQueriesFilterByCustomerAndOwnershipIsRequiredForUpdates"
    //Owner ship
    [Fact]
    public async Task CustomerQueriesFilterByCustomerAndOwnershipIsRequiredForUpdates()
    {
        var repositories = CreateService();
        var owner = Guid.NewGuid();
        var otherCustomer = Guid.NewGuid();
        var owned = await CreateRequest(repositories.Service, owner, "Owned");
        await CreateRequest(repositories.Service, otherCustomer, "Other");

        var requests = await repositories.Service.GetCurrentCustomerRequestsAsync(owner);

        Assert.Single(requests);
        Assert.Equal(owned.ServiceRequestId, requests[0].ServiceRequestId);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            repositories.Service.UpdateAsync(
                otherCustomer,
                owned.ServiceRequestId,
                ValidUpdate()));
    }

    // Purpose: Checks that updating is allowed in Created and AwaitingInformation statuses, but blocked with ConflictException when status is Analyzing.
    // Why: Protects concurrent agent analysis by preventing customer edits while the AI agent is actively processing the request.
    // Scenario: Update in Created succeeds; update during Analyzing throws ConflictException; update in AwaitingInformation succeeds.
    // Pass condition: ConflictException is thrown during Analyzing, and successful updates return updated fields.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestServiceTests.UpdateAsync_AllowsOnlyEditableStatuses"
    [Fact]
    public async Task UpdateAsync_AllowsOnlyEditableStatuses()
    {
        var repositories = CreateService();
        var customerId = Guid.NewGuid();
        var response = await CreateRequest(repositories.Service, customerId, "Before");

        var updated = await repositories.Service.UpdateAsync(
            customerId,
            response.ServiceRequestId,
            new UpdateServiceRequestRequest
            {
                Description = "After",
                LocationText = "Kandy",
                Latitude = 7.29m,
                Longitude = 80.63m
            });

        Assert.Equal("After", updated.Description);
        Assert.Equal("Kandy", updated.LocationText);

        repositories.Requests.Single().Status = ServiceRequestStatus.Analyzing;
        await Assert.ThrowsAsync<ConflictException>(() =>
            repositories.Service.UpdateAsync(customerId, response.ServiceRequestId, ValidUpdate()));

        repositories.Requests.Single().Status = ServiceRequestStatus.AwaitingInformation;
        var awaitingUpdate = await repositories.Service.UpdateAsync(
            customerId,
            response.ServiceRequestId,
            ValidUpdate());
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, awaitingUpdate.Status);
    }

    // Purpose: Checks that a request can be cancelled in initial statuses, but not once it reaches ReadyForMatching, and that cancellation preserves the DB record.
    // Why: Prevents cancelling a request after provider matching has commenced and ensures audit history is preserved.
    // Scenario: Cancel in Created status succeeds; cancel in ReadyForMatching status throws ConflictException.
    // Pass condition: Status becomes Cancelled, record remains in repository, and cancelling from ReadyForMatching throws ConflictException.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestServiceTests.CancelAsync_AllowsOnlySpecifiedStatusesAndPreservesRecord"
    [Fact]
    public async Task CancelAsync_AllowsOnlySpecifiedStatusesAndPreservesRecord()
    {
        var repositories = CreateService();
        var customerId = Guid.NewGuid();
        var response = await CreateRequest(repositories.Service, customerId, "Cancel me");

        var cancelled = await repositories.Service.CancelAsync(
            customerId,
            response.ServiceRequestId);

        Assert.Equal(ServiceRequestStatus.Cancelled, cancelled.Status);
        Assert.Single(repositories.Requests);

        var ready = await CreateRequest(repositories.Service, customerId, "Ready");
        repositories.Requests.Single(x => x.Id == ready.ServiceRequestId).Status =
            ServiceRequestStatus.ReadyForMatching;
        await Assert.ThrowsAsync<ConflictException>(() =>
            repositories.Service.CancelAsync(customerId, ready.ServiceRequestId));
    }

    // Purpose: Checks that applying a successful AI problem analysis saves the analysis record and transitions the request to Analyzed with updated category and urgency.
    // Why: Core workflow step where the AI agent's findings are committed to the service request.
    // Scenario: Agent submits detected problem "Possible battery failure", category "Vehicle Repair", urgency High, confidence 0.85.
    // Pass condition: Analysis is added to repository, request category is "Vehicle Repair", urgency is High, and status is Analyzed.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestServiceTests.ApplyProblemAnalysisResult_PersistsResultAndUpdatesCategoryUrgencyAndStatus"
    //This is BVB
    [Fact]
    public async Task ApplyProblemAnalysisResult_PersistsResultAndUpdatesCategoryUrgencyAndStatus()
    {
        var repositories = CreateService();
        var response = await CreateRequest(repositories.Service, Guid.NewGuid(), "Analyze me");

        var analysis = await repositories.Service.ApplyProblemAnalysisResultAsync(
            new ApplyProblemAnalysisResult
            {
                ServiceRequestId = response.ServiceRequestId,
                DetectedProblem = "Possible battery failure",
                Category = "Vehicle Repair",
                Urgency = ServiceRequestUrgency.High,
                Confidence = 0.85m,
                AgentName = "ProblemUnderstandingAgent"
            });

        var request = repositories.Requests.Single();
        Assert.Single(repositories.Analyses);
        Assert.Equal("Possible battery failure", analysis.DetectedProblem);
        Assert.Equal("Vehicle Repair", request.Category);
        Assert.Equal(ServiceRequestUrgency.High, request.Urgency);
        Assert.Equal(ServiceRequestStatus.Analyzed, request.Status);
    }

    // Purpose: Checks that confidence > 1.0 is rejected, and when NeedsMoreInformation = true, status transitions to AwaitingInformation.
    // Why: Validates AI confidence bounds and verifies the clarification loop transition.
    // Scenario: Confidence 1.1 throws ArgumentException; result with NeedsMoreInformation = true sets status to AwaitingInformation.
    // Pass condition: ArgumentException thrown for confidence > 1; status transitions to AwaitingInformation.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestServiceTests.ApplyProblemAnalysisResult_RejectsInvalidConfidenceAndSupportsAwaitingInformation"
    [Fact]
    public async Task ApplyProblemAnalysisResult_RejectsInvalidConfidenceAndSupportsAwaitingInformation()
    {
        var repositories = CreateService();
        var response = await CreateRequest(repositories.Service, Guid.NewGuid(), "Need details");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repositories.Service.ApplyProblemAnalysisResultAsync(
                new ApplyProblemAnalysisResult
                {
                    ServiceRequestId = response.ServiceRequestId,
                    DetectedProblem = "Problem",
                    Category = "Home Repair",
                    Confidence = 1.1m,
                    AgentName = "Agent"
                }));

        await repositories.Service.ApplyProblemAnalysisResultAsync(
            new ApplyProblemAnalysisResult
            {
                ServiceRequestId = response.ServiceRequestId,
                DetectedProblem = "Need location detail",
                Category = "Home Repair",
                Urgency = ServiceRequestUrgency.Medium,
                Confidence = 0.5m,
                AgentName = "Agent",
                NeedsMoreInformation = true
            });

        Assert.Equal(
            ServiceRequestStatus.AwaitingInformation,
            repositories.Requests.Single().Status);
    }

    // Purpose: Checks that GetReadyForMatchingAsync returns null unless status is ReadyForMatching, and redacts customer ID from the matching DTO.
    // Why: Protects customer privacy and ensures the matching engine only receives vetted, fully analyzed requests.
    // Scenario: Called when status is Analyzed (returns null), then called when status is ReadyForMatching (returns matching DTO).
    // Pass condition: Returns null initially; once ReadyForMatching, returns populated DTO without customer ID property.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestServiceTests.GetReadyForMatchingAsync_ReturnsContractOnlyWhenReady"
    [Fact]
    public async Task GetReadyForMatchingAsync_ReturnsContractOnlyWhenReady()
    {
        var repositories = CreateService();
        var response = await CreateRequest(repositories.Service, Guid.NewGuid(), "Match me");
        repositories.Requests.Single().Status = ServiceRequestStatus.Analyzed;

        Assert.Null(await repositories.Service.GetReadyForMatchingAsync(response.ServiceRequestId));

        var analysis = new ProblemAnalysis
        {
            ServiceRequestId = response.ServiceRequestId,
            DetectedProblem = "Engine issue",
            Confidence = 0.92m,
            AgentName = "Agent",
            CreatedAt = DateTime.UtcNow
        };
        repositories.Analyses.Add(analysis);
        repositories.Requests.Single().Category = "Vehicle Repair";
        repositories.Requests.Single().Urgency = ServiceRequestUrgency.High;
        repositories.Requests.Single().Status = ServiceRequestStatus.ReadyForMatching;
        repositories.Requests.Single().LocationSource = LocationSource.OpenStreetMap;
        repositories.Requests.Single().Latitude = 6.905m;
        repositories.Requests.Single().Longitude = 79.9195m;

        var matching = await repositories.Service.GetReadyForMatchingAsync(response.ServiceRequestId);

        Assert.NotNull(matching);
        Assert.Equal("Colombo", matching.LocationText);
        Assert.Equal(LocationSource.OpenStreetMap, matching.LocationSource);
        Assert.Equal(6.905m, matching.Latitude);
        Assert.Equal(79.9195m, matching.Longitude);
        Assert.Equal("Engine issue", matching!.ProblemSummary);
        Assert.Equal(ServiceRequestStatus.ReadyForMatching, matching.Status);
        Assert.DoesNotContain("CustomerId", matching.GetType().GetProperties().Select(x => x.Name));
    }

    // Purpose: Checks that admin requests query returns all service requests across all customers.
    // Why: Admins need global visibility across the entire system.
    // Scenario: Two requests created under two different customer IDs; admin lists all.
    // Pass condition: Returns 2 requests.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestServiceTests.GetAllForAdminAsync_ReturnsAllRequestsAcrossCustomers"
    [Fact]
    public async Task GetAllForAdminAsync_ReturnsAllRequestsAcrossCustomers()
    {
        var repositories = CreateService();
        var customer1 = Guid.NewGuid();
        var customer2 = Guid.NewGuid();

        await CreateRequest(repositories.Service, customer1, "Issue 1");
        await CreateRequest(repositories.Service, customer2, "Issue 2");

        var results = await repositories.Service.GetAllForAdminAsync();

        Assert.Equal(2, results.Count);
    }

    // Purpose: Checks that admin list query correctly filters by status, category, urgency, and combinations thereof.
    // Why: Enables admins to efficiently filter and triage service requests by operational criteria.
    // Scenario: 2 requests with different category, urgency, and status; filtered individually and combined.
    // Pass condition: Filter by plumbing returns 1; filter by analyzed returns 1; filter by high urgency returns 1; combined returns 1; non-matching returns 0.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestServiceTests.GetAllForAdminAsync_FiltersCorrectly"
    [Fact]
    public async Task GetAllForAdminAsync_FiltersCorrectly()
    {
        var repositories = CreateService();
        var c1 = Guid.NewGuid();
        var req1 = await CreateRequest(repositories.Service, c1, "Plumbing request");
        var req2 = await CreateRequest(repositories.Service, c1, "Electrical request");

        repositories.Requests.Single(x => x.Id == req1.ServiceRequestId).Category = "Plumbing";
        repositories.Requests.Single(x => x.Id == req1.ServiceRequestId).Status = ServiceRequestStatus.Analyzed;
        repositories.Requests.Single(x => x.Id == req1.ServiceRequestId).Urgency = ServiceRequestUrgency.High;

        repositories.Requests.Single(x => x.Id == req2.ServiceRequestId).Category = "Electrical";
        repositories.Requests.Single(x => x.Id == req2.ServiceRequestId).Status = ServiceRequestStatus.Created;
        repositories.Requests.Single(x => x.Id == req2.ServiceRequestId).Urgency = ServiceRequestUrgency.Low;

        var plumbingOnly = await repositories.Service.GetAllForAdminAsync(category: "Plumbing");
        Assert.Single(plumbingOnly);
        Assert.Equal(req1.ServiceRequestId, plumbingOnly[0].ServiceRequestId);

        var analyzedOnly = await repositories.Service.GetAllForAdminAsync(status: "Analyzed");
        Assert.Single(analyzedOnly);
        Assert.Equal(req1.ServiceRequestId, analyzedOnly[0].ServiceRequestId);

        var highOnly = await repositories.Service.GetAllForAdminAsync(urgency: "High");
        Assert.Single(highOnly);
        Assert.Equal(req1.ServiceRequestId, highOnly[0].ServiceRequestId);

        var combined = await repositories.Service.GetAllForAdminAsync(status: "Analyzed", category: "Plumbing", urgency: "High");
        Assert.Single(combined);

        var nonMatching = await repositories.Service.GetAllForAdminAsync(status: "Cancelled");
        Assert.Empty(nonMatching);
    }

    // Purpose: Checks that invalid filter values (unknown status, category, or urgency strings) throw ArgumentException.
    // Why: Prevents malformed queries from executing or failing silently.
    // Scenario: Admin passes invalid status, invalid category, or invalid urgency.
    // Pass condition: Throws ArgumentException. Note: This parameterized test runs all 3 InlineData cases.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.ServiceRequestServiceTests.GetAllForAdminAsync_InvalidFilters_ThrowArgumentException"
    [Theory]
    [InlineData("InvalidStatus", null, null)]
    [InlineData(null, "InvalidCategory", null)]
    [InlineData(null, null, "InvalidUrgency")]
    public async Task GetAllForAdminAsync_InvalidFilters_ThrowArgumentException(string? status, string? category, string? urgency)
    {
        var repositories = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repositories.Service.GetAllForAdminAsync(status, category, urgency));
    }

    // Purpose: Checks that an admin can retrieve detailed service request information regardless of which customer owns it.
    // Why: Admin support and problem investigation require customer-agnostic retrieval.
    // Scenario: Request created under customer A; admin fetches by request ID.
    // Pass condition: Returns full request detail with matching request ID and customer ID.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestServiceTests.GetByIdForAdminAsync_ReturnsDetail_RegardlessOfCustomer"
    [Fact]
    public async Task GetByIdForAdminAsync_ReturnsDetail_RegardlessOfCustomer()
    {
        var repositories = CreateService();
        var customerId = Guid.NewGuid();
        var created = await CreateRequest(repositories.Service, customerId, "Test request");

        var result = await repositories.Service.GetByIdForAdminAsync(created.ServiceRequestId);

        Assert.NotNull(result);
        Assert.Equal(created.ServiceRequestId, result.ServiceRequestId);
        Assert.Equal(customerId, result.CustomerId);
    }

    // Purpose: Checks that requesting a non-existent request ID as admin throws KeyNotFoundException.
    // Why: Signals clearly that the requested entity does not exist.
    // Scenario: Admin queries random non-existent GUID.
    // Pass condition: Throws KeyNotFoundException.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestServiceTests.GetByIdForAdminAsync_NotFound_ThrowsKeyNotFoundException"
    [Fact]
    public async Task GetByIdForAdminAsync_NotFound_ThrowsKeyNotFoundException()
    {
        var repositories = CreateService();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            repositories.Service.GetByIdForAdminAsync(Guid.NewGuid()));
    }

    private static (ServiceRequestService Service, List<ServiceRequest> Requests, List<ProblemAnalysis> Analyses) CreateService()
    {
        var requests = new List<ServiceRequest>();
        var analyses = new List<ProblemAnalysis>();
        return (
            new ServiceRequestService(
                new InMemoryServiceRequestRepository(requests, analyses),
                new InMemoryProblemAnalysisRepository(analyses)),
            requests,
            analyses);
    }

    private static async Task<ServiceRequestResponse> CreateRequest(
        ServiceRequestService service,
        Guid customerId,
        string description)
    {
        return await service.CreateAsync(
            customerId,
            new CreateServiceRequestRequest
            {
                Description = description,
                LocationText = "Colombo"
            });
    }

    private static UpdateServiceRequestRequest ValidUpdate() => new()
    {
        Description = "Updated description",
        LocationText = "Colombo"
    };

    private sealed class InMemoryServiceRequestRepository : IServiceRequestRepository
    {
        private readonly List<ServiceRequest> _requests;
        private readonly List<ProblemAnalysis> _analyses;

        public InMemoryServiceRequestRepository(
            List<ServiceRequest> requests,
            List<ProblemAnalysis> analyses)
        {
            _requests = requests;
            _analyses = analyses;
        }

        public Task<ServiceRequest?> GetByIdAsync(
            Guid id,
            bool includeProblemAnalyses = false,
            CancellationToken cancellationToken = default)
        {
            var request = _requests.SingleOrDefault(x => x.Id == id);
            if (includeProblemAnalyses && request is not null)
            {
                request.ProblemAnalyses = _analyses
                    .Where(x => x.ServiceRequestId == id)
                    .ToList();
            }

            return Task.FromResult(request);
        }

        public Task<ServiceRequest?> GetByIdAndCustomerIdAsync(Guid id, Guid customerId, bool includeProblemAnalyses = false, CancellationToken cancellationToken = default)
        {
            var request = _requests.SingleOrDefault(x => x.Id == id && x.CustomerId == customerId);
            if (includeProblemAnalyses && request is not null)
            {
                request.ProblemAnalyses = _analyses
                    .Where(x => x.ServiceRequestId == id)
                    .ToList();
            }

            return Task.FromResult(request);
        }

        public Task<IReadOnlyList<ServiceRequest>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ServiceRequest>>(_requests.Where(x => x.CustomerId == customerId).ToArray());

        public Task<IReadOnlyList<ServiceRequest>> GetByStatusAsync(ServiceRequestStatus status, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ServiceRequest>>(_requests.Where(x => x.Status == status).ToArray());

        public Task<IReadOnlyList<ServiceRequest>> GetAllForAdminAsync(
            ServiceRequestStatus? status = null,
            string? category = null,
            ServiceRequestUrgency? urgency = null,
            CancellationToken cancellationToken = default)
        {
            var query = _requests.AsEnumerable();

            if (status.HasValue)
            {
                query = query.Where(x => x.Status == status.Value);
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(x => string.Equals(x.Category, category, StringComparison.OrdinalIgnoreCase));
            }

            if (urgency.HasValue)
            {
                query = query.Where(x => x.Urgency == urgency.Value);
            }

            foreach (var req in query)
            {
                req.ProblemAnalyses = _analyses.Where(a => a.ServiceRequestId == req.Id).ToList();
            }

            return Task.FromResult<IReadOnlyList<ServiceRequest>>(
                query.OrderByDescending(x => x.CreatedAt).ToArray());
        }

        public Task AddAsync(ServiceRequest serviceRequest, CancellationToken cancellationToken = default)
        {
            _requests.Add(serviceRequest);
            return Task.CompletedTask;
        }

        public void Update(ServiceRequest serviceRequest)
        {
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryProblemAnalysisRepository : IProblemAnalysisRepository
    {
        private readonly List<ProblemAnalysis> _analyses;

        public InMemoryProblemAnalysisRepository(List<ProblemAnalysis> analyses)
        {
            _analyses = analyses;
        }

        public Task<IReadOnlyList<ProblemAnalysis>> GetByServiceRequestIdAsync(Guid serviceRequestId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProblemAnalysis>>(_analyses.Where(x => x.ServiceRequestId == serviceRequestId).ToArray());

        public Task<ProblemAnalysis?> GetMostRecentByServiceRequestIdAsync(Guid serviceRequestId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_analyses.Where(x => x.ServiceRequestId == serviceRequestId).OrderByDescending(x => x.CreatedAt).FirstOrDefault());

        public Task AddAsync(ProblemAnalysis problemAnalysis, CancellationToken cancellationToken = default)
        {
            _analyses.Add(problemAnalysis);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
