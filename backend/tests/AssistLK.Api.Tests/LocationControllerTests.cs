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
        public string? UserAgent { get; private set; }
        public string? Path { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Query = request.RequestUri!.Query;
            UserAgent = request.Headers.UserAgent.ToString();
            Path = request.RequestUri.AbsolutePath;
            return send(request, cancellationToken);
        }
    }

    private static Handler Reply(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json) }));

    private static string Address(params (string Type, string Name)[] components) => JsonSerializer.Serialize(new
    {
        display_name = "  Example Road, Example City  ", place_id = 123,
        address = components.ToDictionary(c => c.Type, c => c.Name)
    });

    // Run the real controller, JWT authorization, validation, and typed Nominatim service.
    // Any attempt to resolve the database fails; preview cannot read or write it.
    private static async Task<HttpResponseMessage> Send(Handler handler, object payload,
        UserRole? role = UserRole.Customer)
    {
        using var parent = new AssistLKApiTestFactory();
        using var factory = parent.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(new LocationGeocodingOptions());
            services.AddScoped<AssistLKDbContext>(_ => throw new InvalidOperationException("Preview accessed the database."));
            services.AddHttpClient<ILocationGeocodingService, NominatimReverseGeocodingService>()
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
        var handler = Reply(Address(("road", "Example Road"), ("city", "Example City")));
        using var response = await Send(handler, new { latitude = 0, longitude = 0 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Example Road, Example City", body.GetProperty("formattedAddress").GetString());
        Assert.Equal("Approximate", body.GetProperty("resolutionLevel").GetString());
        Assert.Equal("123", body.GetProperty("placeId").GetString());
        Assert.Contains("lat=0&lon=0", handler.Query);
        Assert.Contains("format=jsonv2&addressdetails=1", handler.Query);
        Assert.DoesNotContain("key=", handler.Query);
        Assert.Equal("/reverse", handler.Path);
        Assert.Equal("AssistLK-SE3090/1.0", handler.UserAgent);
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
    public async Task InvalidCoordinates_Return400_WithoutCallingNominatim(int latitude, int longitude)
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
        (string, string)[] components = [("road", "Road"), ("neighbourhood", "Neighborhood"),
            ("city", "City"), ("state", "Province"), ("postcode", "123"), ("country", "Country")];
        var handler = Reply(Address(reverse ? components.Reverse().ToArray() : components));
        using var response = await Send(handler, new { latitude = 6.9050m, longitude = 79.9195m });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var pair in new[] { ("street", "Road"), ("neighborhood", "Neighborhood"), ("city", "City"),
            ("province", "Province"), ("postalCode", "123"), ("country", "Country") })
            Assert.Equal(pair.Item2, body.GetProperty(pair.Item1).GetString());
        Assert.Contains("lat=6.905&lon=79.9195", handler.Query);
    }

    [Fact]
    public async Task MissingComponents_AreNull()
    {
        using var response = await Send(Reply(Address()), new { latitude = 0, longitude = 0 });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var field in new[] { "street", "neighborhood", "postalCode", "city", "province", "country" })
            Assert.Equal(JsonValueKind.Null, body.GetProperty(field).ValueKind);
    }

    [Theory]
    [InlineData("road", "neighbourhood", "city")]
    [InlineData("pedestrian", "suburb", "town")]
    [InlineData("residential", "quarter", "village")]
    [InlineData("road", "suburb", "municipality")]
    public async Task Fallbacks_HandleLocalAddressVariations(string road, string neighborhood, string city)
    {
        using var response = await Send(Reply(Address((road, "Road"), (neighborhood, "Area"),
            (city, "Town"), ("province", "Province"))), new { latitude = 0, longitude = 0 });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Road", body.GetProperty("street").GetString());
        Assert.Equal("Area", body.GetProperty("neighborhood").GetString());
        Assert.Equal("Town", body.GetProperty("city").GetString());
        Assert.Equal("Province", body.GetProperty("province").GetString());
    }

    [Theory]
    [InlineData("{\"error\":\"Unable to geocode\"}")]
    [InlineData("{}")]
    [InlineData("{\"display_name\":\" \"}")]
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

    [Fact]
    public async Task Http404_ReturnsManualFallback()
    {
        using var response = await Send(Reply("not returned to client", HttpStatusCode.NotFound), new { latitude = 0, longitude = 0 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"display_name\":1}")]
    [InlineData("{\"display_name\":\"Area\",\"address\":[]}")]
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
    public async Task DefaultConfiguration_NeedsNoCredentials()
    {
        new LocationGeocodingOptions().Validate();
        var handler = Reply(Address());
        using var response = await Send(handler, new { latitude = 0, longitude = 0 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, handler.Calls);
    }

    private static async Task AssertUnavailable(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("manually", body);
        Assert.DoesNotContain(Secret, body);
        Assert.DoesNotContain("nominatim.openstreetmap.org", body);
        Assert.DoesNotContain("Exception", body);
    }
}
