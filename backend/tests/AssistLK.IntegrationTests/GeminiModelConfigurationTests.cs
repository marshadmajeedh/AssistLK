using System.Net;
using System.Text;
using AssistLK.Agents.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AssistLK.IntegrationTests;

/// <summary>
/// Regression tests verifying:
/// 1. Default model resolves to gemini-3.6-flash
/// 2. Configuration override still works
/// 3. Missing configuration uses valid fallback
/// 4. Transient HTTP failure retries (429, 503)
/// 5. Non-retriable failure fast-fail (400, 401, 403, 404)
/// </summary>
public class GeminiModelConfigurationTests
{
    private const string ValidGeminiResponseJson = """
    {
      "candidates": [
        {
          "content": {
            "parts": [
              {
                "text": "{\"category\":\"Plumbing\",\"problemSummary\":\"Pipe leak\",\"urgency\":\"High\",\"needsMoreInformation\":false,\"confidence\":0.9}"
              }
            ]
          }
        }
      ]
    }
    """;

    [Fact]
    public void DefaultModel_ResolvesToGemini36Flash()
    {
        // 1. Default model resolves to gemini-3.6-flash
        var service = new GeminiService();

        Assert.Equal("gemini-3.6-flash", service.Model);
        Assert.Equal(GeminiService.DefaultModel, service.Model);
    }

    [Fact]
    public void ConfigurationOverride_WorksAsExpected()
    {
        // 2. Configuration override still works
        var settings = new Dictionary<string, string?>
        {
            { "Gemini:Model", "gemini-1.5-pro-override" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var service = new GeminiService(configuration: configuration);

        Assert.Equal("gemini-1.5-pro-override", service.Model);
    }

    [Fact]
    public void MissingConfiguration_UsesValidFallback()
    {
        // 3. Missing configuration uses valid fallback
        var serviceWithNullConfig = new GeminiService(configuration: null);
        Assert.Equal("gemini-3.6-flash", serviceWithNullConfig.Model);

        var emptyConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var serviceWithEmptyConfig = new GeminiService(configuration: emptyConfig);
        Assert.Equal("gemini-3.6-flash", serviceWithEmptyConfig.Model);

        var whitespaceConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Gemini:Model", "   " }
            })
            .Build();
        var serviceWithWhitespaceConfig = new GeminiService(configuration: whitespaceConfig);
        Assert.Equal("gemini-3.6-flash", serviceWithWhitespaceConfig.Model);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task TransientFailure_RetriesAndSucceedsOnSubsequentAttempt(HttpStatusCode transientStatusCode)
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(transientStatusCode));
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidGeminiResponseJson, Encoding.UTF8, "application/json")
        });

        using var httpClient = new HttpClient(handler);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Gemini:Model", "gemini-3.6-flash" },
                { "Gemini:ApiKey", "test-api-key" }
            })
            .Build();

        var service = new GeminiService(
            httpClient: httpClient,
            configuration: configuration,
            retryDelay: TimeSpan.Zero);

        // Act
        var result = await service.GenerateContentAsync("Test prompt");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Plumbing", result);
        Assert.Equal(2, handler.CallCount); // 1 initial transient failure + 1 successful retry
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task NonRetriableFailure_DoesNotRetry_ReturnsNull(HttpStatusCode nonRetriableStatusCode)
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(nonRetriableStatusCode));
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidGeminiResponseJson, Encoding.UTF8, "application/json")
        });

        using var httpClient = new HttpClient(handler);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Gemini:Model", "gemini-3.6-flash" },
                { "Gemini:ApiKey", "test-api-key" }
            })
            .Build();

        var service = new GeminiService(
            httpClient: httpClient,
            configuration: configuration,
            retryDelay: TimeSpan.Zero);

        // Act
        var result = await service.GenerateContentAsync("Test prompt");

        // Assert
        Assert.Null(result);
        Assert.Equal(1, handler.CallCount); // Failed fast without retrying
    }

    [Fact]
    public async Task ExhaustedRetries_ReturnsNull()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        using var httpClient = new HttpClient(handler);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Gemini:Model", "gemini-3.6-flash" },
                { "Gemini:ApiKey", "test-api-key" }
            })
            .Build();

        var service = new GeminiService(
            httpClient: httpClient,
            configuration: configuration,
            retryDelay: TimeSpan.Zero);

        // Act
        var result = await service.GenerateContentAsync("Test prompt");

        // Assert
        Assert.Null(result);
        Assert.Equal(3, handler.CallCount); // 3 attempts made and exhausted
    }

    [Fact]
    public async Task LiveGemini_WhenApiKeyConfigured_WaterLeakingHeavilyFromKitchenSink_ReturnsExpectedAnalysis()
    {
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddUserSecrets("15fcbe56-7908-4746-bfbe-0692dc0c8045")
            .Build();

        var apiKey = config["Gemini:ApiKey"] ?? config["GOOGLE_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            // If secrets not configured in environment, skip live call
            return;
        }

        var toolRegistry = new AssistLK.Agents.Core.ToolRegistry();
        toolRegistry.Register(new AssistLK.Agents.Tools.ProblemClassificationTool());
        toolRegistry.Register(new AssistLK.Agents.Tools.LocationExtractionTool());
        toolRegistry.Register(new AssistLK.Agents.Tools.ServiceKnowledgeTool());
        var toolExecutor = new AssistLK.Agents.Core.ToolExecutor(toolRegistry);

        var geminiService = new GeminiService(configuration: config);
        var agent = new AssistLK.Agents.Agents.ProblemUnderstandingAgent(
            toolExecutor,
            geminiService,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AssistLK.Agents.Agents.ProblemUnderstandingAgent>.Instance);

        var context = new AssistLK.Agents.Core.AgentContext
        {
            Input = "Water leaking heavily from kitchen sink"
        };

        var result = await agent.ExecuteAsync(context);

        Assert.True(result.Success);
        var output = Assert.IsType<AssistLK.Agents.Models.ProblemUnderstandingOutput>(result.Data);

        // If live API quota was exceeded or service degraded due to rate limiting,
        // verify that safe degraded fallback was activated cleanly without crashing.
        if (output.Category == "Unclassified" && output.NeedsMoreInformation)
        {
            Assert.Equal(AssistLK.Domain.Enums.ServiceRequestUrgency.Unknown, output.Urgency);
            return;
        }

        Assert.Equal("Plumbing", output.Category);
        Assert.Equal(AssistLK.Domain.Enums.ServiceRequestUrgency.High, output.Urgency);
        Assert.False(output.NeedsMoreInformation);
        Assert.NotNull(output.ProblemSummary);
        Assert.True(output.Confidence >= 0.7m);
    }

    [Fact]
    public async Task SafeDiagnosticLogging_LogsAllowedFields_NeverLeaksSensitiveData()
    {
        const string secretApiKey = "AIzaSySecretApiKey123456789";
        const string customerPrompt = "Customer private description: leaking pipe at address 123 Main St";

        var testLogger = new TestLogger<GeminiService>();
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidGeminiResponseJson, Encoding.UTF8, "application/json")
        });

        using var httpClient = new HttpClient(handler);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Gemini:Model", "gemini-3.6-flash" },
                { "Gemini:ApiKey", secretApiKey }
            })
            .Build();

        var service = new GeminiService(
            httpClient: httpClient,
            configuration: configuration,
            logger: testLogger,
            retryDelay: TimeSpan.Zero);

        var result = await service.GenerateContentAsync(customerPrompt);

        Assert.NotNull(result);

        // Verify Allowed Logs: model name, request started, success status
        Assert.Contains(testLogger.Messages, m => m.Contains("gemini-3.6-flash"));
        Assert.Contains(testLogger.Messages, m => m.Contains("Gemini execution started"));
        Assert.Contains(testLogger.Messages, m => m.Contains("Gemini response received: true"));

        // Verify Forbidden Logs (NEVER logged): API key, prompt, customer data, raw full response body, headers
        Assert.DoesNotContain(testLogger.Messages, m => m.Contains(secretApiKey));
        Assert.DoesNotContain(testLogger.Messages, m => m.Contains(customerPrompt));
        Assert.DoesNotContain(testLogger.Messages, m => m.Contains("123 Main St"));
        Assert.DoesNotContain(testLogger.Messages, m => m.Contains(ValidGeminiResponseJson));
        Assert.DoesNotContain(testLogger.Messages, m => m.Contains("x-goog-api-key"));
        Assert.DoesNotContain(testLogger.Messages, m => m.Contains("key="));
    }

    private class TestLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<CancellationToken, Task<HttpResponseMessage>>> _handlers = new();
        public int CallCount { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }
        public List<HttpRequestMessage> SentRequests { get; } = new();

        public void EnqueueResponse(HttpResponseMessage response)
        {
            _handlers.Enqueue(_ => Task.FromResult(response));
        }

        public void EnqueueException(Exception exception)
        {
            _handlers.Enqueue(_ => Task.FromException<HttpResponseMessage>(exception));
        }

        public void EnqueueDelayedResponse(TimeSpan delay, HttpResponseMessage response)
        {
            _handlers.Enqueue(async ct =>
            {
                await Task.Delay(delay, ct);
                return response;
            });
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            SentRequests.Add(request);

            if (_handlers.Count > 0)
            {
                return await _handlers.Dequeue()(cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }
    }

    [Fact]
    public void DefaultAttemptTimeout_IsTwentySeconds()
    {
        var service = new GeminiService();
        Assert.Equal(TimeSpan.FromSeconds(20), service.AttemptTimeout);
    }

    [Fact]
    public void AttemptTimeout_ConfigurationOverride_Works()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Gemini:AttemptTimeoutSeconds", "15" }
            })
            .Build();

        var service = new GeminiService(configuration: configuration);
        Assert.Equal(TimeSpan.FromSeconds(15), service.AttemptTimeout);
    }

    [Fact]
    public void GeminiRetryPolicyWorstCaseBudget_IsStrictlyBelowFlutterReceiveTimeout()
    {
        // Flutter receive timeout is 90 seconds.
        // Gemini retry-policy worst-case budget:
        //   20s attempt 1
        //   + 0.5s backoff
        //   + 20s attempt 2
        //   + 1.0s backoff
        //   + 20s attempt 3
        //   = 61.5s
        // Leaving approximately 28.5s for workflow/orchestration/database overhead.
        var perAttemptTimeout = GeminiService.DefaultAttemptTimeout;
        const int maxAttempts = 3;
        var totalBackoff = TimeSpan.FromMilliseconds(500) + TimeSpan.FromMilliseconds(1000);
        var totalRetryPolicyBudget = (perAttemptTimeout * maxAttempts) + totalBackoff;

        var flutterReceiveTimeout = TimeSpan.FromSeconds(90);

        Assert.Equal(TimeSpan.FromSeconds(61.5), totalRetryPolicyBudget);
        Assert.True(totalRetryPolicyBudget < flutterReceiveTimeout,
            $"Gemini retry-policy worst-case budget ({totalRetryPolicyBudget.TotalSeconds}s) must be below Flutter receive timeout ({flutterReceiveTimeout.TotalSeconds}s).");
        Assert.True(flutterReceiveTimeout - totalRetryPolicyBudget >= TimeSpan.FromSeconds(20),
            "Expected at least a 20-second safety margin between Gemini retry policy budget and Flutter client timeout.");
    }

    [Fact]
    public async Task SuccessfulGeminiResult_ReturnsExtractedText()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidGeminiResponseJson, Encoding.UTF8, "application/json")
        });

        using var httpClient = new HttpClient(handler);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Gemini:Model", "gemini-3.6-flash" },
                { "Gemini:ApiKey", "test-api-key" }
            })
            .Build();

        var service = new GeminiService(
            httpClient: httpClient,
            configuration: configuration,
            retryDelay: TimeSpan.Zero);

        // Act
        var result = await service.GenerateContentAsync("Customer reported water leakage");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Plumbing", result);
        Assert.Contains("Pipe leak", result);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task TransientFailure_HttpRequestException_RetriesAndSucceedsOnSubsequentAttempt()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        handler.EnqueueException(new HttpRequestException("Connection reset by peer"));
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidGeminiResponseJson, Encoding.UTF8, "application/json")
        });

        using var httpClient = new HttpClient(handler);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Gemini:Model", "gemini-3.6-flash" },
                { "Gemini:ApiKey", "test-api-key" }
            })
            .Build();

        var service = new GeminiService(
            httpClient: httpClient,
            configuration: configuration,
            retryDelay: TimeSpan.Zero);

        // Act
        var result = await service.GenerateContentAsync("Customer reported water leakage");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Plumbing", result);
        Assert.Equal(2, handler.CallCount); // 1 failed attempt + 1 successful retry
    }

    [Fact]
    public async Task Timeout_IndividualAttemptTimesOut_RetriesAndSucceeds()
    {
        // Arrange: attempt 1 times out (50ms limit), attempt 2 returns OK
        var handler = new MockHttpMessageHandler();
        handler.EnqueueDelayedResponse(TimeSpan.FromMilliseconds(150), new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidGeminiResponseJson, Encoding.UTF8, "application/json")
        });
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidGeminiResponseJson, Encoding.UTF8, "application/json")
        });

        using var httpClient = new HttpClient(handler);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Gemini:Model", "gemini-3.6-flash" },
                { "Gemini:ApiKey", "test-api-key" }
            })
            .Build();

        var service = new GeminiService(
            httpClient: httpClient,
            configuration: configuration,
            retryDelay: TimeSpan.Zero,
            attemptTimeout: TimeSpan.FromMilliseconds(40));

        // Act
        var result = await service.GenerateContentAsync("Customer reported water leakage");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Plumbing", result);
        Assert.Equal(2, handler.CallCount); // 1 timeout + 1 successful retry
    }

    [Fact]
    public async Task Timeout_ExhaustedAttempts_ReturnsNull()
    {
        // Arrange: all 3 attempts time out
        var handler = new MockHttpMessageHandler();
        handler.EnqueueDelayedResponse(TimeSpan.FromMilliseconds(150), new HttpResponseMessage(HttpStatusCode.OK));
        handler.EnqueueDelayedResponse(TimeSpan.FromMilliseconds(150), new HttpResponseMessage(HttpStatusCode.OK));
        handler.EnqueueDelayedResponse(TimeSpan.FromMilliseconds(150), new HttpResponseMessage(HttpStatusCode.OK));

        using var httpClient = new HttpClient(handler);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Gemini:Model", "gemini-3.6-flash" },
                { "Gemini:ApiKey", "test-api-key" }
            })
            .Build();

        var service = new GeminiService(
            httpClient: httpClient,
            configuration: configuration,
            retryDelay: TimeSpan.Zero,
            attemptTimeout: TimeSpan.FromMilliseconds(40));

        // Act
        var result = await service.GenerateContentAsync("Customer reported water leakage");

        // Assert
        Assert.Null(result);
        Assert.Equal(3, handler.CallCount); // 3 attempts made and timed out
    }

    [Fact]
    public async Task DegradedFallback_WhenGeminiReturnsNull_ProblemUnderstandingAgentProducesDegradedOutput()
    {
        // Arrange: Gemini returns null (e.g. after network failure or exhausted retries)
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        using var httpClient = new HttpClient(handler);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Gemini:Model", "gemini-3.6-flash" },
                { "Gemini:ApiKey", "test-api-key" }
            })
            .Build();

        var geminiService = new GeminiService(
            httpClient: httpClient,
            configuration: configuration,
            retryDelay: TimeSpan.Zero);

        var toolRegistry = new AssistLK.Agents.Core.ToolRegistry();
        toolRegistry.Register(new AssistLK.Agents.Tools.ProblemClassificationTool());
        toolRegistry.Register(new AssistLK.Agents.Tools.LocationExtractionTool());
        toolRegistry.Register(new AssistLK.Agents.Tools.ServiceKnowledgeTool());
        var toolExecutor = new AssistLK.Agents.Core.ToolExecutor(toolRegistry);

        var agent = new AssistLK.Agents.Agents.ProblemUnderstandingAgent(
            toolExecutor,
            geminiService,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AssistLK.Agents.Agents.ProblemUnderstandingAgent>.Instance);

        var context = new AssistLK.Agents.Core.AgentContext
        {
            Input = "Water pipe has burst in the bathroom and water is everywhere"
        };

        // Act
        var result = await agent.ExecuteAsync(context);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("AwaitingInformation", result.NextAction);

        var output = Assert.IsType<AssistLK.Agents.Models.ProblemUnderstandingOutput>(result.Data);
        Assert.Equal("Unclassified", output.Category);
        Assert.Equal(AssistLK.Domain.Enums.ServiceRequestUrgency.Unknown, output.Urgency);
        Assert.True(output.NeedsMoreInformation);
        Assert.Equal(0.2m, output.Confidence);
        Assert.NotEmpty(output.FollowUpQuestions);
        Assert.Contains("possible", output.ProblemSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("True", output.AdditionalInformation["Degraded"]);
    }

    [Fact]
    public async Task CallerCancellation_PreCancelled_DoesNotMakeHttpRequest_ThrowsImmediately()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidGeminiResponseJson, Encoding.UTF8, "application/json")
        });

        using var httpClient = new HttpClient(handler);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Gemini:Model", "gemini-3.6-flash" },
                { "Gemini:ApiKey", "test-api-key" }
            })
            .Build();

        var service = new GeminiService(
            httpClient: httpClient,
            configuration: configuration,
            retryDelay: TimeSpan.Zero);

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await service.GenerateContentAsync("Customer reported water leakage", cancellationToken: cts.Token);
        });

        Assert.Equal(0, handler.CallCount); // Immediate fail-fast before HTTP request
    }

    [Fact]
    public async Task CallerCancellation_DuringExecution_PropagatesWithoutRetrying()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        var handler = new MockHttpMessageHandler();
        // Handler triggers caller cancellation
        handler.EnqueueDelayedResponse(TimeSpan.FromMilliseconds(50), new HttpResponseMessage(HttpStatusCode.OK));

        using var httpClient = new HttpClient(handler);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Gemini:Model", "gemini-3.6-flash" },
                { "Gemini:ApiKey", "test-api-key" }
            })
            .Build();

        var service = new GeminiService(
            httpClient: httpClient,
            configuration: configuration,
            retryDelay: TimeSpan.FromSeconds(1));

        // Act & Assert
        cts.CancelAfter(15);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await service.GenerateContentAsync("Customer reported water leakage", cancellationToken: cts.Token);
        });

        Assert.True(handler.CallCount <= 1); // No retries attempted on caller cancellation
    }

    [Fact]
    public async Task RequestConstruction_DoesNotIncludeApiKeyInUrlQueryString_SendsViaHeader()
    {
        const string secretApiKey = "AIzaSySecretApiKey123456789";

        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidGeminiResponseJson, Encoding.UTF8, "application/json")
        });

        using var httpClient = new HttpClient(handler);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Gemini:Model", "gemini-3.6-flash" },
                { "Gemini:ApiKey", secretApiKey }
            })
            .Build();

        var service = new GeminiService(
            httpClient: httpClient,
            configuration: configuration,
            retryDelay: TimeSpan.Zero);

        var result = await service.GenerateContentAsync("Customer reported water leakage");

        Assert.NotNull(result);
        Assert.NotNull(handler.LastRequest);

        // 1. Verify Gemini request URI does NOT contain 'key=', 'GEMINI_API_KEY', or the configured secret key
        var requestUri = handler.LastRequest.RequestUri?.ToString() ?? string.Empty;
        var query = handler.LastRequest.RequestUri?.Query ?? string.Empty;

        Assert.DoesNotContain("key=", query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(secretApiKey, requestUri, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GEMINI_API_KEY", requestUri, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent", handler.LastRequest.RequestUri?.GetLeftPart(UriPartial.Path));

        // 2. Verify Request contains the expected API-key header: x-goog-api-key
        Assert.True(handler.LastRequest.Headers.Contains("x-goog-api-key"), "Expected 'x-goog-api-key' request header.");
        var headerValue = handler.LastRequest.Headers.GetValues("x-goog-api-key").FirstOrDefault();
        Assert.Equal(secretApiKey, headerValue);
    }
}

