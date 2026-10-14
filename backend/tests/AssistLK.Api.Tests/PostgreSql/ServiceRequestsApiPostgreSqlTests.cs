using System.Net;
using System.Net.Http.Json;
using AssistLK.Api.DTOs.ServiceRequests;
using AssistLK.Application.ServiceRequests.DTOs;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AssistLK.Api.Tests.PostgreSql;

/// <summary>
/// ServiceRequestsApiPostgreSqlTests verifies the Service Requests HTTP API
/// against a real PostgreSQL database instance.
///
/// Viva Preparation Notes:
/// - What this class tests: Full end-to-end integration across the HTTP layer, controllers,
///   application services, EF Core DbContext, and a live PostgreSQL database for customer requests.
/// - Why these tests are needed: In-memory tests verify routing and logic, but only PostgreSQL tests
///   verify real SQL execution, relational foreign keys, schema migrations, and column types.
/// - What is real vs mocked:
///   * Real: ASP.NET Core test server, HTTP pipeline, EF Core Npgsql provider, and real PostgreSQL database.
///   * Mocked/Faked: AI agent client (FakeProblemUnderstandingClient) and SMS service (TestSmsService).
/// - Prerequisites: A running PostgreSQL instance with connection string configured in
///   ASSISTLK_TEST_POSTGRESQL_CONNECTION, ConnectionStrings__TestConnection, or User Secrets.
/// - PowerShell command to run class from repository root:
///   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.Api.Tests.PostgreSql.ServiceRequestsApiPostgreSqlTests"
/// </summary>
[Collection("EnvironmentTests")]
public class ServiceRequestsApiPostgreSqlTests : IClassFixture<PostgreSqlAssistLKApiTestFactory>, IAsyncLifetime
{
    private readonly PostgreSqlAssistLKApiTestFactory _factory;
    private readonly System.Text.Json.JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public ServiceRequestsApiPostgreSqlTests(PostgreSqlAssistLKApiTestFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Purpose: Checks that POST /api/service-requests creates a record in real PostgreSQL and returns 201 Created.
    /// Why: Proves real database insertion, ID generation, and schema constraint adherence over HTTP.
    /// Scenario: Customer submits description and GPS coordinates to the API.
    /// Pass condition: HTTP 201 Created, response has status Created, and direct DB query confirms record in PostgreSQL.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.PostgreSql.ServiceRequestsApiPostgreSqlTests.CreateServiceRequest_OverHttp_PersistsInRealPostgreSql_AndReturns201Created"
    /// </summary>
    [Fact]
    public async Task CreateServiceRequest_OverHttp_PersistsInRealPostgreSql_AndReturns201Created()
    {
        var customerId = Guid.NewGuid();
        await _factory.SeedUserAsync(customerId, email: "http_customer1@assistlk.com");

        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);

        var payload = new CreateServiceRequestRequest
        {
            Description = "Water pipe leakage in kitchen",
            LocationText = "Colombo 05",
            Latitude = 6.8921m,
            Longitude = 79.8654m
        };

        var response = await client.PostAsJsonAsync("api/service-requests", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.ServiceRequestId);
        Assert.Equal(customerId, created.CustomerId);
        Assert.Equal("Water pipe leakage in kitchen", created.Description);
        Assert.Equal(ServiceRequestStatus.Created, created.Status);

        // Verify in real PostgreSQL database
        await using var context = _factory.CreateDbContext();
        var savedInDb = await context.ServiceRequests.FindAsync(created.ServiceRequestId);
        Assert.NotNull(savedInDb);
        Assert.Equal(customerId, savedInDb.CustomerId);
        Assert.Equal("Colombo 05", savedInDb.LocationText);
        Assert.Equal(ServiceRequestStatus.Created, savedInDb.Status);
    }

    /// <summary>
    /// Purpose: Checks customer ownership isolation over HTTP backed by a real PostgreSQL database.
    /// Why: Verifies that multi-tenant isolation holds in the presence of real database foreign keys and queries.
    /// Scenario: Customer A creates request; Customer A queries it (200 OK); Customer B queries it (404 Not Found).
    /// Pass condition: Customer A receives 200 OK; Customer B receives 404 Not Found.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.PostgreSql.ServiceRequestsApiPostgreSqlTests.GetById_OverHttp_Returns200ForOwner_And404ForDifferentCustomer"
    /// </summary>
    [Fact]
    public async Task GetById_OverHttp_Returns200ForOwner_And404ForDifferentCustomer()
    {
        var customerAId = Guid.NewGuid();
        var customerBId = Guid.NewGuid();

        await _factory.SeedUserAsync(customerAId, email: "owner_a@assistlk.com");
        await _factory.SeedUserAsync(customerBId, email: "intruder_b@assistlk.com");

        var clientA = _factory.CreateAuthenticatedClient(customerAId, UserRole.Customer);
        var clientB = _factory.CreateAuthenticatedClient(customerBId, UserRole.Customer);

        // Customer A creates request
        var createResponse = await clientA.PostAsJsonAsync("api/service-requests", new CreateServiceRequestRequest
        {
            Description = "Roof tiles damaged by storm",
            LocationText = "Galle Fort"
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(created);

        // Customer A retrieves own request -> 200 OK
        var getForOwnerResponse = await clientA.GetAsync($"api/service-requests/{created.ServiceRequestId}");
        Assert.Equal(HttpStatusCode.OK, getForOwnerResponse.StatusCode);

        // Customer B attempts to retrieve Customer A's request -> 404 Not Found (ownership isolation)
        var getForOtherResponse = await clientB.GetAsync($"api/service-requests/{created.ServiceRequestId}");
        Assert.Equal(HttpStatusCode.NotFound, getForOtherResponse.StatusCode);
    }

    /// <summary>
    /// Purpose: Checks analysis execution, persistence of ProblemAnalysis in PostgreSQL, and transition to ReadyForMatching.
    /// Why: Validates the entire analysis and handoff pipeline with real relational database storage.
    /// Scenario: Customer creates request, triggers analysis over HTTP, verifies ProblemAnalysis entity in PostgreSQL, then triggers ready-for-matching.
    /// Pass condition: Status is Analyzed, ProblemAnalysis row exists with matching summary and confidence in PostgreSQL, and final status is ReadyForMatching.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.PostgreSql.ServiceRequestsApiPostgreSqlTests.Analyze_OverHttp_ExecutesWorkflow_PersistsAnalysis_AndReturnsSafeDto"
    /// </summary>
    [Fact]
    public async Task Analyze_OverHttp_ExecutesWorkflow_PersistsAnalysis_AndReturnsSafeDto()
    {
        var customerId = Guid.NewGuid();
        await _factory.SeedUserAsync(customerId, email: "analyze_customer@assistlk.com");

        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);

        // 1. Create request
        var createResponse = await client.PostAsJsonAsync("api/service-requests", new CreateServiceRequestRequest
        {
            Description = "Kitchen pipe is leaking under sink and flooding floor",
            LocationText = "Colombo 03",
            Latitude = 6.905m,
            Longitude = 79.86m
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(created);

        // 2. Trigger analysis over HTTP
        var analyzeResponse = await client.PostAsync($"api/service-requests/{created.ServiceRequestId}/analyze", null);
        Assert.Equal(HttpStatusCode.OK, analyzeResponse.StatusCode);

        var dto = await analyzeResponse.Content.ReadFromJsonAsync<ProblemUnderstandingResponseDto>(_jsonOptions);
        Assert.NotNull(dto);
        Assert.Equal(created.ServiceRequestId, dto.ServiceRequestId);
        Assert.Equal(ServiceRequestStatus.Analyzed, dto.Status);
        Assert.Equal("Plumbing", dto.Category);
        Assert.Equal(ServiceRequestUrgency.High, dto.Urgency);
        Assert.True(dto.Confidence >= 0.80m);
        Assert.False(dto.NeedsMoreInformation);

        // 3. Verify real PostgreSQL state
        await using var context = _factory.CreateDbContext();
        var requestInDb = await context.ServiceRequests
            .Include(r => r.ProblemAnalyses)
            .SingleOrDefaultAsync(r => r.Id == created.ServiceRequestId);

        Assert.NotNull(requestInDb);
        Assert.Equal(ServiceRequestStatus.Analyzed, requestInDb.Status);
        Assert.Single(requestInDb.ProblemAnalyses);

        var analysisInDb = requestInDb.ProblemAnalyses.First();
        Assert.Equal(dto.ProblemSummary, analysisInDb.DetectedProblem);
        Assert.Equal(dto.Confidence, analysisInDb.Confidence);

        // 4. Mark ReadyForMatching over HTTP
        var readyResponse = await client.PostAsync($"api/service-requests/{created.ServiceRequestId}/ready-for-matching", null);
        Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);

        var readyDto = await readyResponse.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(readyDto);
        Assert.Equal(ServiceRequestStatus.ReadyForMatching, readyDto.Status);

        // Verify in DB that status is ReadyForMatching
        await using var verifyContext = _factory.CreateDbContext();
        var finalInDb = await verifyContext.ServiceRequests.FindAsync(created.ServiceRequestId);
        Assert.NotNull(finalInDb);
        Assert.Equal(ServiceRequestStatus.ReadyForMatching, finalInDb.Status);
    }

    /// <summary>
    /// Purpose: Checks that ambiguous descriptions yield AwaitingInformation status and follow-up questions in PostgreSQL.
    /// Why: Guarantees ambiguous issues are prevented from progressing to provider matching in the real database.
    /// Scenario: Customer posts ambiguous "refrigerator" description and triggers analysis.
    /// Pass condition: Status is AwaitingInformation in HTTP response and PostgreSQL, NeedsMoreInformation is True, follow-up questions populated.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.PostgreSql.ServiceRequestsApiPostgreSqlTests.AmbiguousRefrigeratorRequest_OverHttp_ReturnsAwaitingInformation_WithQuestions"
    /// </summary>
    [Fact]
    public async Task AmbiguousRefrigeratorRequest_OverHttp_ReturnsAwaitingInformation_WithQuestions()
    {
        var customerId = Guid.NewGuid();
        await _factory.SeedUserAsync(customerId, email: "fridge_http@assistlk.com");

        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);

        var createResponse = await client.PostAsJsonAsync("api/service-requests", new CreateServiceRequestRequest
        {
            Description = "refrigerator",
            LocationText = "Kandy"
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(created);

        // Analyze ambiguous request
        var analyzeResponse = await client.PostAsync($"api/service-requests/{created.ServiceRequestId}/analyze", null);
        Assert.Equal(HttpStatusCode.OK, analyzeResponse.StatusCode);

        var dto = await analyzeResponse.Content.ReadFromJsonAsync<ProblemUnderstandingResponseDto>(_jsonOptions);
        Assert.NotNull(dto);
        Assert.Equal("Appliance Repair", dto.Category);
        Assert.True(dto.NeedsMoreInformation);
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, dto.Status);
        Assert.NotEmpty(dto.FollowUpQuestions);

        // Verify status in PostgreSQL
        await using var context = _factory.CreateDbContext();
        var req = await context.ServiceRequests.FindAsync(created.ServiceRequestId);
        Assert.NotNull(req);
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, req.Status);
    }

    /// <summary>
    /// Purpose: Checks submitting clarification answers via HTTP, verifying they persist in PostgreSQL and restore on GET.
    /// Why: Ensures customer answers are durably stored in the relational schema and accurately returned in detail projections.
    /// Scenario: Ambiguous request analyzed -> GET details -> Submit answers payload -> GET details reloaded.
    /// Pass condition: HTTP 200 OK on submission, answered questions returned, and reload GET confirms answers are persisted in PostgreSQL.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.PostgreSql.ServiceRequestsApiPostgreSqlTests.SubmitClarificationAnswers_OverHttp_PersistsInPostgreSql_AndRestoresOnGet"
    /// </summary>
    [Fact]
    public async Task SubmitClarificationAnswers_OverHttp_PersistsInPostgreSql_AndRestoresOnGet()
    {
        var customerId = Guid.NewGuid();
        await _factory.SeedUserAsync(customerId, email: "clarify_http@assistlk.com");

        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);

        // 1. Create ambiguous request
        var createResponse = await client.PostAsJsonAsync("api/service-requests", new CreateServiceRequestRequest
        {
            Description = "refrigerator not working",
            LocationText = "Colombo"
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(created);

        // 2. Initial analysis generates clarification questions
        var analyzeResponse = await client.PostAsync($"api/service-requests/{created.ServiceRequestId}/analyze", null);
        Assert.Equal(HttpStatusCode.OK, analyzeResponse.StatusCode);

        // 3. GET request details - verify clarifications and latestAnalysis restoration
        var getResponse = await client.GetAsync($"api/service-requests/{created.ServiceRequestId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var detailDto = await getResponse.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(detailDto);
        Assert.NotNull(detailDto.LatestAnalysis);
        Assert.NotNull(detailDto.Clarifications);
        Assert.NotEmpty(detailDto.Clarifications);

        var firstQuestion = detailDto.Clarifications.First();
        Assert.Null(firstQuestion.Answer);

        // 4. Submit answers over HTTP
        var answersPayload = new SubmitClarificationAnswersRequest
        {
            ClarificationRound = firstQuestion.ClarificationRound,
            Answers = detailDto.Clarifications.Select(q => new ClarificationAnswerSubmissionItem
            {
                ClarificationId = q.Id,
                Answer = "The compressor makes a clicking noise and the fridge does not cool."
            }).ToList()
        };

        var submitResponse = await client.PostAsJsonAsync(
            $"api/service-requests/{created.ServiceRequestId}/clarifications/answers",
            answersPayload);
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        var answeredList = await submitResponse.Content.ReadFromJsonAsync<List<ServiceRequestClarificationDto>>(_jsonOptions);
        Assert.NotNull(answeredList);
        Assert.All(answeredList, a => Assert.NotNull(a.Answer));

        // 5. Reload via GET - verify answers are restored without fabricated data
        var reloadedResponse = await client.GetAsync($"api/service-requests/{created.ServiceRequestId}");
        var reloadedDto = await reloadedResponse.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(reloadedDto);
        Assert.NotNull(reloadedDto.Clarifications);
        Assert.All(reloadedDto.Clarifications, c => Assert.NotNull(c.Answer));
        Assert.Equal(detailDto.LatestAnalysis.Confidence, reloadedDto.LatestAnalysis?.Confidence);
    }
}
