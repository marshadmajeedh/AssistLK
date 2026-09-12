using System.Diagnostics;
using System.Net;
using AssistLK.Infrastructure.ExternalServices;

namespace AssistLK.Api.Tests;

public class NominatimCoordinatorTests
{
    private sealed class Handler : HttpMessageHandler
    {
        public List<long> Starts { get; } = new();
        public bool Fail { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Starts.Add(Stopwatch.GetTimestamp());
            return Task.FromResult(new HttpResponseMessage(Fail ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK)
            { Content = new StringContent("{\"display_name\":\"Example Road\"}") });
        }
    }

    [Fact]
    public async Task ConcurrentTypedClientInstances_ShareOneApplicationThrottle()
    {
        using var gate = new NominatimRequestCoordinator();
        var handler = new Handler();
        using var firstHttp = new HttpClient(handler, disposeHandler: false);
        using var secondHttp = new HttpClient(handler);
        var first = new NominatimReverseGeocodingService(firstHttp, new(), gate);
        var second = new NominatimReverseGeocodingService(secondHttp, new(), gate);
        await Task.WhenAll(first.ReverseGeocodeAsync(0, 0), second.ReverseGeocodeAsync(1, 1),
            first.ReverseGeocodeAsync(2, 2));
        Assert.Equal(3, handler.Starts.Count);
        for (var index = 1; index < handler.Starts.Count; index++)
            Assert.True(Stopwatch.GetElapsedTime(handler.Starts[index - 1], handler.Starts[index]) >= TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ConcurrentIdenticalQueries_UseOneOutboundCallAndCachedPreview()
    {
        using var gate = new NominatimRequestCoordinator();
        var handler = new Handler();
        using var http = new HttpClient(handler);
        var service = new NominatimReverseGeocodingService(http, new(), gate);
        var previews = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => service.ReverseGeocodeAsync(6.9m, 79.9m)));
        Assert.Single(handler.Starts);
        Assert.All(previews, preview => Assert.Equal("Example Road", preview!.FormattedAddress));
    }

    [Fact]
    public async Task FailedRequest_StillEnforcesCooldown_AndIsNotCached()
    {
        using var gate = new NominatimRequestCoordinator();
        var handler = new Handler { Fail = true };
        using var http = new HttpClient(handler);
        var service = new NominatimReverseGeocodingService(http, new(), gate);
        await Assert.ThrowsAsync<AssistLK.Application.Common.Exceptions.LocationGeocodingUnavailableException>(() => service.ReverseGeocodeAsync(0, 0));
        handler.Fail = false;
        await service.ReverseGeocodeAsync(0, 0);
        Assert.Equal(2, handler.Starts.Count);
        Assert.True(Stopwatch.GetElapsedTime(handler.Starts[0], handler.Starts[1]) >= TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CancellationDuringCooldown_DoesNotDispatch()
    {
        using var gate = new NominatimRequestCoordinator();
        var handler = new Handler();
        using var http = new HttpClient(handler);
        var service = new NominatimReverseGeocodingService(http, new(), gate);
        await service.ReverseGeocodeAsync(0, 0);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ReverseGeocodeAsync(1, 1, cancellation.Token));
        Assert.Single(handler.Starts);
    }

    [Theory]
    [InlineData("http://nominatim.openstreetmap.org", "AssistLK-SE3090/1.0")]
    [InlineData("https://user:password@example.com", "AssistLK-SE3090/1.0")]
    [InlineData("https://example.com?key=secret", "AssistLK-SE3090/1.0")]
    [InlineData("https://nominatim.openstreetmap.org", "")]
    [InlineData("https://nominatim.openstreetmap.org", ".NET/8.0")]
    public void InvalidConfiguration_IsRejected(string url, string userAgent)
    {
        Assert.Throws<InvalidOperationException>(() => new LocationGeocodingOptions {
            NominatimBaseUrl = url, UserAgent = userAgent }.Validate());
    }

    [Fact]
    public void CompatibleProviderHost_CanBeChangedWithoutClientChanges()
    {
        new LocationGeocodingOptions { NominatimBaseUrl = "https://geocoding.example.org/nominatim" }.Validate();
        Assert.Throws<InvalidOperationException>(() => new LocationGeocodingOptions { Provider = "Unsupported" }.Validate());
    }
}
