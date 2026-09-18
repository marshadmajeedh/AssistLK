using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssistLK.Api.Tests;

[Collection("EnvironmentTests")]
public class LocationSourceTests
{
    [Theory]
    [InlineData(null, "Manual")]
    [InlineData("Manual", "Manual")]
    [InlineData("OpenStreetMap", "OpenStreetMap")]
    public async Task Create_PersistsSource_AndIndependentDeviceCoordinates(string? source, string expected)
    {
        using var factory = new AssistLKApiTestFactory();
        using var client = factory.CreateAuthenticatedClient(Guid.NewGuid(), UserRole.Customer);
        var payload = new Dictionary<string, object> { ["description"] = "Leaking kitchen sink", ["locationText"] = "Example Road",
            ["latitude"] = 6.905m, ["longitude"] = 79.9195m };
        if (source != null) payload["locationSource"] = source;
        using var response = await client.PostAsJsonAsync("/api/service-requests", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expected, body.GetProperty("locationSource").GetString());
        using var scope = factory.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<AssistLKDbContext>().ServiceRequests.SingleAsync();
        Assert.Equal(expected, saved.LocationSource.ToString());
        Assert.Equal(6.905m, saved.Latitude);
        Assert.Equal(79.9195m, saved.Longitude);
    }

    [Theory]
    [InlineData("OpenStreetMap", "OpenStreetMap")]
    [InlineData("Manual", "Manual")]
    [InlineData(null, "Manual")]
    public async Task Update_PreservesExplicitSource_OrSwitchesToManual(string? source, string expected)
    {
        using var factory = new AssistLKApiTestFactory();
        using var client = factory.CreateAuthenticatedClient(Guid.NewGuid(), UserRole.Customer);
        using var created = await client.PostAsJsonAsync("/api/service-requests", new {
            description = "Leaking kitchen sink", locationText = "Example Road", locationSource = "OpenStreetMap", latitude = 6m, longitude = 79m });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("serviceRequestId").GetGuid();
        var payload = new Dictionary<string, object?> { ["description"] = "Leaking kitchen sink", ["locationText"] = "Another Road",
            ["latitude"] = null, ["longitude"] = null };
        if (source != null) payload["locationSource"] = source;
        using var response = await client.PutAsJsonAsync($"/api/service-requests/{id}", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<AssistLKDbContext>().ServiceRequests.SingleAsync();
        Assert.Equal(expected, saved.LocationSource.ToString());
        Assert.Null(saved.Latitude);
        Assert.Null(saved.Longitude);
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/service-requests/{id}");
        Assert.Equal(expected, detail.GetProperty("locationSource").GetString());
    }

    [Theory]
    [InlineData("Other")]
    [InlineData(2)]
    public async Task InvalidSource_RejectedForCreateAndUpdate(object source)
    {
        using var factory = new AssistLKApiTestFactory();
        using var client = factory.CreateAuthenticatedClient(Guid.NewGuid(), UserRole.Customer);
        var payload = new { description = "Leaking kitchen sink", locationText = "Example Road", locationSource = source };
        using var create = await client.PostAsJsonAsync("/api/service-requests", payload);
        using var update = await client.PutAsJsonAsync($"/api/service-requests/{Guid.NewGuid()}", payload);
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
    }
}
