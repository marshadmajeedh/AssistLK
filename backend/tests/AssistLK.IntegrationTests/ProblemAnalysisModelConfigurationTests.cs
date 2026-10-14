using AssistLK.Domain.Entities;
using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AssistLK.IntegrationTests;

/// <summary>
/// What this class tests:
///   Entity Framework Core model configuration, table mappings, column nullability, precision/scale, foreign keys, cascade delete rules, and check constraints for ProblemAnalysis.
/// Why these tests are needed:
///   Ensures EF Core metadata matches database schema specifications (table name "ProblemAnalyses", non-nullable FK, precision (5,4) for confidence score, max length 100 for agent name, and CK_ProblemAnalyses_Confidence constraint).
/// What is real and what is mocked/faked:
///   - Real: EF Core model builder metadata (IDesignTimeModel) compiled using Npgsql options.
///   - Mocked/Faked: None. No live database connection is made; schema metadata is inspected in-memory.
/// Prerequisites:
///   None. Runs in-memory without a live database.
/// Run from repository root:
///   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.ProblemAnalysisModelConfigurationTests"
/// </summary>
public class ProblemAnalysisModelConfigurationTests
{
    private static readonly IEntityType EntityType = CreateModel()
        .FindEntityType(typeof(ProblemAnalysis))!;

    /// <summary>
    /// Purpose: Checks table name "ProblemAnalyses", non-nullability of ServiceRequestId and DetectedProblem, Confidence precision (5,4), and AgentName max length (100).
    /// Why: Guarantees database columns match domain requirements and won't accept truncated or invalid precision values.
    /// Scenario: Inspects EntityType properties for ProblemAnalysis.
    /// Pass condition: Table name is "ProblemAnalyses", FK and DetectedProblem are non-nullable, confidence is (5,4), and agent name max length is 100.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ProblemAnalysisModelConfigurationTests.ProblemAnalysis_IsMappedToTheExpectedTableAndProperties"
    /// </summary>
    [Fact]
    public void ProblemAnalysis_IsMappedToTheExpectedTableAndProperties()
    {
        Assert.Equal("ProblemAnalyses", EntityType.GetTableName());

        var serviceRequestId = EntityType.FindProperty(nameof(ProblemAnalysis.ServiceRequestId))!;
        var detectedProblem = EntityType.FindProperty(nameof(ProblemAnalysis.DetectedProblem))!;
        var confidence = EntityType.FindProperty(nameof(ProblemAnalysis.Confidence))!;
        var agentName = EntityType.FindProperty(nameof(ProblemAnalysis.AgentName))!;

        Assert.False(serviceRequestId.IsNullable);
        Assert.False(detectedProblem.IsNullable);
        Assert.Equal(5, confidence.GetPrecision());
        Assert.Equal(4, confidence.GetScale());
        Assert.Equal(100, agentName.GetMaxLength());
    }

    /// <summary>
    /// Purpose: Checks that an index exists on ServiceRequestId and that a cascade-delete foreign key connects to ServiceRequest.Id.
    /// Why: Fast query lookups for a request's analyses, and automatic cleanup of analysis records when a service request is deleted.
    /// Scenario: Inspects entity indexes and foreign keys on EntityType.
    /// Pass condition: ServiceRequestId index is present, foreign key targets ServiceRequest.Id, and delete behavior is Cascade.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ProblemAnalysisModelConfigurationTests.ProblemAnalysis_HasTheExpectedIndexAndServiceRequestRelationship"
    /// </summary>
    [Fact]
    public void ProblemAnalysis_HasTheExpectedIndexAndServiceRequestRelationship()
    {
        Assert.Contains(
            EntityType.GetIndexes(),
            index => index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(ProblemAnalysis.ServiceRequestId) }));

        var foreignKey = EntityType.GetForeignKeys().Single(
            key => key.Properties.Single().Name == nameof(ProblemAnalysis.ServiceRequestId));

        Assert.Equal(typeof(ServiceRequest), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(
            nameof(ServiceRequest.Id),
            foreignKey.PrincipalKey.Properties.Single().Name);
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
    }

    /// <summary>
    /// Purpose: Checks that the database check constraint CK_ProblemAnalyses_Confidence is configured on the entity.
    /// Why: Enforces at the database engine level that confidence values remain within valid bounds (e.g. 0 to 1).
    /// Scenario: Inspects check constraints collection on EntityType.
    /// Pass condition: Constraint names contain "CK_ProblemAnalyses_Confidence".
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ProblemAnalysisModelConfigurationTests.ProblemAnalysis_HasTheExpectedCheckConstraint"
    /// </summary>
    [Fact]
    public void ProblemAnalysis_HasTheExpectedCheckConstraint()
    {
        var constraintNames = EntityType.GetCheckConstraints()
            .Select(constraint => constraint.Name)
            .ToArray();

        Assert.Contains("CK_ProblemAnalyses_Confidence", constraintNames);
    }

    private static IModel CreateModel()
    {
        var options = new DbContextOptionsBuilder<AssistLKDbContext>()
            .UseNpgsql("Host=localhost;Database=AssistLKTests;Username=test;Password=test")
            .Options;

        using var context = new AssistLKDbContext(options);
        return context.GetService<IDesignTimeModel>().Model;
    }
}