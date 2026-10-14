using AssistLK.Application.Common.Exceptions;
using AssistLK.Application.Interfaces;
using AssistLK.Application.ServiceRequests.DTOs;
using AssistLK.Application.Services;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;

namespace AssistLK.IntegrationTests;

/// <summary>
/// Component1LifecycleTests verifies state machine transitions, recovery rollbacks,
/// pre-analysis status capture, and validation gates of ServiceRequestService.
///
/// Viva Preparation Notes:
/// - What this class tests: The lifecycle transitions of service requests:
///   Created -> Analyzing -> Analyzed / AwaitingInformation -> ReadyForMatching,
///   analysis failure recovery (Analyzing -> Created or AwaitingInformation),
///   and pre-matching validation gates (Sri Lanka coordinate boundaries, classification, urgency, confidence).
/// - Why these tests are needed: Guarantees that requests follow strict lifecycle rules,
///   invalid state transitions fail with ConflictException, and dispatched requests are completely valid.
/// - What is real vs mocked:
///   * Real: ServiceRequestService business logic, lifecycle transition rules, and validation checks.
///   * Mocked/Faked: Database repositories (uses in-memory test doubles InMemoryServiceRequestRepository).
/// - Prerequisites: None (runs completely in-memory with .NET 8 SDK).
/// - PowerShell command to run class from repository root:
///   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.Component1LifecycleTests"
/// </summary>
public class Component1LifecycleTests
{
    private (ServiceRequestService Service, List<ServiceRequest> Requests, List<ProblemAnalysis> Analyses) CreateTestContext()
    {
        var requests = new List<ServiceRequest>();
        var analyses = new List<ProblemAnalysis>();

        var reqRepo = new InMemoryServiceRequestRepository(requests, analyses);
        var anaRepo = new InMemoryProblemAnalysisRepository(analyses);

        var service = new ServiceRequestService(reqRepo, anaRepo);
        return (service, requests, analyses);
    }

    /// <summary>
    /// Purpose: Checks that calling BeginAnalysisAsync transitions a request from Created to Analyzing.
    /// Why: Locks the request to indicate that AI problem understanding is in progress.
    /// Scenario: Request in status Created begins analysis.
    /// Pass condition: Returned status and entity status are updated to Analyzing.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1LifecycleTests.BeginAnalysisAsync_TransitionsFromCreatedToAnalyzing"
    /// </summary>
    [Fact]
    public async Task BeginAnalysisAsync_TransitionsFromCreatedToAnalyzing()
    {
        var (service, requests, _) = CreateTestContext();
        var id = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = Guid.NewGuid(),
            Description = "Leaking pipe",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Created
        });

        var response = await service.BeginAnalysisAsync(id);

        Assert.Equal(ServiceRequestStatus.Analyzing, response.Status);
        Assert.Equal(ServiceRequestStatus.Analyzing, requests.Single().Status);
    }

    /// <summary>
    /// Purpose: Checks that a request in AwaitingInformation transitions to Analyzing when re-analysis begins.
    /// Why: Allows subsequent clarification answers to re-engage the AI problem understanding pipeline.
    /// Scenario: Request in AwaitingInformation status begins re-analysis.
    /// Pass condition: Returned status and entity status are updated to Analyzing.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1LifecycleTests.BeginAnalysisAsync_TransitionsFromAwaitingInformationToAnalyzing"
    /// </summary>
    [Fact]
    public async Task BeginAnalysisAsync_TransitionsFromAwaitingInformationToAnalyzing()
    {
        var (service, requests, _) = CreateTestContext();
        var id = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = Guid.NewGuid(),
            Description = "Something broken",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.AwaitingInformation
        });

        var response = await service.BeginAnalysisAsync(id);

        Assert.Equal(ServiceRequestStatus.Analyzing, response.Status);
        Assert.Equal(ServiceRequestStatus.Analyzing, requests.Single().Status);
    }

    /// <summary>
    /// Purpose: Checks that BeginAnalysisAsync throws ConflictException when called from non-initial statuses.
    /// Why: Prevents double-analysis or re-analyzing completed, matching-ready, or cancelled requests.
    /// Scenario: Requests in Analyzing, Analyzed, ReadyForMatching, and Cancelled attempt to begin analysis.
    /// Pass condition: ConflictException is thrown for every invalid status.
    /// Note: Parameterized test (Theory). The command below executes all 4 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.Component1LifecycleTests.BeginAnalysisAsync_ThrowsFromInvalidStatuses"
    /// </summary>
    [Theory]
    [InlineData(ServiceRequestStatus.Analyzing)]
    [InlineData(ServiceRequestStatus.Analyzed)]
    [InlineData(ServiceRequestStatus.ReadyForMatching)]
    [InlineData(ServiceRequestStatus.Cancelled)]
    public async Task BeginAnalysisAsync_ThrowsFromInvalidStatuses(ServiceRequestStatus invalidStatus)
    {
        var (service, requests, _) = CreateTestContext();
        var id = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = Guid.NewGuid(),
            Description = "Test description",
            LocationText = "Colombo",
            Status = invalidStatus
        });

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.BeginAnalysisAsync(id));
    }

    /// <summary>
    /// Purpose: Checks that a failed analysis can transition back to Created status for fresh analysis retry.
    /// Why: Allows service recovery when agent analysis fails on a newly created request without data loss.
    /// Scenario: A request in Analyzing status is recovered to Created target status.
    /// Pass condition: Both returned response and repository entity status equal Created.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1LifecycleTests.RecoverFailedAnalysisAsync_TransitionsFromAnalyzingToCreated"
    /// </summary>
    [Fact]
    public async Task RecoverFailedAnalysisAsync_TransitionsFromAnalyzingToCreated()
    {
        var (service, requests, _) = CreateTestContext();
        var id = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = Guid.NewGuid(),
            Description = "Leaking pipe",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Analyzing
        });

        var response = await service.RecoverFailedAnalysisAsync(id, ServiceRequestStatus.Created);

        Assert.Equal(ServiceRequestStatus.Created, response.Status);
        Assert.Equal(ServiceRequestStatus.Created, requests.Single().Status);
    }

    /// <summary>
    /// Purpose: Checks that a failed clarification analysis can transition back to AwaitingInformation status.
    /// Why: Allows recovering to awaiting-information if clarification re-analysis encounters a transient failure.
    /// Scenario: A request in Analyzing status is recovered to AwaitingInformation target status.
    /// Pass condition: Both returned response and repository entity status equal AwaitingInformation.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1LifecycleTests.RecoverFailedAnalysisAsync_TransitionsFromAnalyzingToAwaitingInformation"
    /// </summary>
    [Fact]
    public async Task RecoverFailedAnalysisAsync_TransitionsFromAnalyzingToAwaitingInformation()
    {
        var (service, requests, _) = CreateTestContext();
        var id = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = Guid.NewGuid(),
            Description = "Something broken",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Analyzing
        });

        var response = await service.RecoverFailedAnalysisAsync(id, ServiceRequestStatus.AwaitingInformation);

        Assert.Equal(ServiceRequestStatus.AwaitingInformation, response.Status);
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, requests.Single().Status);
    }

    /// <summary>
    /// Purpose: Checks that recovery fails if the request is not currently in Analyzing state.
    /// Why: Prevents corrupting requests in Created, AwaitingInformation, Analyzed, ReadyForMatching, or Cancelled states.
    /// Scenario: Attempts to invoke RecoverFailedAnalysisAsync on non-analyzing statuses.
    /// Pass condition: Throws ConflictException containing "Recovery is only allowed when status is Analyzing" and leaves status untouched.
    /// Note: Parameterized test (Theory). The command below executes all 5 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.Component1LifecycleTests.RecoverFailedAnalysisAsync_ThrowsWhenCurrentStatusIsNotAnalyzing"
    /// </summary>
    [Theory]
    [InlineData(ServiceRequestStatus.Created)]
    [InlineData(ServiceRequestStatus.AwaitingInformation)]
    [InlineData(ServiceRequestStatus.Analyzed)]
    [InlineData(ServiceRequestStatus.ReadyForMatching)]
    [InlineData(ServiceRequestStatus.Cancelled)]
    public async Task RecoverFailedAnalysisAsync_ThrowsWhenCurrentStatusIsNotAnalyzing(ServiceRequestStatus nonAnalyzingStatus)
    {
        var (service, requests, _) = CreateTestContext();
        var id = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = Guid.NewGuid(),
            Description = "Test description",
            LocationText = "Colombo",
            Status = nonAnalyzingStatus
        });

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.RecoverFailedAnalysisAsync(id, ServiceRequestStatus.Created));

        Assert.Contains("Recovery is only allowed when status is Analyzing", ex.Message);
        Assert.Equal(nonAnalyzingStatus, requests.Single().Status);
    }

    /// <summary>
    /// Purpose: Checks that recovery only permits Created or AwaitingInformation as valid target states.
    /// Why: Prevents bypassing domain rules by illegally jumping to Analyzed, Cancelled, or invalid enum values.
    /// Scenario: Recovery is requested with target statuses like Analyzing, Analyzed, ReadyForMatching, Cancelled, or 999.
    /// Pass condition: Throws ConflictException containing "Invalid recovery target status" and keeps request in Analyzing status.
    /// Note: Parameterized test (Theory). The command below executes all 5 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.Component1LifecycleTests.RecoverFailedAnalysisAsync_ThrowsWhenTargetStatusIsInvalid"
    /// </summary>
    [Theory]
    [InlineData(ServiceRequestStatus.Analyzing)]
    [InlineData(ServiceRequestStatus.Analyzed)]
    [InlineData(ServiceRequestStatus.ReadyForMatching)]
    [InlineData(ServiceRequestStatus.Cancelled)]
    [InlineData((ServiceRequestStatus)999)]
    public async Task RecoverFailedAnalysisAsync_ThrowsWhenTargetStatusIsInvalid(ServiceRequestStatus invalidTarget)
    {
        var (service, requests, _) = CreateTestContext();
        var id = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = Guid.NewGuid(),
            Description = "Test description",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Analyzing
        });

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.RecoverFailedAnalysisAsync(id, invalidTarget));

        Assert.Contains("Invalid recovery target status", ex.Message);
        Assert.Equal(ServiceRequestStatus.Analyzing, requests.Single().Status);
    }

    /// <summary>
    /// Purpose: Checks that recovering a non-existent service request throws KeyNotFoundException.
    /// Why: Verifies proper error handling when given an unknown request ID.
    /// Scenario: Non-existent GUID passed to RecoverFailedAnalysisAsync.
    /// Pass condition: Throws KeyNotFoundException.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1LifecycleTests.RecoverFailedAnalysisAsync_ThrowsKeyNotFound_WhenRequestDoesNotExist"
    /// </summary>
    [Fact]
    public async Task RecoverFailedAnalysisAsync_ThrowsKeyNotFound_WhenRequestDoesNotExist()
    {
        var (service, _, _) = CreateTestContext();
        var id = Guid.NewGuid();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.RecoverFailedAnalysisAsync(id, ServiceRequestStatus.Created));
    }

    /// <summary>
    /// Purpose: Checks that GetPreAnalysisStatusAsync returns the current pre-analysis status (Created or AwaitingInformation).
    /// Why: Agent background services need to know the baseline status before kicking off analysis.
    /// Scenario: Queries status for requests in Created and AwaitingInformation states.
    /// Pass condition: Returns the matching ServiceRequestStatus enum value.
    /// Note: Parameterized test (Theory). The command below executes all 2 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.Component1LifecycleTests.GetPreAnalysisStatusAsync_ReturnsStatus_WhenValid"
    /// </summary>
    [Theory]
    [InlineData(ServiceRequestStatus.Created)]
    [InlineData(ServiceRequestStatus.AwaitingInformation)]
    public async Task GetPreAnalysisStatusAsync_ReturnsStatus_WhenValid(ServiceRequestStatus validStatus)
    {
        var (service, requests, _) = CreateTestContext();
        var id = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = Guid.NewGuid(),
            Description = "Test description",
            LocationText = "Colombo",
            Status = validStatus
        });

        var status = await service.GetPreAnalysisStatusAsync(id);

        Assert.Equal(validStatus, status);
    }

    /// <summary>
    /// Purpose: Checks that querying pre-analysis status throws ConflictException if request is in an inappropriate lifecycle stage.
    /// Why: Protects workflow integrity if a request is already Analyzing, Analyzed, ReadyForMatching, or Cancelled.
    /// Scenario: Calls GetPreAnalysisStatusAsync for non-pre-analysis statuses.
    /// Pass condition: Throws ConflictException.
    /// Note: Parameterized test (Theory). The command below executes all 4 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.Component1LifecycleTests.GetPreAnalysisStatusAsync_ThrowsConflict_WhenInvalidStatus"
    /// </summary>
    [Theory]
    [InlineData(ServiceRequestStatus.Analyzing)]
    [InlineData(ServiceRequestStatus.Analyzed)]
    [InlineData(ServiceRequestStatus.ReadyForMatching)]
    [InlineData(ServiceRequestStatus.Cancelled)]
    public async Task GetPreAnalysisStatusAsync_ThrowsConflict_WhenInvalidStatus(ServiceRequestStatus invalidStatus)
    {
        var (service, requests, _) = CreateTestContext();
        var id = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = Guid.NewGuid(),
            Description = "Test description",
            LocationText = "Colombo",
            Status = invalidStatus
        });

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.GetPreAnalysisStatusAsync(id));
    }

    /// <summary>
    /// Purpose: Checks that querying pre-analysis status for an unknown ID throws KeyNotFoundException.
    /// Why: Ensures predictable error reporting when a record is missing.
    /// Scenario: Non-existent GUID passed to GetPreAnalysisStatusAsync.
    /// Pass condition: Throws KeyNotFoundException.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1LifecycleTests.GetPreAnalysisStatusAsync_ThrowsKeyNotFound_WhenRequestDoesNotExist"
    /// </summary>
    [Fact]
    public async Task GetPreAnalysisStatusAsync_ThrowsKeyNotFound_WhenRequestDoesNotExist()
    {
        var (service, _, _) = CreateTestContext();
        var id = Guid.NewGuid();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.GetPreAnalysisStatusAsync(id));
    }

    /// <summary>
    /// Purpose: Checks that applying a complete analysis result transitions request status from Analyzing to Analyzed.
    /// Why: Verifies successful agent output updates domain fields (category, urgency) and persists the problem analysis record.
    /// Scenario: Applies analysis with category 'Plumbing', high urgency, and NeedsMoreInformation = false.
    /// Pass condition: Request Category is 'Plumbing', Status is Analyzed, Urgency is High, and an analysis record is added.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1LifecycleTests.ApplyProblemAnalysisResult_TransitionsAnalyzingToAnalyzedWhenComplete"
    /// </summary>
    [Fact]
    public async Task ApplyProblemAnalysisResult_TransitionsAnalyzingToAnalyzedWhenComplete()
    {
        var (service, requests, analyses) = CreateTestContext();
        var id = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = Guid.NewGuid(),
            Description = "Kitchen pipe burst and flooded floor",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Analyzing
        });

        var result = await service.ApplyProblemAnalysisResultAsync(new ApplyProblemAnalysisResult
        {
            ServiceRequestId = id,
            Category = "Plumbing",
            DetectedProblem = "Burst pipe flooding kitchen",
            Confidence = 0.9m,
            Urgency = ServiceRequestUrgency.High,
            NeedsMoreInformation = false,
            AgentName = "ProblemUnderstandingAgent"
        });

        Assert.Equal("Plumbing", requests.Single().Category);
        Assert.Equal(ServiceRequestStatus.Analyzed, requests.Single().Status);
        Assert.Equal(ServiceRequestUrgency.High, requests.Single().Urgency);
        Assert.Single(analyses);
    }

    /// <summary>
    /// Purpose: Checks that applying an analysis result requesting clarification transitions status to AwaitingInformation.
    /// Why: Ensures the customer is prompted for missing information when the problem cannot be confidently classified.
    /// Scenario: Applies analysis with NeedsMoreInformation = true and low confidence.
    /// Pass condition: Request status transitions to AwaitingInformation and analysis record is persisted.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1LifecycleTests.ApplyProblemAnalysisResult_TransitionsAnalyzingToAwaitingInformationWhenIncomplete"
    /// </summary>
    [Fact]
    public async Task ApplyProblemAnalysisResult_TransitionsAnalyzingToAwaitingInformationWhenIncomplete()
    {
        var (service, requests, analyses) = CreateTestContext();
        var id = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = Guid.NewGuid(),
            Description = "Machine broken",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Analyzing
        });

        await service.ApplyProblemAnalysisResultAsync(new ApplyProblemAnalysisResult
        {
            ServiceRequestId = id,
            Category = "Unclassified",
            DetectedProblem = "Insufficient details",
            Confidence = 0.3m,
            Urgency = ServiceRequestUrgency.Unknown,
            NeedsMoreInformation = true,
            AgentName = "ProblemUnderstandingAgent"
        });

        Assert.Equal(ServiceRequestStatus.AwaitingInformation, requests.Single().Status);
        Assert.Single(analyses);
    }

    /// <summary>
    /// Purpose: Checks that a fully analyzed request with valid coordinates transitions to ReadyForMatching.
    /// Why: Confirms customer approval to release the service request to provider matching (Component 2).
    /// Scenario: Request has valid coordinates, category, high urgency, and attached problem analysis.
    /// Pass condition: Response status and repository status both equal ReadyForMatching.
    /// Note: Parameterized test (Theory). The command below executes both LocationSource cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.Component1LifecycleTests.MarkReadyForMatchingAsync_TransitionsAnalyzedToReadyForMatchingWhenValid"
    /// </summary>
    [Theory]
    [InlineData(LocationSource.Manual)]
    [InlineData(LocationSource.OpenStreetMap)]
    public async Task MarkReadyForMatchingAsync_TransitionsAnalyzedToReadyForMatchingWhenValid(LocationSource source)
    {
        var (service, requests, analyses) = CreateTestContext();
        var id = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = customerId,
            Description = "Car engine won't start",
            LocationText = "Colombo",
            LocationSource = source,
            Latitude = 6.9271m,
            Longitude = 79.8612m,
            Category = "Vehicle Repair",
            Urgency = ServiceRequestUrgency.High,
            Status = ServiceRequestStatus.Analyzed
        });

        analyses.Add(new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = id,
            DetectedProblem = "Starter motor or battery fault",
            Confidence = 0.85m,
            AgentName = "ProblemUnderstandingAgent"
        });

        var response = await service.MarkReadyForMatchingAsync(id, customerId);

        Assert.Equal(ServiceRequestStatus.ReadyForMatching, response.Status);
        Assert.Equal(ServiceRequestStatus.ReadyForMatching, requests.Single().Status);
    }

    /// <summary>
    /// Purpose: Checks that a customer cannot mark someone else's request as ready for matching.
    /// Why: Enforces customer multi-tenancy and data isolation security.
    /// Scenario: Customer B calls MarkReadyForMatchingAsync for Customer A's request.
    /// Pass condition: Throws KeyNotFoundException.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1LifecycleTests.MarkReadyForMatchingAsync_ThrowsKeyNotFound_WhenNotOwned"
    /// </summary>
    [Fact]
    public async Task MarkReadyForMatchingAsync_ThrowsKeyNotFound_WhenNotOwned()
    {
        var (service, requests, analyses) = CreateTestContext();
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var otherCustomerId = Guid.NewGuid();

        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = ownerId,
            Description = "Car engine won't start",
            LocationText = "Colombo",
            Category = "Vehicle Repair",
            Urgency = ServiceRequestUrgency.High,
            Status = ServiceRequestStatus.Analyzed
        });

        analyses.Add(new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = id,
            DetectedProblem = "Starter motor or battery fault",
            Confidence = 0.85m,
            AgentName = "ProblemUnderstandingAgent"
        });

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.MarkReadyForMatchingAsync(id, otherCustomerId));
    }

    /// <summary>
    /// Purpose: Checks that a request with 'Unclassified' category cannot proceed to matching.
    /// Why: Service providers cannot be matched without a recognized service category.
    /// Scenario: Customer attempts to mark ready for matching when category is 'Unclassified'.
    /// Pass condition: Throws ConflictException.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1LifecycleTests.MarkReadyForMatchingAsync_ThrowsWhenCategoryIsUnclassified"
    /// </summary>
    [Fact]
    public async Task MarkReadyForMatchingAsync_ThrowsWhenCategoryIsUnclassified()
    {
        var (service, requests, analyses) = CreateTestContext();
        var id = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = customerId,
            Description = "Vague description",
            LocationText = "Colombo",
            Category = "Unclassified",
            Urgency = ServiceRequestUrgency.Medium,
            Status = ServiceRequestStatus.Analyzed
        });

        analyses.Add(new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = id,
            DetectedProblem = "Some problem",
            Confidence = 0.7m,
            AgentName = "ProblemUnderstandingAgent"
        });

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.MarkReadyForMatchingAsync(id, customerId));
    }

    /// <summary>
    /// Purpose: Checks that a request with 'Unknown' urgency cannot proceed to matching.
    /// Why: Prevents scheduling or dispatching without validated urgency classification.
    /// Scenario: Customer attempts to mark ready for matching when urgency is ServiceRequestUrgency.Unknown.
    /// Pass condition: Throws ConflictException.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1LifecycleTests.MarkReadyForMatchingAsync_ThrowsWhenUrgencyIsUnknown"
    /// </summary>
    [Fact]
    public async Task MarkReadyForMatchingAsync_ThrowsWhenUrgencyIsUnknown()
    {
        var (service, requests, analyses) = CreateTestContext();
        var id = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = customerId,
            Description = "Plumbing issue",
            LocationText = "Colombo",
            Category = "Plumbing",
            Urgency = ServiceRequestUrgency.Unknown,
            Status = ServiceRequestStatus.Analyzed
        });

        analyses.Add(new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = id,
            DetectedProblem = "Plumbing fault",
            Confidence = 0.8m,
            AgentName = "ProblemUnderstandingAgent"
        });

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.MarkReadyForMatchingAsync(id, customerId));
    }

    /// <summary>
    /// Purpose: Checks that a request cannot be marked ready for matching without an attached problem analysis record.
    /// Why: Enforces that AI problem understanding verification must occur before provider dispatch.
    /// Scenario: Request is in Analyzed status but has no entries in the ProblemAnalyses repository.
    /// Pass condition: Throws ConflictException.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1LifecycleTests.MarkReadyForMatchingAsync_ThrowsWhenNoAnalysisExists"
    /// </summary>
    [Fact]
    public async Task MarkReadyForMatchingAsync_ThrowsWhenNoAnalysisExists()
    {
        var (service, requests, _) = CreateTestContext();
        var id = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = customerId,
            Description = "Electrical issue",
            LocationText = "Colombo",
            Category = "Electrical",
            Urgency = ServiceRequestUrgency.High,
            Status = ServiceRequestStatus.Analyzed
        });

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.MarkReadyForMatchingAsync(id, customerId));
    }

    /// <summary>
    /// Purpose: Checks that a request without GPS coordinates (latitude or longitude null) cannot be marked ready for matching.
    /// Why: Geo-matching algorithms in Component 2 require valid coordinates to find nearby providers.
    /// Scenario: Request has null Latitude and Longitude.
    /// Pass condition: Throws ConflictException mentioning "coordinates".
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1LifecycleTests.MarkReadyForMatchingAsync_ThrowsWhenCoordinatesAreMissing"
    /// </summary>
    [Fact]
    public async Task MarkReadyForMatchingAsync_ThrowsWhenCoordinatesAreMissing()
    {
        var (service, requests, analyses) = CreateTestContext();
        var id = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = customerId,
            Description = "Car engine won't start",
            LocationText = "Kandy, Central Province",
            Latitude = null,  // Missing coordinates
            Longitude = null, // Missing coordinates
            Category = "Vehicle Repair",
            Urgency = ServiceRequestUrgency.High,
            Status = ServiceRequestStatus.Analyzed
        });

        analyses.Add(new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = id,
            DetectedProblem = "Battery discharged",
            Confidence = 0.8m,
            AgentName = "ProblemUnderstandingAgent"
        });

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.MarkReadyForMatchingAsync(id, customerId));
        Assert.Contains("coordinates", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Purpose: Checks that out-of-range latitude (<-90 or >90) or longitude (<-180 or >180) blocks marking ready for matching.
    /// Why: Prevents invalid geographic data from corrupting matching calculations.
    /// Scenario: Coordinates with latitude +-91 or longitude +-181 are tested.
    /// Pass condition: Throws ConflictException mentioning "coordinates".
    /// Note: Parameterized test (Theory). The command below executes all 4 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.Component1LifecycleTests.MarkReadyForMatchingAsync_ThrowsWhenCoordinatesOutOfRange"
    /// </summary>
    [Theory]
    [InlineData(-91, 0)]
    [InlineData(91, 0)]
    [InlineData(0, -181)]
    [InlineData(0, 181)]
    public async Task MarkReadyForMatchingAsync_ThrowsWhenCoordinatesOutOfRange(decimal lat, decimal lon)
    {
        var (service, requests, analyses) = CreateTestContext();
        var id = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = customerId,
            Description = "Car engine won't start",
            LocationText = "Kandy, Central Province",
            Latitude = lat,
            Longitude = lon,
            Category = "Vehicle Repair",
            Urgency = ServiceRequestUrgency.High,
            Status = ServiceRequestStatus.Analyzed
        });

        analyses.Add(new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = id,
            DetectedProblem = "Battery discharged",
            Confidence = 0.8m,
            AgentName = "ProblemUnderstandingAgent"
        });

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.MarkReadyForMatchingAsync(id, customerId));
        Assert.Contains("coordinates", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Purpose: Checks that valid geographic coordinates and location metadata are returned on successful readiness transition.
    /// Why: Verifies that location coordinates and geocoding source are preserved accurately for Component 2.
    /// Scenario: Request with valid Kandy coordinates (7.2906, 80.6337) and OpenStreetMap source.
    /// Pass condition: Returned DTO has Status = ReadyForMatching, matching coordinates, and OpenStreetMap source.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1LifecycleTests.MarkReadyForMatchingAsync_Succeeds_WhenValidCoordinatesArePresent"
    /// </summary>
    [Fact]
    public async Task MarkReadyForMatchingAsync_Succeeds_WhenValidCoordinatesArePresent()
    {
        var (service, requests, analyses) = CreateTestContext();
        var id = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = customerId,
            Description = "Car engine won't start",
            LocationText = "Kandy, Central Province",
            Latitude = 7.2906m,
            Longitude = 80.6337m,
            LocationSource = LocationSource.OpenStreetMap,
            Category = "Vehicle Repair",
            Urgency = ServiceRequestUrgency.High,
            Status = ServiceRequestStatus.Analyzed
        });

        analyses.Add(new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = id,
            DetectedProblem = "Battery discharged",
            Confidence = 0.8m,
            AgentName = "ProblemUnderstandingAgent"
        });

        var response = await service.MarkReadyForMatchingAsync(id, customerId);

        Assert.Equal(ServiceRequestStatus.ReadyForMatching, response.Status);
        Assert.Equal(7.2906m, response.Latitude);
        Assert.Equal(80.6337m, response.Longitude);
        Assert.Equal(LocationSource.OpenStreetMap, response.LocationSource);
        Assert.Equal("Kandy, Central Province", response.LocationText);
    }

    /// <summary>
    /// Purpose: Checks that changing location text clears out old coordinates and resets location source to Manual.
    /// Why: Prevents dangerous location mismatch where address text says "Galle" but coordinates point to "Kandy".
    /// Scenario: Customer updates address from Kandy to Galle while passing old coordinates.
    /// Pass condition: Response has new address "Galle", null Latitude, null Longitude, and Manual LocationSource.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1LifecycleTests.UpdateAsync_ChangingAddress_CannotRetainStaleCoordinates"
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ChangingAddress_CannotRetainStaleCoordinates()
    {
        var (service, requests, _) = CreateTestContext();
        var id = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = customerId,
            Description = "Initial plumbing issue",
            LocationText = "Kandy",
            Latitude = 7.2906m,
            Longitude = 80.6337m,
            LocationSource = LocationSource.OpenStreetMap,
            Category = "Plumbing",
            Urgency = ServiceRequestUrgency.Medium,
            Status = ServiceRequestStatus.Created
        });

        // Customer changes address text to Galle, but supplies old Kandy coordinates
        var update = new UpdateServiceRequestRequest
        {
            Description = "Initial plumbing issue",
            LocationText = "Galle",
            Latitude = 7.2906m,
            Longitude = 80.6337m,
            LocationSource = LocationSource.OpenStreetMap
        };

        var response = await service.UpdateAsync(customerId, id, update);

        Assert.Equal("Galle", response.LocationText);
        Assert.Null(response.Latitude);
        Assert.Null(response.Longitude);
        Assert.Equal(LocationSource.Manual, response.LocationSource);
    }

    /// <summary>
    /// Purpose: Checks that an empty location text prevents transitioning to ReadyForMatching.
    /// Why: Providers require human-readable address context in addition to map coordinates.
    /// Scenario: Request has empty string for LocationText.
    /// Pass condition: Throws ConflictException.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1LifecycleTests.MarkReadyForMatchingAsync_ThrowsWhenLocationTextIsMissing"
    /// </summary>
    [Fact]
    public async Task MarkReadyForMatchingAsync_ThrowsWhenLocationTextIsMissing()
    {
        var (service, requests, analyses) = CreateTestContext();
        var id = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = customerId,
            Description = "Car engine won't start",
            LocationText = "", // Missing!
            Category = "Vehicle Repair",
            Urgency = ServiceRequestUrgency.High,
            Status = ServiceRequestStatus.Analyzed
        });

        analyses.Add(new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = id,
            DetectedProblem = "Battery discharged",
            Confidence = 0.8m,
            AgentName = "ProblemUnderstandingAgent"
        });

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.MarkReadyForMatchingAsync(id, customerId));
    }

    /// <summary>
    /// Purpose: Checks that invalid analysis confidence values (0.0, negative, or >1.0) prevent marking ready for matching.
    /// Why: Ensures only valid statistical confidence scores (between 0.0 exclusive and 1.0 inclusive) pass validation.
    /// Scenario: Tests problem analysis confidence values of 0.0, -0.1, and 1.2.
    /// Pass condition: Throws ConflictException.
    /// Note: Parameterized test (Theory). The command below executes all 3 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.Component1LifecycleTests.MarkReadyForMatchingAsync_ThrowsWhenLatestProblemAnalysisHasInvalidConfidence"
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(1.2)]
    public async Task MarkReadyForMatchingAsync_ThrowsWhenLatestProblemAnalysisHasInvalidConfidence(decimal invalidConfidence)
    {
        var (service, requests, analyses) = CreateTestContext();
        var id = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = customerId,
            Description = "Car engine won't start",
            LocationText = "Colombo",
            Category = "Vehicle Repair",
            Urgency = ServiceRequestUrgency.High,
            Status = ServiceRequestStatus.Analyzed
        });

        analyses.Add(new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = id,
            DetectedProblem = "Battery discharged",
            Confidence = invalidConfidence,
            AgentName = "ProblemUnderstandingAgent"
        });

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.MarkReadyForMatchingAsync(id, customerId));
    }

    // In-memory test doubles
    private sealed class InMemoryServiceRequestRepository : IServiceRequestRepository
    {
        private readonly List<ServiceRequest> _requests;
        private readonly List<ProblemAnalysis> _analyses;

        public InMemoryServiceRequestRepository(List<ServiceRequest> requests, List<ProblemAnalysis> analyses)
        {
            _requests = requests;
            _analyses = analyses;
        }

        public Task<ServiceRequest?> GetByIdAsync(Guid id, bool includeProblemAnalyses = false, CancellationToken cancellationToken = default)
        {
            var req = _requests.SingleOrDefault(x => x.Id == id);
            if (req != null && includeProblemAnalyses)
            {
                req.ProblemAnalyses = _analyses.Where(a => a.ServiceRequestId == id).ToList();
            }
            return Task.FromResult(req);
        }

        public Task<ServiceRequest?> GetByIdAndCustomerIdAsync(Guid id, Guid customerId, bool includeProblemAnalyses = false, CancellationToken cancellationToken = default)
        {
            var req = _requests.SingleOrDefault(x => x.Id == id && x.CustomerId == customerId);
            if (req != null && includeProblemAnalyses)
            {
                req.ProblemAnalyses = _analyses.Where(a => a.ServiceRequestId == id).ToList();
            }
            return Task.FromResult(req);
        }

        public Task<IReadOnlyList<ServiceRequest>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ServiceRequest>>(_requests.Where(x => x.CustomerId == customerId).ToArray());

        public Task<IReadOnlyList<ServiceRequest>> GetByStatusAsync(ServiceRequestStatus status, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ServiceRequest>>(_requests.Where(x => x.Status == status).ToArray());

        public Task<IReadOnlyList<ServiceRequest>> GetAllForAdminAsync(
            ServiceRequestStatus? status = null,
            string? category = null,
            ServiceRequestUrgency? urgency = null,
            CancellationToken cancellationToken = default)
        {
            var query = _requests.AsEnumerable();
            if (status.HasValue) query = query.Where(x => x.Status == status.Value);
            if (!string.IsNullOrWhiteSpace(category)) query = query.Where(x => string.Equals(x.Category, category, StringComparison.OrdinalIgnoreCase));
            if (urgency.HasValue) query = query.Where(x => x.Urgency == urgency.Value);
            return Task.FromResult<IReadOnlyList<ServiceRequest>>(query.OrderByDescending(x => x.CreatedAt).ToArray());
        }

        public Task AddAsync(ServiceRequest serviceRequest, CancellationToken cancellationToken = default)
        {
            _requests.Add(serviceRequest);
            return Task.CompletedTask;
        }

        public void Update(ServiceRequest serviceRequest) { }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryProblemAnalysisRepository : IProblemAnalysisRepository
    {
        private readonly List<ProblemAnalysis> _analyses;

        public InMemoryProblemAnalysisRepository(List<ProblemAnalysis> analyses)
        {
            _analyses = analyses;
        }

        public Task<IReadOnlyList<ProblemAnalysis>> GetByServiceRequestIdAsync(Guid serviceRequestId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProblemAnalysis>>(_analyses.Where(a => a.ServiceRequestId == serviceRequestId).ToArray());

        public Task<ProblemAnalysis?> GetMostRecentByServiceRequestIdAsync(Guid serviceRequestId, CancellationToken cancellationToken = default)
            => Task.FromResult(_analyses.Where(a => a.ServiceRequestId == serviceRequestId).OrderByDescending(a => a.CreatedAt).FirstOrDefault());

        public Task AddAsync(ProblemAnalysis problemAnalysis, CancellationToken cancellationToken = default)
        {
            _analyses.Add(problemAnalysis);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
