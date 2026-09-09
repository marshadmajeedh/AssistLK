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

        // Verify Forbidden Logs (NEVER logged): API key, prompt, customer data, raw full response body
        Assert.DoesNotContain(testLogger.Messages, m => m.Contains(secretApiKey));
        Assert.DoesNotContain(testLogger.Messages, m => m.Contains(customerPrompt));
        Assert.DoesNotContain(testLogger.Messages, m => m.Contains("123 Main St"));
        Assert.DoesNotContain(testLogger.Messages, m => m.Contains(ValidGeminiResponseJson));
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
        private readonly Queue<HttpResponseMessage> _responses = new();
        public int CallCount { get; private set; }

        public void EnqueueResponse(HttpResponseMessage response)
        {
            _responses.Enqueue(response);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            if (_responses.Count > 0)
            {
                return Task.FromResult(_responses.Dequeue());
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }
    }
}
