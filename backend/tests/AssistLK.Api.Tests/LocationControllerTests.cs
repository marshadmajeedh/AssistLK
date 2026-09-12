using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AssistLK.Application.Interfaces;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using AssistLK.Infrastructure.ExternalServices;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AssistLK.Api.Tests;

[Collection("EnvironmentTests")]
public class LocationControllerTests
{
    private const string Endpoint = "/api/location/reverse-geocode";
    private const string Secret = "test-key-never-return-this";

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Query { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Query = request.RequestUri!.Query;
            return send(request, cancellationToken);
        }
    }

    private static Handler Reply(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json) }));

    private static string Address(params (string Type, string Name)[] components) => JsonSerializer.Serialize(new
    {
        status = "OK",
        results = new[] { new {
            formatted_address = "  Example Road, Example City  ", place_id = "example-place",
            geometry = new { location_type = "ROOFTOP" },
            address_components = components.Select(c => new { long_name = c.Name, types = new[] { "political", c.Type } })
        } }
    });

    // Run the real controller, JWT authorization, validation, and typed Google service.
    // Any attempt to resolve the database fails; preview cannot read or write it.
    private static async Task<HttpResponseMessage> Send(Handler handler, object payload,
        UserRole? role = UserRole.Customer, string key = Secret)
    {
        using var parent = new AssistLKApiTestFactory();
        using var factory = parent.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(new GoogleMapsOptions { ApiKey = key });
            services.AddScoped<AssistLKDbContext>(_ => throw new InvalidOperationException("Preview accessed the database."));
            services.AddHttpClient<ILocationGeocodingService, GoogleReverseGeocodingService>()
                .ConfigurePrimaryHttpMessageHandler(() => handler);
        }));
        using var client = factory.CreateClient();
        if (role != null)
            client.DefaultRequestHeaders.Authorization = new("Bearer", parent.GenerateJwtToken(Guid.NewGuid(), role.Value));
        return await client.PostAsJsonAsync(Endpoint, payload);
    }

    [Fact]
    public async Task Customer_ZeroCoordinates_ReturnsNormalizedPreview_WithoutDatabaseAccess()
    {
        var handler = Reply(Address(("route", "Example Road"), ("locality", "Example City")));
        using var response = await Send(handler, new { latitude = 0, longitude = 0 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Example Road, Example City", body.GetProperty("formattedAddress").GetString());
        Assert.Equal("StreetAddress", body.GetProperty("resolutionLevel").GetString());
        Assert.Equal("example-place", body.GetProperty("placeId").GetString());
        Assert.Contains("latlng=0,0", handler.Query);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData(UserRole.Admin, 403)]
    [InlineData(UserRole.Provider, 403)]
    public async Task Authorization_BlocksOtherRoles(UserRole? role, int expected)
    {
        var handler = Reply(Address());
        using var response = await Send(handler, new { latitude = 0, longitude = 0 }, role);
        Assert.Equal(expected, (int)response.StatusCode);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData(-91, 0)]
    [InlineData(91, 0)]
    [InlineData(0, -181)]
    [InlineData(0, 181)]
    public async Task InvalidCoordinates_Return400_WithoutCallingGoogle(int latitude, int longitude)
    {
        var handler = Reply(Address());
        using var response = await Send(handler, new { latitude, longitude });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"latitude\":0}")]
    [InlineData("{\"longitude\":0}")]
    [InlineData("{\"latitude\":null,\"longitude\":0}")]
    [InlineData("{\"latitude\":\"NaN\",\"longitude\":0}")]
    public async Task MissingOrNonnumericCoordinates_Return400(string payload)
    {
        var handler = Reply(Address());
        using var response = await Send(handler, JsonSerializer.Deserialize<JsonElement>(payload));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Components_MapByType_RegardlessOfOrder(bool reverse)
    {
        (string, string)[] components = [("route", "Road"), ("neighborhood", "Neighborhood"),
            ("locality", "City"), ("administrative_area_level_1", "Province"), ("postal_code", "123"), ("country", "Country")];
        var handler = Reply(Address(reverse ? components.Reverse().ToArray() : components));
        using var response = await Send(handler, new { latitude = 6.9050m, longitude = 79.9195m });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var pair in new[] { ("street", "Road"), ("neighborhood", "Neighborhood"), ("city", "City"),
            ("province", "Province"), ("postalCode", "123"), ("country", "Country") })
            Assert.Equal(pair.Item2, body.GetProperty(pair.Item1).GetString());
        Assert.Contains("latlng=6.9050,79.9195", handler.Query);
    }

    [Fact]
    public async Task MissingComponents_AreNull()
    {
        using var response = await Send(Reply(Address()), new { latitude = 0, longitude = 0 });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var field in new[] { "street", "neighborhood", "postalCode", "city", "province", "country" })
            Assert.Equal(JsonValueKind.Null, body.GetProperty(field).ValueKind);
    }

    [Fact]
    public async Task Fallbacks_UsePostalTownAndSmallestSublocality()
    {
        using var response = await Send(Reply(Address(("postal_town", "Town"), ("administrative_area_level_2", "District"),
            ("sublocality_level_1", "Large"), ("sublocality_level_2", "Small"))), new { latitude = 0, longitude = 0 });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Town", body.GetProperty("city").GetString());
        Assert.Equal("Small", body.GetProperty("neighborhood").GetString());
    }

    [Theory]
    [InlineData("{\"status\":\"ZERO_RESULTS\",\"results\":[]}")]
    [InlineData("{\"status\":\"OK\",\"results\":[]}")]
    [InlineData("{\"status\":\"OK\",\"results\":[{\"formatted_address\":\" \"}]}")]
    public async Task NoUsefulResult_Return404(string json)
    {
        using var response = await Send(Reply(json), new { latitude = 0, longitude = 0 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("manually", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task UpstreamHttpFailures_AreSafe_AndNeverRetried(int status)
    {
        var handler = Reply(Secret, (HttpStatusCode)status);
        using var response = await Send(handler, new { latitude = 0, longitude = 0 });
        await AssertUnavailable(response);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("REQUEST_DENIED")]
    [InlineData("OVER_QUERY_LIMIT")]
    [InlineData("OVER_DAILY_LIMIT")]
    [InlineData("UNKNOWN_ERROR")]
    [InlineData("INVALID_REQUEST")]
    public async Task GoogleStatusFailures_AreSafe(string status)
    {
        var handler = Reply(JsonSerializer.Serialize(new { status, error_message = Secret }));
        using var response = await Send(handler, new { latitude = 0, longitude = 0 });
        await AssertUnavailable(response);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"status\":\"OK\",\"results\":{}}")]
    [InlineData("{\"status\":\"OK\",\"results\":[{\"formatted_address\":1}]}")]
    public async Task MalformedResponse_IsSafe(string json)
    {
        using var response = await Send(Reply(json), new { latitude = 0, longitude = 0 });
        await AssertUnavailable(response);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TimeoutAndNetworkErrors_AreSafe(bool timeout)
    {
        var handler = new Handler((_, _) => throw (timeout
            ? new TaskCanceledException(Secret) : new HttpRequestException(Secret)));
        using var response = await Send(handler, new { latitude = 0, longitude = 0 });
        await AssertUnavailable(response);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task UnconfiguredKey_DisablesPreviewWithoutHttpCall()
    {
        var handler = Reply(Address());
        using var response = await Send(handler, new { latitude = 0, longitude = 0 }, key: "");
        await AssertUnavailable(response);
        Assert.Equal(0, handler.Calls);
    }

    private static async Task AssertUnavailable(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("manually", body);
        Assert.DoesNotContain(Secret, body);
        Assert.DoesNotContain("maps.googleapis.com", body);
        Assert.DoesNotContain("Exception", body);
    }
}
