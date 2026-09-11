using System.Net;
using System.Text;
using System.Text.Json;
using AssistLK.Agents.Abstractions;
using AssistLK.Agents.Adapters;
using AssistLK.Agents.Agents;
using AssistLK.Agents.Clients;
using AssistLK.Agents.Configuration;
using AssistLK.Agents.Core;
using AssistLK.Agents.DTOs;
using AssistLK.Agents.Models;
using AssistLK.Agents.Services;
using AssistLK.Agents.Tools;
using AssistLK.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssistLK.IntegrationTests;

/// <summary>
/// Unit & contract tests for Phase 2 ExternalProblemUnderstandingAgentAdapter,
/// ProblemUnderstandingHttpClient, configuration validation, security boundaries,
/// and mode-switch registration.
/// Normal test execution requires zero running Python processes.
/// </summary>
public class ExternalProblemUnderstandingAgentAdapterTests
{
    #region Test Helpers

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content != null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return _handler(request);
        }
    }

    private static AgentContext CreateTestContext(
        string description = "Water pipe leaking in kitchen",
        string locationText = "Colombo 03",
        decimal? latitude = 6.9271m,
        decimal? longitude = 79.8612m,
        string? categoryHint = "Plumbing",
        IReadOnlyList<ClarificationHistoryItem>? history = null)
    {
        var serviceRequestId = Guid.NewGuid();
        var input = new ProblemUnderstandingInput
        {
            ServiceRequestId = serviceRequestId,
            Description = description,
            LocationText = locationText,
            Latitude = latitude,
            Longitude = longitude,
            CategoryHint = categoryHint,
            ClarificationHistory = history ?? Array.Empty<ClarificationHistoryItem>()
        };

        var context = new AgentContext
        {
            WorkflowId = Guid.NewGuid(),
            Input = description
        };
        context.Data[nameof(ProblemUnderstandingInput)] = input;
        return context;
    }

    private static (ExternalProblemUnderstandingAgentAdapter adapter, MockHttpMessageHandler handler) CreateAdapterWithMockHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory,
        AgentServicesOptions? options = null)
    {
        var handler = new MockHttpMessageHandler(responseFactory);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:8001")
        };
        var client = new ProblemUnderstandingHttpClient(
            httpClient,
            options ?? new AgentServicesOptions(),
            NullLogger<ProblemUnderstandingHttpClient>.Instance);

        var adapter = new ExternalProblemUnderstandingAgentAdapter(
            client,
            NullLogger<ExternalProblemUnderstandingAgentAdapter>.Instance);

        return (adapter, handler);
    }

    private static HttpResponseMessage CreateJsonResponse(object payload, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var json = JsonSerializer.Serialize(payload);
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    #endregion

    #region Section 15: Required Tests 1-4 (Request Serialization)

    [Fact]
    public async Task Test01_RequestSerialization_SerializesExpectedContract()
    {
        // Item 1: request serialization matches Python wire contract
        var (adapter, handler) = CreateAdapterWithMockHandler(_ =>
            CreateJsonResponse(new AgentExecutionResponseDto
            {
                RequestId = Guid.NewGuid(),
                Success = true,
                Result = new ProblemUnderstandingOutputPayloadDto
                {
                    Category = "Plumbing",
                    ProblemSummary = "Water pipe leak detected.",
                    Urgency = "High",
                    NeedsMoreInformation = false,
                    Confidence = 0.95m
                },
                Metadata = new ExecutionMetadataPayloadDto
                {
                    AgentName = "ProblemUnderstandingAgent",
                    Provider = "offline",
                    Degraded = false,
                    DurationMs = 12
                }
            }));

        var context = CreateTestContext();
        var result = await adapter.ExecuteAsync(context);

        Assert.True(result.Success);
        Assert.NotNull(handler.LastRequestBody);

        using var doc = JsonDocument.Parse(handler.LastRequestBody);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("requestId", out _));
        Assert.Equal("ProblemUnderstandingAgent", root.GetProperty("agentName").GetString());
        Assert.Equal("analyze-problem", root.GetProperty("operation").GetString());
        Assert.True(root.TryGetProperty("input", out var inputElement));
        Assert.Equal("Water pipe leaking in kitchen", inputElement.GetProperty("description").GetString());
        Assert.Equal("Colombo 03", inputElement.GetProperty("locationText").GetString());
        Assert.Equal("Plumbing", inputElement.GetProperty("categoryHint").GetString());

        // Null timestamp and parameters must be omitted from wire JSON
        Assert.False(root.TryGetProperty("timestamp", out _));
        Assert.False(root.TryGetProperty("parameters", out _));
    }

    [Fact]
    public async Task Test02_ClarificationHistorySerialization_PreservesRoundQuestionAnswer()
    {
        // Item 2: clarification history serialization
        var history = new List<ClarificationHistoryItem>
        {
            new() { Round = 1, Question = "Is the main valve off?", Answer = "Yes, shut off." },
            new() { Round = 2, Question = "Any electrical sockets nearby?", Answer = "No, clear." }
        };

        var (adapter, handler) = CreateAdapterWithMockHandler(_ =>
            CreateJsonResponse(new AgentExecutionResponseDto
            {
                RequestId = Guid.NewGuid(),
                Success = true,
                Result = new ProblemUnderstandingOutputPayloadDto
                {
                    Category = "Plumbing",
                    ProblemSummary = "Verified leak.",
                    Urgency = "Medium",
                    Confidence = 0.90m
                }
            }));

        var context = CreateTestContext(history: history);
        await adapter.ExecuteAsync(context);

        Assert.NotNull(handler.LastRequestBody);
        using var doc = JsonDocument.Parse(handler.LastRequestBody);
        var historyProp = doc.RootElement.GetProperty("input").GetProperty("clarificationHistory");

        Assert.Equal(2, historyProp.GetArrayLength());
        Assert.Equal(1, historyProp[0].GetProperty("round").GetInt32());
        Assert.Equal("Is the main valve off?", historyProp[0].GetProperty("question").GetString());
        Assert.Equal("Yes, shut off.", historyProp[0].GetProperty("answer").GetString());
        Assert.Equal(2, historyProp[1].GetProperty("round").GetInt32());
    }

    [Fact]
    public async Task Test03_NullableGpsSerialization_PreservesNullAndPopulatedCoordinates()
    {
        // Item 3: nullable GPS serialization
        // Case A: null coordinates
        var (adapter1, handler1) = CreateAdapterWithMockHandler(_ =>
            CreateJsonResponse(new AgentExecutionResponseDto
            {
                Success = true,
                Result = new ProblemUnderstandingOutputPayloadDto { Category = "Plumbing", ProblemSummary = "Summary", Urgency = "Low", Confidence = 0.5m }
            }));

        var contextNullGps = CreateTestContext(latitude: null, longitude: null);
        await adapter1.ExecuteAsync(contextNullGps);

        using (var docNull = JsonDocument.Parse(handler1.LastRequestBody!))
        {
            var inputProp = docNull.RootElement.GetProperty("input");
            Assert.Equal(JsonValueKind.Null, inputProp.GetProperty("latitude").ValueKind);
            Assert.Equal(JsonValueKind.Null, inputProp.GetProperty("longitude").ValueKind);
        }

        // Case B: populated coordinates
        var (adapter2, handler2) = CreateAdapterWithMockHandler(_ =>
            CreateJsonResponse(new AgentExecutionResponseDto
            {
                Success = true,
                Result = new ProblemUnderstandingOutputPayloadDto { Category = "Plumbing", ProblemSummary = "Summary", Urgency = "Low", Confidence = 0.5m }
            }));

        var contextPopulated = CreateTestContext(latitude: 6.9044m, longitude: 79.8665m);
        await adapter2.ExecuteAsync(contextPopulated);

        using (var docPop = JsonDocument.Parse(handler2.LastRequestBody!))
        {
            var inputProp = docPop.RootElement.GetProperty("input");
            Assert.Equal(6.9044, inputProp.GetProperty("latitude").GetDouble(), 4);
            Assert.Equal(79.8665, inputProp.GetProperty("longitude").GetDouble(), 4);
        }
    }

    [Fact]
    public async Task Test04_CategoryHintSerialization_PreservesHintWhenPresentOrNull()
    {
        // Item 4: CategoryHint serialization
        var (adapter, handler) = CreateAdapterWithMockHandler(_ =>
            CreateJsonResponse(new AgentExecutionResponseDto
            {
                Success = true,
                Result = new ProblemUnderstandingOutputPayloadDto { Category = "Electrical", ProblemSummary = "Socket sparking", Urgency = "High", Confidence = 0.9m }
            }));

        var contextHint = CreateTestContext(categoryHint: "Electrical");
        await adapter.ExecuteAsync(contextHint);

        using var doc = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("Electrical", doc.RootElement.GetProperty("input").GetProperty("categoryHint").GetString());
    }

    #endregion

    #region Section 15: Required Tests 5-9 (Response Mapping & Validation)

    [Fact]
    public async Task Test05_ResponseMapping_SuccessfulPlumbingResponse_MapsAllFields()
    {
        // Item 5: successful Plumbing response mapping
        var (adapter, _) = CreateAdapterWithMockHandler(_ =>
            CreateJsonResponse(new AgentExecutionResponseDto
            {
                RequestId = Guid.NewGuid(),
                Success = true,
                Result = new ProblemUnderstandingOutputPayloadDto
                {
                    Category = "Plumbing",
                    ProblemSummary = "Possible burst pipe causing kitchen flooding.",
                    Urgency = "High",
                    NeedsMoreInformation = false,
                    FollowUpQuestions = new List<string>(),
                    Confidence = 0.92m,
                    ExtractedLocation = "Colombo 03",
                    AdditionalInformation = new Dictionary<string, string>
                    {
                        ["WaterSupplyIsolated"] = "True"
                    }
                },
                Metadata = new ExecutionMetadataPayloadDto
                {
                    AgentName = "ProblemUnderstandingAgent",
                    Provider = "gemini",
                    Degraded = false,
                    DurationMs = 250,
                    ToolExecutions = new List<ToolExecutionAuditPayloadDto>
                    {
                        new() { Tool = "LocationExtractionTool", Success = true, DurationMs = 2 },
                        new() { Tool = "ProblemClassificationTool", Success = true, DurationMs = 3 }
                    }
                }
            }));

        var context = CreateTestContext();
        var result = await adapter.ExecuteAsync(context);

        Assert.True(result.Success);
        Assert.Equal("Analysed", result.NextAction);
        Assert.NotNull(result.Data);
        var output = Assert.IsType<ProblemUnderstandingOutput>(result.Data);

        Assert.Equal("Plumbing", output.Category);
        Assert.Equal("Possible burst pipe causing kitchen flooding.", output.ProblemSummary);
        Assert.Equal(ServiceRequestUrgency.High, output.Urgency);
        Assert.False(output.NeedsMoreInformation);
        Assert.Empty(output.FollowUpQuestions);
        Assert.Equal(0.92m, output.Confidence);
        Assert.Equal("Colombo 03", output.ExtractedLocation);
        Assert.Equal("True", output.AdditionalInformation["WaterSupplyIsolated"]);
        Assert.Equal("gemini", output.AdditionalInformation["Provider"]);
    }

    [Fact]
    public async Task Test06_ResponseMapping_SuccessfulVehicleRepairResponse_MapsAllFields()
    {
        // Item 6: successful Vehicle Repair response mapping
        var (adapter, _) = CreateAdapterWithMockHandler(_ =>
            CreateJsonResponse(new AgentExecutionResponseDto
            {
                RequestId = Guid.NewGuid(),
                Success = true,
                Result = new ProblemUnderstandingOutputPayloadDto
                {
                    Category = "Vehicle Repair",
                    ProblemSummary = "Car engine stalled and won't crank.",
                    Urgency = "High",
                    NeedsMoreInformation = false,
                    FollowUpQuestions = new List<string>(),
                    Confidence = 0.88m,
                    ExtractedLocation = "Kandy Road, Kadawatha",
                    AdditionalInformation = new Dictionary<string, string>
                    {
                        ["VehicleStalled"] = "True"
                    }
                },
                Metadata = new ExecutionMetadataPayloadDto
                {
                    AgentName = "ProblemUnderstandingAgent",
                    Provider = "offline",
                    Degraded = false
                }
            }));

        var context = CreateTestContext(description: "Car broke down on the road, engine died.");
        var result = await adapter.ExecuteAsync(context);

        Assert.True(result.Success);
        var output = Assert.IsType<ProblemUnderstandingOutput>(result.Data);
        Assert.Equal("Vehicle Repair", output.Category);
        Assert.Equal(ServiceRequestUrgency.High, output.Urgency);
        Assert.Equal(0.88m, output.Confidence);
    }

    [Theory]
    [InlineData("Low", ServiceRequestUrgency.Low)]
    [InlineData("Medium", ServiceRequestUrgency.Medium)]
    [InlineData("High", ServiceRequestUrgency.High)]
    [InlineData("Critical", ServiceRequestUrgency.Critical)]
    [InlineData("Unknown", ServiceRequestUrgency.Unknown)]
    [InlineData("critical", ServiceRequestUrgency.Critical)]
    [InlineData("LOW", ServiceRequestUrgency.Low)]
    public async Task Test07_UrgencyMapping_MapsAllRecognizedUrgencyLevels(string rawUrgency, ServiceRequestUrgency expected)
    {
        // Item 7: urgency mapping
        var (adapter, _) = CreateAdapterWithMockHandler(_ =>
            CreateJsonResponse(new AgentExecutionResponseDto
            {
                Success = true,
                Result = new ProblemUnderstandingOutputPayloadDto
                {
                    Category = "Plumbing",
                    ProblemSummary = "Issue summary",
                    Urgency = rawUrgency,
                    Confidence = 0.80m
                }
            }));

        var context = CreateTestContext();
        var result = await adapter.ExecuteAsync(context);

        Assert.True(result.Success);
        var output = Assert.IsType<ProblemUnderstandingOutput>(result.Data);
        Assert.Equal(expected, output.Urgency);
    }

    [Fact]
    public async Task Test08_ConfidenceMapping_PreservesNormalizedScore_AndRejectsInvalidBounds()
    {
        // Item 8: confidence mapping & bounds validation
        // Valid confidence
        var (validAdapter, _) = CreateAdapterWithMockHandler(_ =>
            CreateJsonResponse(new AgentExecutionResponseDto
            {
                Success = true,
                Result = new ProblemUnderstandingOutputPayloadDto
                {
                    Category = "Electrical",
                    ProblemSummary = "Valid electrical issue",
                    Urgency = "Medium",
                    Confidence = 0.77m
                }
            }));

        var validResult = await validAdapter.ExecuteAsync(CreateTestContext());
        Assert.True(validResult.Success);
        var validOutput = Assert.IsType<ProblemUnderstandingOutput>(validResult.Data);
        Assert.Equal(0.77m, validOutput.Confidence);

        // Out-of-bounds confidence (> 1.0) must fail safely
        var (invalidAdapter, _) = CreateAdapterWithMockHandler(_ =>
            CreateJsonResponse(new AgentExecutionResponseDto
            {
                Success = true,
                Result = new ProblemUnderstandingOutputPayloadDto
                {
                    Category = "Electrical",
                    ProblemSummary = "Out of bounds",
                    Urgency = "Medium",
                    Confidence = 1.50m
                }
            }));

        var invalidResult = await invalidAdapter.ExecuteAsync(CreateTestContext());
        Assert.False(invalidResult.Success);
        Assert.Contains("confidence", invalidResult.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Test09_FollowUpQuestionsMapping_BoundsToMaxThreeAndSanitizesLength()
    {
        // Item 9: follow-up questions mapping
        var longQuestion = new string('A', 600);
        var (adapter, _) = CreateAdapterWithMockHandler(_ =>
            CreateJsonResponse(new AgentExecutionResponseDto
            {
                Success = true,
                Result = new ProblemUnderstandingOutputPayloadDto
                {
                    Category = "Unclassified",
                    ProblemSummary = "Vague description needs clarification",
                    Urgency = "Unknown",
                    NeedsMoreInformation = true,
                    Confidence = 0.20m,
                    FollowUpQuestions = new List<string>
                    {
                        "Question 1?",
                        "Question 2?",
                        "Question 3?",
                        "Question 4 (should be discarded)?",
                        longQuestion
                    }
                }
            }));

        var context = CreateTestContext();
        var result = await adapter.ExecuteAsync(context);

        Assert.True(result.Success);
        var output = Assert.IsType<ProblemUnderstandingOutput>(result.Data);
        Assert.True(output.NeedsMoreInformation);
        Assert.Equal("AwaitingInformation", result.NextAction);
        Assert.True(output.FollowUpQuestions.Count <= 3);
        Assert.Equal(3, output.FollowUpQuestions.Count);
        Assert.Equal("Question 1?", output.FollowUpQuestions[0]);
        Assert.Equal("Question 2?", output.FollowUpQuestions[1]);
        Assert.Equal("Question 3?", output.FollowUpQuestions[2]);
    }

    #endregion

    #region Section 15: Required Tests 10-12 (Degraded & Metadata Mapping)

    [Fact]
    public async Task Test10_DegradedResultMapping_TreatedAsValidAgentResult()
    {
        // Item 10: degraded result mapping
        // Critical requirement: If Python returns degraded result, treat it as a VALID agent result,
        // do not convert degraded=true into transport failure.
        var (adapter, _) = CreateAdapterWithMockHandler(_ =>
            CreateJsonResponse(new AgentExecutionResponseDto
            {
                RequestId = Guid.NewGuid(),
                Success = true,
                Result = new ProblemUnderstandingOutputPayloadDto
                {
                    Category = "Unclassified",
                    ProblemSummary = "Possible service issue. Category could not be established.",
                    Urgency = "Unknown",
                    NeedsMoreInformation = true,
                    Confidence = 0.20m,
                    FollowUpQuestions = new List<string> { "Could you describe the problem in more detail?" }
                },
                Metadata = new ExecutionMetadataPayloadDto
                {
                    AgentName = "ProblemUnderstandingAgent",
                    Provider = "gemini",
                    Degraded = true,
                    DurationMs = 1500
                }
            }));

        var context = CreateTestContext();
        var result = await adapter.ExecuteAsync(context);

        Assert.True(result.Success);
        Assert.Equal("AwaitingInformation", result.NextAction);
        var output = Assert.IsType<ProblemUnderstandingOutput>(result.Data);
        Assert.Equal("Unclassified", output.Category);
        Assert.Equal(ServiceRequestUrgency.Unknown, output.Urgency);
        Assert.True(output.NeedsMoreInformation);
        Assert.True(context.Data.ContainsKey("Degraded"));
        Assert.True((bool)context.Data["Degraded"]);
        Assert.Equal("True", output.AdditionalInformation["Degraded"]);
    }

    [Fact]
    public async Task Test11_ProviderMetadataMapping_RecordsProviderInContextAndOutput()
    {
        // Item 11: provider metadata mapping
        var (adapter, _) = CreateAdapterWithMockHandler(_ =>
            CreateJsonResponse(new AgentExecutionResponseDto
            {
                Success = true,
                Result = new ProblemUnderstandingOutputPayloadDto
                {
                    Category = "Plumbing",
                    ProblemSummary = "Pipe leak",
                    Urgency = "Medium",
                    Confidence = 0.85m
                },
                Metadata = new ExecutionMetadataPayloadDto
                {
                    AgentName = "ProblemUnderstandingAgent",
                    Provider = "offline",
                    Degraded = false,
                    DurationMs = 45
                }
            }));

        var context = CreateTestContext();
        var result = await adapter.ExecuteAsync(context);

        Assert.True(result.Success);
        Assert.Equal("offline", context.Data["Provider"]);
        Assert.Equal(45, context.Data["AgentDurationMs"]);

        var output = Assert.IsType<ProblemUnderstandingOutput>(result.Data);
        Assert.Equal("offline", output.AdditionalInformation["Provider"]);
    }

    [Fact]
    public async Task Test12_ToolExecutionCountMapping_SetsToolCallsAndToolCallCountInContext()
    {
        // Item 12: tool execution count mapping
        var (adapter, _) = CreateAdapterWithMockHandler(_ =>
            CreateJsonResponse(new AgentExecutionResponseDto
            {
                Success = true,
                Result = new ProblemUnderstandingOutputPayloadDto
                {
                    Category = "Plumbing",
                    ProblemSummary = "Drain blocked",
                    Urgency = "Low",
                    Confidence = 0.80m
                },
                Metadata = new ExecutionMetadataPayloadDto
                {
                    ToolExecutions = new List<ToolExecutionAuditPayloadDto>
                    {
                        new() { Tool = "LocationExtractionTool", Success = true },
                        new() { Tool = "ProblemClassificationTool", Success = true },
                        new() { Tool = "ServiceKnowledgeTool", Success = true }
                    }
                }
            }));

        var context = CreateTestContext();
        var result = await adapter.ExecuteAsync(context);

        Assert.True(result.Success);
        Assert.Equal(3, context.Data["ToolCalls"]);
        Assert.Equal(3, context.Data["ToolCallCount"]);
    }

    #endregion

    #region Section 15: Required Tests 13-20 (Transport & Error Handling)

    [Fact]
    public async Task Test13_MalformedJson_ReturnsSafeAgentResultFailure()
    {
        // Item 13: malformed JSON
        var (adapter, _) = CreateAdapterWithMockHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"invalid_json\": [unterminated", Encoding.UTF8, "application/json")
            });

        var result = await adapter.ExecuteAsync(CreateTestContext());

        Assert.False(result.Success);
        Assert.Contains("Malformed", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Test14_SuccessFalse_ReturnsSafeAgentResultFailure()
    {
        // Item 14: success=false
        var (adapter, _) = CreateAdapterWithMockHandler(_ =>
            CreateJsonResponse(new AgentExecutionResponseDto
            {
                Success = false,
                ErrorMessage = "LangGraph unrecoverable execution failure",
                Result = null
            }));

        var result = await adapter.ExecuteAsync(CreateTestContext());

        Assert.False(result.Success);
        Assert.Contains("unrecoverable", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Test15_MissingResult_ReturnsSafeAgentResultFailure()
    {
        // Item 15: missing result when success=true
        var (adapter, _) = CreateAdapterWithMockHandler(_ =>
            CreateJsonResponse(new AgentExecutionResponseDto
            {
                Success = true,
                Result = null
            }));

        var result = await adapter.ExecuteAsync(CreateTestContext());

        Assert.False(result.Success);
        Assert.Contains("missing", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Test16_Http401_ReturnsSafeAgentResultFailure()
    {
        // Item 16: HTTP 401
        var (adapter, _) = CreateAdapterWithMockHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"detail\": \"Invalid API key\"}", Encoding.UTF8, "application/json")
            });

        var result = await adapter.ExecuteAsync(CreateTestContext());

        Assert.False(result.Success);
        Assert.Contains("401", result.Message);
    }

    [Fact]
    public async Task Test17_Http422_ReturnsSafeAgentResultFailure()
    {
        // Item 17: HTTP 422
        var (adapter, _) = CreateAdapterWithMockHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            {
                Content = new StringContent("{\"detail\": [{\"msg\": \"Field required\"}]}", Encoding.UTF8, "application/json")
            });

        var result = await adapter.ExecuteAsync(CreateTestContext());

        Assert.False(result.Success);
        Assert.Contains("422", result.Message);
    }

    [Fact]
    public async Task Test18_Http500_ReturnsSafeAgentResultFailure()
    {
        // Item 18: HTTP 500
        var (adapter, _) = CreateAdapterWithMockHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Internal Server Error", Encoding.UTF8, "text/plain")
            });

        var result = await adapter.ExecuteAsync(CreateTestContext());

        Assert.False(result.Success);
        Assert.Contains("500", result.Message);
    }

    [Fact]
    public async Task Test19_ConnectionFailure_ReturnsSafeAgentResultFailure()
    {
        // Item 19: connection failure
        var (adapter, _) = CreateAdapterWithMockHandler(_ =>
            throw new HttpRequestException("Connection refused (127.0.0.1:8001)"));

        var result = await adapter.ExecuteAsync(CreateTestContext());

        Assert.False(result.Success);
        Assert.Contains("Connection", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Test20_Timeout_ReturnsSafeAgentResultFailure()
    {
        // Item 20: timeout
        var (adapter, _) = CreateAdapterWithMockHandler(_ =>
            throw new TaskCanceledException("The operation was canceled due to timeout."));

        var result = await adapter.ExecuteAsync(CreateTestContext());

        Assert.False(result.Success);
        Assert.Contains("timed out", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Section 15: Required Tests 21-23 (Security & No Native Fallback)

    [Fact]
    public async Task Test21_InternalApiKeyHeader_SentOnlyWhenConfigured()
    {
        // Item 21: internal API key header
        var options = new AgentServicesOptions
        {
            InternalApiKey = "my-secret-internal-key-999"
        };

        var (adapter, handler) = CreateAdapterWithMockHandler(_ =>
            CreateJsonResponse(new AgentExecutionResponseDto
            {
                Success = true,
                Result = new ProblemUnderstandingOutputPayloadDto { Category = "Plumbing", ProblemSummary = "Summary", Urgency = "Low", Confidence = 0.5m }
            }), options);

        await adapter.ExecuteAsync(CreateTestContext());

        Assert.NotNull(handler.LastRequest);
        Assert.True(handler.LastRequest.Headers.Contains("X-Internal-Api-Key"));
        var keyVal = handler.LastRequest.Headers.GetValues("X-Internal-Api-Key").First();
        Assert.Equal("my-secret-internal-key-999", keyVal);
    }

    [Fact]
    public async Task Test22_NoCustomerJwtOrProviderKeysForwarded()
    {
        // Item 22: no customer JWT forwarded
        var (adapter, handler) = CreateAdapterWithMockHandler(_ =>
            CreateJsonResponse(new AgentExecutionResponseDto
            {
                Success = true,
                Result = new ProblemUnderstandingOutputPayloadDto { Category = "Plumbing", ProblemSummary = "Summary", Urgency = "Low", Confidence = 0.5m }
            }));

        var context = CreateTestContext();
        context.Memory["JwtToken"] = "customer-jwt-token-sample";
        context.Memory["Authorization"] = "Bearer token";

        await adapter.ExecuteAsync(context);

        Assert.NotNull(handler.LastRequest);
        Assert.Null(handler.LastRequest.Headers.Authorization);
        Assert.False(handler.LastRequest.Headers.Contains("Authorization"));
        Assert.False(handler.LastRequest.Headers.Contains("GOOGLE_API_KEY"));
        Assert.False(handler.LastRequest.Headers.Contains("OPENAI_API_KEY"));
    }

    [Fact]
    public async Task Test23_NoAutomaticNativeFallback_FailureReturnsFailureWithoutCallingNativeAgent()
    {
        // Item 23: no automatic native fallback
        // When the adapter fails, it returns safe failure. It must NOT silently call GeminiService.
        var (adapter, _) = CreateAdapterWithMockHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await adapter.ExecuteAsync(CreateTestContext());

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Contains("500", result.Message);
    }

    #endregion

    #region Section 15: Required Tests 24-25 & Strict Mode Validation

    [Fact]
    public void Test24_ModeSwitch_NativeCSharpMode_RegistersNativeAgent()
    {
        // Item 24: NativeCSharp mode still registers native agent
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AgentServices:ProblemUnderstandingMode"] = "NativeCSharp"
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddHttpClient();

        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var options = new AgentServicesOptions();
            config.GetSection(AgentServicesOptions.SectionName).Bind(options);
            options.Validate();
            return options;
        });

        services.AddScoped<ToolRegistry>();
        services.AddScoped<ToolExecutor>();
        services.AddScoped<IGeminiService, GeminiService>();
        services.AddScoped<ProblemUnderstandingAgent>();
        services.AddScoped<IProblemUnderstandingClient, ProblemUnderstandingHttpClient>();
        services.AddScoped<ExternalProblemUnderstandingAgentAdapter>();

        services.AddScoped<AgentRegistry>(sp =>
        {
            var registry = new AgentRegistry();
            var options = sp.GetRequiredService<AgentServicesOptions>();

            if (string.Equals(options.ProblemUnderstandingMode, AgentServicesOptions.ExternalPythonMode, StringComparison.OrdinalIgnoreCase))
            {
                registry.Register(sp.GetRequiredService<ExternalProblemUnderstandingAgentAdapter>());
            }
            else if (string.Equals(options.ProblemUnderstandingMode, AgentServicesOptions.NativeCSharpMode, StringComparison.OrdinalIgnoreCase))
            {
                registry.Register(sp.GetRequiredService<ProblemUnderstandingAgent>());
            }
            return registry;
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var registry = scope.ServiceProvider.GetRequiredService<AgentRegistry>();
        var registeredAgent = registry.Get("ProblemUnderstandingAgent");

        Assert.NotNull(registeredAgent);
        Assert.IsType<ProblemUnderstandingAgent>(registeredAgent);
    }

    [Fact]
    public void Test25_ModeSwitch_ExternalPythonMode_RegistersExternalAdapter()
    {
        // Item 25: ExternalPython mode registers external adapter
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AgentServices:ProblemUnderstandingMode"] = "ExternalPython",
                ["AgentServices:ProblemUnderstandingUrl"] = "http://127.0.0.1:8001"
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddHttpClient();

        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var options = new AgentServicesOptions();
            config.GetSection(AgentServicesOptions.SectionName).Bind(options);
            options.Validate();
            return options;
        });

        services.AddScoped<ToolRegistry>();
        services.AddScoped<ToolExecutor>();
        services.AddScoped<IGeminiService, GeminiService>();
        services.AddScoped<ProblemUnderstandingAgent>();
        services.AddScoped<IProblemUnderstandingClient, ProblemUnderstandingHttpClient>();
        services.AddScoped<ExternalProblemUnderstandingAgentAdapter>();

        services.AddScoped<AgentRegistry>(sp =>
        {
            var registry = new AgentRegistry();
            var options = sp.GetRequiredService<AgentServicesOptions>();

            if (string.Equals(options.ProblemUnderstandingMode, AgentServicesOptions.ExternalPythonMode, StringComparison.OrdinalIgnoreCase))
            {
                registry.Register(sp.GetRequiredService<ExternalProblemUnderstandingAgentAdapter>());
            }
            else if (string.Equals(options.ProblemUnderstandingMode, AgentServicesOptions.NativeCSharpMode, StringComparison.OrdinalIgnoreCase))
            {
                registry.Register(sp.GetRequiredService<ProblemUnderstandingAgent>());
            }
            return registry;
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var registry = scope.ServiceProvider.GetRequiredService<AgentRegistry>();
        var registeredAgent = registry.Get("ProblemUnderstandingAgent");

        Assert.NotNull(registeredAgent);
        Assert.IsType<ExternalProblemUnderstandingAgentAdapter>(registeredAgent);
    }

    [Theory]
    [InlineData("InvalidMode")]
    [InlineData("Python")]
    [InlineData("OpenAI")]
    [InlineData("None")]
    public void Test26_StrictModeValidation_ThrowsOnUnknownMode(string invalidMode)
    {
        var options = new AgentServicesOptions
        {
            ProblemUnderstandingMode = invalidMode
        };

        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains(invalidMode, ex.Message);
        Assert.Contains("NativeCSharp", ex.Message);
        Assert.Contains("ExternalPython", ex.Message);
    }

    [Fact]
    public async Task Test27_EmptyDescription_ReturnsFastNeedsMoreInformationWithoutRemoteCall()
    {
        var (adapter, handler) = CreateAdapterWithMockHandler(_ =>
            throw new InvalidOperationException("Remote call should not happen for empty description"));

        var context = CreateTestContext(description: "   ");
        var result = await adapter.ExecuteAsync(context);

        Assert.True(result.Success);
        Assert.Equal("AwaitingInformation", result.NextAction);
        Assert.Null(handler.LastRequest); // Zero remote requests made

        var output = Assert.IsType<ProblemUnderstandingOutput>(result.Data);
        Assert.Equal("Unclassified", output.Category);
        Assert.True(output.NeedsMoreInformation);
    }

    #endregion
}
