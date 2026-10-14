using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AssistLK.IntegrationTests;

/// <summary>
/// What this class tests:
///   Entity Framework Core database model configuration for ServiceRequest, covering table mapping, property nullability/lengths, coordinate precision (9,6), enum converters, indexes, User foreign key, and database check constraints.
/// Why these tests are needed:
///   Verifies schema integrity at design-time without connecting to a live database, ensuring migrations and database check constraints (CK_ServiceRequests_Latitude, CK_ServiceRequests_Longitude, CK_ServiceRequests_Status_Valid) stay strictly in sync with domain rules.
/// What is real and what is mocked/faked:
///   - Real: EF Core Npgsql model metadata (IDesignTimeModel).
///   - Mocked/Faked: None. No live database connection is opened; schema is inspected in-memory.
/// Prerequisites:
///   None. Runs in-memory.
/// Run from repository root:
///   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.ServiceRequestModelConfigurationTests"
/// </summary>
public class ServiceRequestModelConfigurationTests
{
    private static readonly IEntityType EntityType = CreateModel()
        .FindEntityType(typeof(ServiceRequest))!;

    /// <summary>
    /// Purpose: Checks that LocationSource defaults to Manual, is stored as a string, and confirms no redundant granular address columns exist.
    /// Why: Verifies the lightweight address approach (single location text + coordinates + source) rather than unnecessary multi-column address schema.
    /// Scenario: Inspects LocationSource property configuration and verifies absence of street/neighborhood/city/GooglePlaceId properties.
    /// Pass condition: LocationSource is non-nullable, defaults to Manual, converts to string, and structured address columns are null.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestModelConfigurationTests.LocationSource_DefaultsToManual_WithoutStructuredAddressColumns"
    /// </summary>
    [Fact]
    public void LocationSource_DefaultsToManual_WithoutStructuredAddressColumns()
    {
        var source = EntityType.FindProperty(nameof(ServiceRequest.LocationSource))!;
        Assert.False(source.IsNullable);
        Assert.Equal(LocationSource.Manual, source.GetDefaultValue());
        Assert.Equal(typeof(string), source.GetTypeMapping().Converter!.ProviderClrType);
        foreach (var name in new[] { "LocationStreet", "LocationNeighborhood", "LocationCity", "LocationProvince",
            "LocationPostalCode", "LocationCountry", "GooglePlaceId", "Accuracy" })
            Assert.Null(EntityType.FindProperty(name));
    }

    /// <summary>
    /// Purpose: Checks that ServiceRequest maps to "ServiceRequests" table with precise column types, string lengths, coordinate precision (9,6), and enum string converters.
    /// Why: Ensures data integrity in PostgreSQL for geospatial coordinates and status strings.
    /// Scenario: Inspects EntityType properties for CustomerId, Category, Description, LocationText, Latitude, Longitude, Urgency, Status.
    /// Pass condition: Table name is "ServiceRequests", description is "text", location text max length is 255, coordinates have precision 9 scale 6, and enums map to string.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestModelConfigurationTests.ServiceRequest_IsMappedToTheExpectedTableAndProperties"
    /// </summary>
    [Fact]
    public void ServiceRequest_IsMappedToTheExpectedTableAndProperties()
    {
        Assert.Equal("ServiceRequests", EntityType.GetTableName());

        var customerId = EntityType.FindProperty(nameof(ServiceRequest.CustomerId))!;
        var categoryHint = EntityType.FindProperty(nameof(ServiceRequest.CategoryHint))!;
        var category = EntityType.FindProperty(nameof(ServiceRequest.Category))!;
        var description = EntityType.FindProperty(nameof(ServiceRequest.Description))!;
        var locationText = EntityType.FindProperty(nameof(ServiceRequest.LocationText))!;
        var latitude = EntityType.FindProperty(nameof(ServiceRequest.Latitude))!;
        var longitude = EntityType.FindProperty(nameof(ServiceRequest.Longitude))!;
        var urgency = EntityType.FindProperty(nameof(ServiceRequest.Urgency))!;
        var status = EntityType.FindProperty(nameof(ServiceRequest.Status))!;

        Assert.False(customerId.IsNullable);
        Assert.True(categoryHint.IsNullable);
        Assert.Equal(100, categoryHint.GetMaxLength());
        Assert.False(category.IsNullable);
        Assert.Equal(100, category.GetMaxLength());
        Assert.Equal("text", description.GetColumnType());
        Assert.Equal(255, locationText.GetMaxLength());
        Assert.Equal(9, latitude.GetPrecision());
        Assert.Equal(6, latitude.GetScale());
        Assert.Equal(9, longitude.GetPrecision());
        Assert.Equal(6, longitude.GetScale());
        Assert.Equal(typeof(string), urgency.GetTypeMapping().Converter!.ProviderClrType);
        Assert.Equal(typeof(string), status.GetTypeMapping().Converter!.ProviderClrType);
    }

    /// <summary>
    /// Purpose: Checks that CategoryHint is optional (nullable), max length 100, and does not alter the non-nullable Category column.
    /// Why: Verifies that customer category suggestions are optional hints while verified Category remains a mandatory database column.
    /// Scenario: Inspects CategoryHint and Category property metadata on EntityType.
    /// Pass condition: CategoryHint is nullable with max length 100 and no default; Category is non-nullable with max length 100.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestModelConfigurationTests.ServiceRequest_CategoryHint_IsConfiguredAsOptionalWithExpectedMaxLength"
    /// </summary>
    [Fact]
    public void ServiceRequest_CategoryHint_IsConfiguredAsOptionalWithExpectedMaxLength()
    {
        var categoryHint = EntityType.FindProperty(nameof(ServiceRequest.CategoryHint))!;

        Assert.NotNull(categoryHint);
        Assert.True(categoryHint.IsNullable);
        Assert.Equal(100, categoryHint.GetMaxLength());
        Assert.Null(categoryHint.GetDefaultValue());

        // Category remains distinct and required
        var category = EntityType.FindProperty(nameof(ServiceRequest.Category))!;
        Assert.NotNull(category);
        Assert.False(category.IsNullable);
        Assert.Equal(100, category.GetMaxLength());
    }

    /// <summary>
    /// Purpose: Checks that indexes on CustomerId and Status exist, and that a foreign key links CustomerId to User.Id with DeleteBehavior.Restrict.
    /// Why: Speeds up customer and status queries and prevents accidental cascading deletion of customer records.
    /// Scenario: Inspects EntityType indexes and foreign keys.
    /// Pass condition: Indexes exist for CustomerId and Status; foreign key references User.Id with Restrict delete behavior.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestModelConfigurationTests.ServiceRequest_HasTheExpectedIndexesAndCustomerRelationship"
    /// </summary>
    [Fact]
    public void ServiceRequest_HasTheExpectedIndexesAndCustomerRelationship()
    {
        Assert.Contains(
            EntityType.GetIndexes(),
            index => index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(ServiceRequest.CustomerId) }));
        Assert.Contains(
            EntityType.GetIndexes(),
            index => index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(ServiceRequest.Status) }));

        var foreignKey = EntityType.GetForeignKeys().Single(
            key => key.Properties.Single().Name == nameof(ServiceRequest.CustomerId));

        Assert.Equal(typeof(User), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(nameof(User.Id), foreignKey.PrincipalKey.Properties.Single().Name);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }

    /// <summary>
    /// Purpose: Checks that database check constraints enforce valid Latitude (-90 to 90), Longitude (-180 to 180), and valid Status values.
    /// Why: Protects the database at the engine level from out-of-range coordinates or illegal status transitions.
    /// Scenario: Inspects check constraints collection on EntityType.
    /// Pass condition: Contains Latitude, Longitude, and Status_Valid check constraints with all 6 status strings in the SQL expression.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestModelConfigurationTests.ServiceRequest_HasTheExpectedCheckConstraints"
    /// </summary>
    [Fact]
    public void ServiceRequest_HasTheExpectedCheckConstraints()
    {
        var constraints = EntityType.GetCheckConstraints().ToArray();
        var constraintNames = constraints
            .Select(constraint => constraint.Name)
            .ToArray();

        Assert.Contains("CK_ServiceRequests_Latitude", constraintNames);
        Assert.Contains("CK_ServiceRequests_Longitude", constraintNames);
        Assert.Contains("CK_ServiceRequests_Status_Valid", constraintNames);

        var statusConstraint = constraints.Single(c => c.Name == "CK_ServiceRequests_Status_Valid");
        Assert.Contains("'Created'", statusConstraint.Sql);
        Assert.Contains("'Analyzing'", statusConstraint.Sql);
        Assert.Contains("'AwaitingInformation'", statusConstraint.Sql);
        Assert.Contains("'Analyzed'", statusConstraint.Sql);
        Assert.Contains("'ReadyForMatching'", statusConstraint.Sql);
        Assert.Contains("'Cancelled'", statusConstraint.Sql);
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
