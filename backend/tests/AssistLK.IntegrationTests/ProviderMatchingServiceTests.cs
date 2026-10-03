using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AssistLK.Application.Services.Providers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AssistLK.IntegrationTests;

public class ProviderMatchingServiceTests
{
    private readonly Mock<ILogger<ProviderMatchingService>> _loggerMock = new();

    private class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responder;

        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        public TestHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content != null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return await _responder(request);
        }
    }

    [Fact]
    public async Task StartMatchingAsync_Success_ReturnsDeserializedMatchResponse()
    {
        // Arrange
        var expectedResponse = new MatchResponse
        {
            ThreadId = "thread-12345",
            Status = "Pending",
            RecommendedProvider = new RecommendedProvider
            {
                Id = "p-001",
                Name = "Kamal Perera",
                Score = 0.95m,
                DistanceKm = 1.2m,
                Rating = 4.8m,
                Verified = true,
                MatchRationale = "Top ranked match based on distance and rating.",
                Rank = 1
            },
            TokensConsumed = new TokenUsageDto
            {
                InputTokens = 45,
                OutputTokens = 18,
                TotalTokens = 63
            }
        };

        var handler = new TestHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expectedResponse)
            };
            return Task.FromResult(response);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000") };
        var service = new ProviderMatchingService(httpClient, _loggerMock.Object);

        var request = new MatchStartRequest
        {
            Objective = "Burst pipe in kitchen. Category: plumbing, Urgency: 5",
            Urgency = 5,
            CustomerLatitude = 6.9270,
            CustomerLongitude = 79.8610,
            EligibleProviders = new List<ProviderCandidateDto>
            {
                new()
                {
                    ProviderId = "p-001",
                    Name = "Kamal Perera",
                    Rating = 4.8,
                    Latitude = 6.9271,
                    Longitude = 79.8612,
                    Verified = true,
                    OperatingRadiusKm = 10.0,
                    Skills = new List<string> { "plumbing" }
                }
            }
        };

        // Act
        var result = await service.StartMatchingAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("thread-12345", result.ThreadId);
        Assert.Equal("Pending", result.Status);
        Assert.NotNull(result.RecommendedProvider);
        Assert.Equal("Kamal Perera", result.RecommendedProvider.Name);
        Assert.Equal(0.95m, result.RecommendedProvider.Score);
        Assert.NotNull(result.TokensConsumed);
        Assert.Equal(63, result.TokensConsumed.TotalTokens);

        Assert.Equal("/match/start", handler.LastRequest?.RequestUri?.AbsolutePath);
        Assert.Contains("p-001", handler.LastRequestBody);
    }

    [Fact]
    public async Task StartMatchingAsync_HttpError_ThrowsApplicationException()
    {
        // Arrange
        var handler = new TestHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            return Task.FromResult(response);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000") };
        var service = new ProviderMatchingService(httpClient, _loggerMock.Object);

        var request = new MatchStartRequest
        {
            Objective = "Plumbing repair",
            Urgency = 2
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ApplicationException>(() => service.StartMatchingAsync(request));
        Assert.Equal("AI Matching Engine is currently unavailable.", ex.Message);
    }

    [Fact]
    public async Task StartMatchingAsync_Timeout_ThrowsApplicationException()
    {
        // Arrange
        var handler = new TestHttpMessageHandler(_ => throw new TaskCanceledException("Request timed out."));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000") };
        var service = new ProviderMatchingService(httpClient, _loggerMock.Object);

        var request = new MatchStartRequest { Objective = "Plumbing repair" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ApplicationException>(() => service.StartMatchingAsync(request));
        Assert.Equal("AI Matching Engine timed out.", ex.Message);
    }

    [Fact]
    public async Task ResumeMatchingAsync_Approve_ReturnsApprovedResponse()
    {
        // Arrange
        var expectedResponse = new MatchResponse
        {
            ThreadId = "thread-12345",
            Status = "Approved"
        };

        var handler = new TestHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expectedResponse)
            };
            return Task.FromResult(response);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000") };
        var service = new ProviderMatchingService(httpClient, _loggerMock.Object);

        // Act
        var result = await service.ResumeMatchingAsync("thread-12345", "Approve", "admin-001");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("thread-12345", result.ThreadId);
        Assert.Equal("Approved", result.Status);

        Assert.Equal("/match/resume", handler.LastRequest?.RequestUri?.AbsolutePath);
        Assert.Contains("\"action\":\"Approve\"", handler.LastRequestBody);
        Assert.Contains("\"admin_id\":\"admin-001\"", handler.LastRequestBody);
    }

    [Fact]
    public async Task ResumeMatchingAsync_Reject_ReturnsRejectedResponse()
    {
        // Arrange
        var expectedResponse = new MatchResponse
        {
            ThreadId = "thread-12345",
            Status = "Rejected"
        };

        var handler = new TestHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expectedResponse)
            };
            return Task.FromResult(response);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000") };
        var service = new ProviderMatchingService(httpClient, _loggerMock.Object);

        // Act
        var result = await service.ResumeMatchingAsync("thread-12345", "Reject", "admin-002");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Rejected", result.Status);
        Assert.Contains("\"action\":\"Reject\"", handler.LastRequestBody);
    }

    [Fact]
    public async Task ResumeMatchingAsync_HttpError_ThrowsApplicationException()
    {
        // Arrange
        var handler = new TestHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            return Task.FromResult(response);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000") };
        var service = new ProviderMatchingService(httpClient, _loggerMock.Object);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ApplicationException>(() =>
            service.ResumeMatchingAsync("thread-999", "Approve", "admin-001"));

        Assert.Equal("Could not process Admin match decision (service unavailable).", ex.Message);
    }

    [Fact]
    public async Task ResumeMatchingAsync_Timeout_ThrowsApplicationException()
    {
        // Arrange
        var handler = new TestHttpMessageHandler(_ => throw new TaskCanceledException("Timeout"));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000") };
        var service = new ProviderMatchingService(httpClient, _loggerMock.Object);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ApplicationException>(() =>
            service.ResumeMatchingAsync("thread-999", "Approve", "admin-001"));

        Assert.Equal("Admin decision timed out.", ex.Message);
    }
}
