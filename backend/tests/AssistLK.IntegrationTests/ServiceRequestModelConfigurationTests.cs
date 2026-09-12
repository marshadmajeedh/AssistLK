using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AssistLK.IntegrationTests;

public class ServiceRequestModelConfigurationTests
{
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

    private static readonly IEntityType EntityType = CreateModel()
        .FindEntityType(typeof(ServiceRequest))!;

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

    [Fact]
    public void ServiceRequest_HasTheExpectedCheckConstraints()
    {
        var constraintNames = EntityType.GetCheckConstraints()
            .Select(constraint => constraint.Name)
            .ToArray();

        Assert.Contains("CK_ServiceRequests_Latitude", constraintNames);
        Assert.Contains("CK_ServiceRequests_Longitude", constraintNames);
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
