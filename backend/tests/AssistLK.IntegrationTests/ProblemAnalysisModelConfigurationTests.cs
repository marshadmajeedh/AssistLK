using AssistLK.Domain.Entities;
using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AssistLK.IntegrationTests;

public class ProblemAnalysisModelConfigurationTests
{
    private static readonly IEntityType EntityType = CreateModel()
        .FindEntityType(typeof(ProblemAnalysis))!;

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