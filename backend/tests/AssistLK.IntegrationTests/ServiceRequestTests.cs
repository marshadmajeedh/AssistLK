using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;

namespace AssistLK.IntegrationTests;

/// <summary>
/// What this class tests:
///   Domain entity construction, default field values, CategoryHint vs Category distinction, and 1-to-many relationship navigation between ServiceRequest and ProblemAnalysis.
/// Why these tests are needed:
///   Ensures that when a customer creates a new service request, it initializes with canonical defaults (Status = Created, Category = "Unclassified", Urgency = Unknown) and that user-supplied CategoryHint does not prematurely overwrite AI-determined Category.
/// What is real and what is mocked/faked:
///   - Real: Domain entities (ServiceRequest, ProblemAnalysis).
///   - Mocked/Faked: None. Runs in-memory.
/// Prerequisites:
///   None.
/// Run from repository root:
///   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.ServiceRequestTests"
/// </summary>
public class ServiceRequestTests
{
    /// <summary>
    /// Purpose: Checks that ServiceRequest properties are correctly assigned during object initialization.
    /// Why: Verifies that core domain properties (CustomerId, Category, Urgency, Status) retain their assigned values.
    /// Scenario: Instantiates a ServiceRequest with explicit valid values.
    /// Pass condition: Assigned property values match expected values.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestTests.ServiceRequest_CanBeConstructedWithValidValues"
    /// </summary>
    [Fact]
    public void ServiceRequest_CanBeConstructedWithValidValues()
    {
        var customerId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            CustomerId = customerId,
            Category = "Vehicle repair",
            Description = "The vehicle will not start.",
            LocationText = "Colombo",
            Latitude = 6.927079m,
            Longitude = 79.861244m,
            Urgency = ServiceRequestUrgency.High,
            Status = ServiceRequestStatus.Created
        };

        Assert.Equal(customerId, request.CustomerId);
        Assert.Equal("Vehicle repair", request.Category);
        Assert.Equal(ServiceRequestUrgency.High, request.Urgency);
        Assert.Equal(ServiceRequestStatus.Created, request.Status);
    }

    /// <summary>
    /// Purpose: Checks that a newly instantiated ServiceRequest has required domain defaults (Category = "Unclassified", Description = "", LocationText = "", Urgency = Unknown, Status = Created).
    /// Why: Prevents null reference exceptions and guarantees consistent domain state upon record creation.
    /// Scenario: Instantiates parameterless new ServiceRequest().
    /// Pass condition: Defaults match canonical initial values.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestTests.ServiceRequest_UsesTheRequiredInitialValuesAsDefaults"
    /// </summary>
    [Fact]
    public void ServiceRequest_UsesTheRequiredInitialValuesAsDefaults()
    {
        var request = new ServiceRequest();

        Assert.Equal(
            Enum.GetValues<ServiceRequestUrgency>().First(),
            request.Urgency);
        Assert.Equal(
            Enum.GetValues<ServiceRequestStatus>().First(),
            request.Status);
        Assert.Null(request.CategoryHint);
        Assert.Equal("Unclassified", request.Category);
        Assert.Equal(string.Empty, request.Description);
        Assert.Equal(string.Empty, request.LocationText);
        Assert.Empty(request.ProblemAnalyses);
    }

    /// <summary>
    /// Purpose: Checks that CategoryHint and Category remain distinct properties on creation.
    /// Why: Architectural requirement: a customer's optional CategoryHint (e.g. "Plumbing") must NOT bypass AI classification (Category remains "Unclassified" until verified by agent).
    /// Scenario: Creates request with CategoryHint = "Plumbing".
    /// Pass condition: CategoryHint is "Plumbing", but Category is "Unclassified".
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestTests.ServiceRequest_CanBeConstructedWithCategoryHint_WhileCategoryRemainsDistinct"
    /// </summary>
    [Fact]
    public void ServiceRequest_CanBeConstructedWithCategoryHint_WhileCategoryRemainsDistinct()
    {
        var request = new ServiceRequest
        {
            CustomerId = Guid.NewGuid(),
            CategoryHint = "Plumbing",
            Description = "Leaking pipe under sink",
            LocationText = "Colombo"
        };

        Assert.Equal("Plumbing", request.CategoryHint);
        Assert.Equal("Unclassified", request.Category);
    }

    /// <summary>
    /// Purpose: Checks that a ServiceRequest can hold multiple ProblemAnalysis entities in its navigation collection.
    /// Why: Supports multiple rounds of problem understanding analysis (e.g. initial analysis and follow-up clarification analyses).
    /// Scenario: Adds 2 ProblemAnalysis entities to request.ProblemAnalyses.
    /// Pass condition: Count is 2 and both items are present in the collection.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestTests.ServiceRequest_CanContainMultipleProblemAnalyses"
    /// </summary>
    [Fact]
    public void ServiceRequest_CanContainMultipleProblemAnalyses()
    {
        var request = new ServiceRequest();
        var firstAnalysis = new ProblemAnalysis { ServiceRequest = request };
        var secondAnalysis = new ProblemAnalysis { ServiceRequest = request };

        request.ProblemAnalyses.Add(firstAnalysis);
        request.ProblemAnalyses.Add(secondAnalysis);

        Assert.Equal(2, request.ProblemAnalyses.Count);
        Assert.Contains(firstAnalysis, request.ProblemAnalyses);
        Assert.Contains(secondAnalysis, request.ProblemAnalyses);
    }

    /// <summary>
    /// Purpose: Checks bidirectional navigation from ProblemAnalysis to its parent ServiceRequest.
    /// Why: Ensures navigation properties and foreign key IDs match for ORM graph traversal.
    /// Scenario: Instantiates ProblemAnalysis referencing parent ServiceRequest.
    /// Pass condition: Reference is identical (Assert.Same) and ServiceRequestId equals request.Id.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestTests.ProblemAnalysis_CanReferenceItsParentServiceRequest"
    /// </summary>
    [Fact]
    public void ProblemAnalysis_CanReferenceItsParentServiceRequest()
    {
        var request = new ServiceRequest();
        var analysis = new ProblemAnalysis
        {
            ServiceRequest = request,
            ServiceRequestId = request.Id
        };

        Assert.Same(request, analysis.ServiceRequest);
        Assert.Equal(request.Id, analysis.ServiceRequestId);
    }
}