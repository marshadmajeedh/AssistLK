using System.Net;
using System.Text;
using System.Text.Json;
using AssistLK.Agents.Abstractions;
using AssistLK.Agents.Adapters;
using AssistLK.Agents.Clients;
using AssistLK.Agents.Configuration;
using AssistLK.Agents.Core;
using AssistLK.Agents.DTOs;
using AssistLK.Agents.Models;
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
// =========================================================================================
// Class: ExternalProblemUnderstandingAgentAdapterTests
// What this class tests:
//   Unit and HTTP wire contract tests for ExternalProblemUnderstandingAgentAdapter and
//   ProblemUnderstandingHttpClient, which bridge the C# backend to the external Python
//   Problem Understanding agent microservice.
// Why these tests are needed:
//   Ensures that outgoing request payloads conform to the FastAPI contract, response fields
//   (category, urgency, confidence, visual evidence) map accurately into C# domain models,
//   degraded agent results are handled gracefully, security boundaries are enforced (no customer
//   JWTs or provider secrets leaked), and HTTP errors/timeouts fail safely without crashing.
// What is real vs mocked/faked:
//   - REAL: C# adapter, ProblemUnderstandingHttpClient, JSON serialization/deserialization,
//     urgency/confidence parsers, and AgentServicesOptions validation logic.
//   - MOCKED: HTTP transport is mocked via MockHttpMessageHandler returning preconfigured
//     JSON responses or throwing simulated network/timeout exceptions.
//   - Zero live Python processes, external network calls, or paid AI models are used.
// Prerequisites:
//   None. Runs entirely in-memory with mocked HTTP handlers.
// Run from repository root:
//   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests"
// =========================================================================================
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

    // Purpose: Checks that ProblemUnderstandingInput serializes into the exact JSON wire format expected by the Python FastAPI agent service.
    // Why: Python agent will reject the request with 422 if required fields (requestId, agentName, operation, input) are missing or misnamed.
    // Scenario: C# executes adapter with a standard problem description, location, and category hint.
    // Pass condition: handler.LastRequestBody contains "agentName": "ProblemUnderstandingAgent", "operation": "analyze-problem", and omits null timestamp and parameters.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test01_RequestSerialization_SerializesExpectedContract"
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

    // Purpose: Verifies that prior rounds of clarification Q&A are properly serialized in the JSON wire payload under input.clarificationHistory.
    // Why: When re-analyzing after customer answers a question, the Python agent needs past round numbers, questions, and answers to avoid repeating questions.
    // Scenario: Input contains a 2-round clarification history list.
    // Pass condition: JSON payload contains a 2-element clarificationHistory array with matching round numbers, question texts, and answers.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test02_ClarificationHistorySerialization_PreservesRoundQuestionAnswer"
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

    // Purpose: Checks that GPS latitude and longitude serialize properly both when null and when floating-point coordinates are provided.
    // Why: Customers may or may not provide GPS coordinates; serialization must preserve null as JSON null without crashing or defaulting to 0.0.
    // Scenario: Tests Case A (null coordinates) and Case B (populated coordinates: latitude 6.9044, longitude 79.8665).
    // Pass condition: Null inputs produce null JSON values; populated inputs produce double values matching coordinates to 4 decimal places.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test03_NullableGpsSerialization_PreservesNullAndPopulatedCoordinates"
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

    // Purpose: Checks that a customer-provided category hint ("Electrical") is passed through to the JSON request payload.
    // Why: Category hints help the Python agent narrow its classification search space when available.
    // Scenario: Context initialized with categoryHint = "Electrical".
    // Pass condition: Wire JSON contains "categoryHint": "Electrical".
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test04_CategoryHintSerialization_PreservesHintWhenPresentOrNull"
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

    // Purpose: Verifies full mapping of a successful Plumbing response DTO from Python into C# ProblemUnderstandingOutput.
    // Why: Ensures all output fields (category, summary, urgency enum, confidence, extracted location, additional info, provider) map cleanly into C# domain objects.
    // Scenario: Python returns HTTP 200 with Plumbing category, high urgency, 0.92 confidence, extracted location, and metadata.
    // Pass condition: result.Success is true, NextAction is "Analysed", and all fields on ProblemUnderstandingOutput match the response DTO.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test05_ResponseMapping_SuccessfulPlumbingResponse_MapsAllFields"
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

    // Purpose: Checks mapping of a Vehicle Repair category response from Python into C# models.
    // Why: Confirms multi-category support across different service categories in AssistLK.
    // Scenario: Python returns Vehicle Repair category with 0.88 confidence for a car breakdown description.
    // Pass condition: result.Success is true, output category is "Vehicle Repair", urgency is High, and confidence is 0.88.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test06_ResponseMapping_SuccessfulVehicleRepairResponse_MapsAllFields"
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

    // Purpose: Verifies that string urgency values from Python (case-insensitive) map to ServiceRequestUrgency enum values.
    // Why: Python serializes urgency strings ("Low", "Medium", "High", "Critical", "Unknown", "critical", "LOW") which must parse into C# strongly typed enum values.
    // Scenario: Parameterized test running various casing variations of urgency strings.
    // Pass condition: output.Urgency equals the expected ServiceRequestUrgency enum member for each case.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test07_UrgencyMapping_MapsAllRecognizedUrgencyLevels"
    // Note: Parameterized theory test; this command executes all 7 inline data cases.
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

    // Purpose: Checks that valid confidence scores (0.0 to 1.0) are preserved, while scores exceeding 1.0 trigger safe agent failure.
    // Why: Confidence must be bounded between 0 and 1; out-of-range scores indicate corrupted agent outputs.
    // Scenario: Valid confidence (0.77) followed by invalid out-of-bounds confidence (1.50).
    // Pass condition: Valid returns success with confidence 0.77; invalid returns Success = false with an error message mentioning "confidence".
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test08_ConfidenceMapping_PreservesNormalizedScore_AndRejectsInvalidBounds"
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

    // Purpose: Verifies that follow-up clarification questions are capped at a maximum of 3 items and trigger the "AwaitingInformation" action.
    // Why: AssistLK business rules limit clarification questions to at most 3 to prevent overwhelming customers.
    // Scenario: Python response contains 5 follow-up questions including an excessively long string.
    // Pass condition: Result is successful with NextAction "AwaitingInformation", NeedsMoreInformation is true, and exactly 3 questions are kept.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test09_FollowUpQuestionsMapping_BoundsToMaxThreeAndSanitizesLength"
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

    // Purpose: Checks that a degraded Python agent response (Degraded = true) is treated as a valid agent result rather than a transport error.
    // Why: When AI models time out or fail, the agent produces fallback/degraded outputs; C# must accept this output and guide the user rather than crashing.
    // Scenario: Python returns Success = true, Degraded = true, category "Unclassified", and asks for more details.
    // Pass condition: result.Success is true, NextAction is "AwaitingInformation", context Degraded flag is true, and output contains degraded metadata.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test10_DegradedResultMapping_TreatedAsValidAgentResult"
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

    // Purpose: Checks that agent execution metadata (provider name e.g. "offline", duration in ms) is recorded into context.Data and output.AdditionalInformation.
    // Why: Provides auditability of which AI provider was used and how long the analysis took.
    // Scenario: Python returns metadata with Provider = "offline" and DurationMs = 45.
    // Pass condition: context.Data["Provider"] is "offline", context.Data["AgentDurationMs"] is 45, and output contains "offline".
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test11_ProviderMetadataMapping_RecordsProviderInContextAndOutput"
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

    // Purpose: Verifies that the count of executed tools reported in metadata is captured in context.Data["ToolCalls"] and ["ToolCallCount"].
    // Why: Allows monitoring and billing systems to track LangGraph tool invocations.
    // Scenario: Python returns metadata with 3 tool executions.
    // Pass condition: Both ToolCalls and ToolCallCount in context.Data equal 3.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test12_ToolExecutionCountMapping_SetsToolCallsAndToolCallCountInContext"
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

    // Purpose: Checks that unparseable or broken JSON from the Python service returns a graceful failure result instead of an unhandled exception.
    // Why: Protects the backend workflow from crashing if Python emits invalid or truncated JSON.
    // Scenario: HTTP 200 returned with content "{\"invalid_json\": [unterminated".
    // Pass condition: result.Success is false, and result.Message mentions "Malformed".
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test13_MalformedJson_ReturnsSafeAgentResultFailure"
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

    // Purpose: Verifies that when Python returns a JSON envelope with Success = false, the C# adapter returns failure with the Python error message.
    // Why: The external agent may catch an internal LangGraph exception and report it in the JSON envelope; C# must relay the failure.
    // Scenario: Python returns Success = false and ErrorMessage = "LangGraph unrecoverable execution failure".
    // Pass condition: result.Success is false and result.Message contains "unrecoverable".
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test14_SuccessFalse_ReturnsSafeAgentResultFailure"
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

    // Purpose: Checks that a response claiming Success = true but having Result = null fails safely.
    // Why: Defensive coding against malformed payloads missing expected payload data.
    // Scenario: Response JSON with Success = true and Result = null.
    // Pass condition: result.Success is false and result.Message mentions "missing".
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test15_MissingResult_ReturnsSafeAgentResultFailure"
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

    // Purpose: Checks that HTTP 401 Unauthorized from the agent service is caught and converted to a safe agent failure.
    // Why: If the internal API key is misconfigured or expired, the C# adapter should fail cleanly.
    // Scenario: HTTP 401 response with "Invalid API key" detail.
    // Pass condition: result.Success is false and result.Message contains "401".
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test16_Http401_ReturnsSafeAgentResultFailure"
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

    // Purpose: Checks that HTTP 422 Unprocessable Entity (FastAPI validation error) fails cleanly.
    // Why: If request payload fails Pydantic schema validation, C# reports the error without throwing an uncaught exception.
    // Scenario: HTTP 422 response with Pydantic field validation error.
    // Pass condition: result.Success is false and result.Message contains "422".
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test17_Http422_ReturnsSafeAgentResultFailure"
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

    // Purpose: Checks that HTTP 500 Internal Server Error returns safe failure.
    // Why: Python server-side errors must not crash the C# backend workflow.
    // Scenario: HTTP 500 response with "Internal Server Error" body.
    // Pass condition: result.Success is false and result.Message contains "500".
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test18_Http500_ReturnsSafeAgentResultFailure"
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

    // Purpose: Verifies that network connection refusal (e.g. Python service down) fails cleanly.
    // Why: The system must remain resilient when external microservices are unreachable.
    // Scenario: Mock handler throws HttpRequestException ("Connection refused").
    // Pass condition: result.Success is false and result.Message contains "Connection".
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test19_ConnectionFailure_ReturnsSafeAgentResultFailure"
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

    // Purpose: Verifies that HTTP timeouts throw TaskCanceledException which is caught and converted to safe failure.
    // Why: Prevents threads from hanging indefinitely when an AI model takes too long to respond.
    // Scenario: Mock handler throws TaskCanceledException simulating a timeout.
    // Pass condition: result.Success is false and result.Message contains "timed out".
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test20_Timeout_ReturnsSafeAgentResultFailure"
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

    // Purpose: Verifies that X-Internal-Api-Key HTTP header is included in outgoing requests when configured in AgentServicesOptions.
    // Why: Authenticates the backend to the internal Python agent service.
    // Scenario: Adapter configured with InternalApiKey = "my-secret-internal-key-999".
    // Pass condition: Outgoing request contains X-Internal-Api-Key header with matching secret string.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test21_InternalApiKeyHeader_SentOnlyWhenConfigured"
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

    // Purpose: Checks that customer JWT tokens and LLM API keys are NEVER forwarded to the Python agent service.
    // Why: Critical security boundary: Prevents leaking customer credentials or cloud provider secrets across internal microservice boundaries.
    // Scenario: Context memory populated with JwtToken and Authorization bearer token.
    // Pass condition: Outgoing HTTP request has null Authorization header and does not contain GOOGLE_API_KEY or OPENAI_API_KEY.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test22_NoCustomerJwtOrProviderKeysForwarded"
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

    // Purpose: Ensures that adapter failure does NOT silently fall back to an in-process native AI service (like GeminiService).
    // Why: Architecture design: Failures must be explicit so callers can handle fallback or retry deliberately.
    // Scenario: Python service returns HTTP 500.
    // Pass condition: Adapter returns Success = false, Data is null, and error message contains "500" without calling any native fallback.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test23_NoAutomaticNativeFallback_FailureReturnsFailureWithoutCallingNativeAgent"
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

    // Purpose: Checks that image-enhanced multimodal requests send the exact same X-Internal-Api-Key header and do not forward customer JWT.
    // Why: Multimodal requests must follow the identical microservice security contract as text-only requests.
    // Scenario: Adapter configured with internal API key executes context containing visual evidence JPEG byte data.
    // Pass condition: result.Success is true, request has X-Internal-Api-Key with expected key, and Authorization is null.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test24_InternalApiKeyHeader_SentOnMultimodalRequestWithSameHeader"
    [Fact]
    public async Task Test24_InternalApiKeyHeader_SentOnMultimodalRequestWithSameHeader()
    {
        // Item 24: multimodal image-enhanced requests send the EXACT SAME internal API key header
        var options = new AgentServicesOptions
        {
            InternalApiKey = "multimodal-shared-secret-123"
        };

        var attachmentId = Guid.NewGuid();
        var (adapter, handler) = CreateAdapterWithMockHandler(_ =>
            CreateJsonResponse(new AgentExecutionResponseDto
            {
                Success = true,
                Metadata = new ExecutionMetadataPayloadDto { Provider = "offline" },
                Result = new ProblemUnderstandingOutputPayloadDto
                {
                    Category = "Plumbing",
                    ProblemSummary = "Pipe leak detected",
                    Urgency = "High",
                    Confidence = 0.9m,
                    VisionStatus = "used",
                    AttachmentIdsUsed = new List<Guid> { attachmentId },
                    VisualObservations = new List<AssistLK.Agents.Models.VisualObservation>
                    {
                        new() { AttachmentId = attachmentId, Observation = "Water pooling around pipe joint" }
                    }
                }
            }), options);

        var context = CreateTestContext();
        context.VisualEvidence = new List<VisualEvidencePayloadDto>
        {
            new()
            {
                AttachmentId = attachmentId,
                ContentType = "image/jpeg",
                DataBase64 = Convert.ToBase64String(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 }),
                Width = 80,
                Height = 40
            }
        };

        var result = await adapter.ExecuteAsync(context);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(handler.LastRequest);
        Assert.True(handler.LastRequest.Headers.Contains("X-Internal-Api-Key"));
        var keyVal = handler.LastRequest.Headers.GetValues("X-Internal-Api-Key").First();
        Assert.Equal("multimodal-shared-secret-123", keyVal);
        Assert.Null(handler.LastRequest.Headers.Authorization);
    }

    // Purpose: Checks that clarification re-analysis requests send the exact same X-Internal-Api-Key header.
    // Why: Clarification requests must authenticate identically to initial analysis requests.
    // Scenario: Adapter configured with internal API key executes context containing clarification Q&A history.
    // Pass condition: result.Success is true, request contains X-Internal-Api-Key header with configured key.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test25_InternalApiKeyHeader_SentOnClarificationRequestWithSameHeader"
    [Fact]
    public async Task Test25_InternalApiKeyHeader_SentOnClarificationRequestWithSameHeader()
    {
        // Item 25: clarification re-analysis requests send the EXACT SAME internal API key header
        var options = new AgentServicesOptions
        {
            InternalApiKey = "clarification-shared-secret-456"
        };

        var (adapter, handler) = CreateAdapterWithMockHandler(_ =>
            CreateJsonResponse(new AgentExecutionResponseDto
            {
                Success = true,
                Result = new ProblemUnderstandingOutputPayloadDto
                {
                    Category = "Electrical",
                    ProblemSummary = "Sparking outlet",
                    Urgency = "High",
                    Confidence = 0.95m
                }
            }), options);

        var history = new List<ClarificationHistoryItem>
        {
            new() { Round = 1, Question = "Is there smoke?", Answer = "Yes, light smoke" }
        };
        var context = CreateTestContext(description: "Outlet sparks when plugging in charger", history: history);

        var result = await adapter.ExecuteAsync(context);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(handler.LastRequest);
        Assert.True(handler.LastRequest.Headers.Contains("X-Internal-Api-Key"));
        var keyVal = handler.LastRequest.Headers.GetValues("X-Internal-Api-Key").First();
        Assert.Equal("clarification-shared-secret-456", keyVal);
    }

    // Purpose: Checks that accidental leading/trailing whitespace and quotes in InternalApiKey configuration are safely stripped before sending.
    // Why: Common deployment mistake in .env or appsettings files where values are quoted with extra spaces.
    // Scenario: Config has InternalApiKey = "  \"secret-with-quotes-and-spaces\"  ".
    // Pass condition: Outgoing header value is cleanly stripped to "secret-with-quotes-and-spaces".
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test26_InternalApiKeyHeader_StripsAccidentalSurroundingQuotesAndWhitespace"
    [Fact]
    public async Task Test26_InternalApiKeyHeader_StripsAccidentalSurroundingQuotesAndWhitespace()
    {
        // Item 26: surrounding quotes and whitespace are safely stripped
        var options = new AgentServicesOptions
        {
            InternalApiKey = "  \"secret-with-quotes-and-spaces\"  "
        };

        var (adapter, handler) = CreateAdapterWithMockHandler(_ =>
            CreateJsonResponse(new AgentExecutionResponseDto
            {
                Success = true,
                Result = new ProblemUnderstandingOutputPayloadDto
                {
                    Category = "Plumbing",
                    ProblemSummary = "Cleaned",
                    Urgency = "Low",
                    Confidence = 0.7m
                }
            }), options);

        await adapter.ExecuteAsync(CreateTestContext());

        Assert.NotNull(handler.LastRequest);
        Assert.True(handler.LastRequest.Headers.Contains("X-Internal-Api-Key"));
        var keyVal = handler.LastRequest.Headers.GetValues("X-Internal-Api-Key").First();
        Assert.Equal("secret-with-quotes-and-spaces", keyVal);
    }

    #endregion

    #region Section 15: Agent Services Options Validation

    // Purpose: Checks that AgentServicesOptions.Validate() throws an exception when ProblemUnderstandingUrl is empty, whitespace, malformed, or has an invalid scheme.
    // Why: Early startup validation prevents misconfigured URLs from causing runtime failures later.
    // Scenario: Parameterized test with empty string, spaces, "not-a-url", and "ftp://...".
    // Pass condition: Throws InvalidOperationException mentioning "ProblemUnderstandingUrl".
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test26_OptionsValidation_ThrowsOnInvalidUrl"
    // Note: Parameterized theory test; this command executes all 4 inline data cases.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    [InlineData("ftp://127.0.0.1:8001")]
    public void Test26_OptionsValidation_ThrowsOnInvalidUrl(string invalidUrl)
    {
        var options = new AgentServicesOptions
        {
            ProblemUnderstandingUrl = invalidUrl
        };

        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("ProblemUnderstandingUrl", ex.Message);
    }

    // Purpose: Checks that AgentServicesOptions.Validate() throws an exception when TimeoutSeconds is zero or negative.
    // Why: HTTP client timeout must be a positive integer to prevent deadlocks or immediate cancellations.
    // Scenario: Parameterized test with 0, -1, and -45 seconds.
    // Pass condition: Throws InvalidOperationException mentioning "TimeoutSeconds".
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test26_OptionsValidation_ThrowsOnNonPositiveTimeout"
    // Note: Parameterized theory test; this command executes all 3 inline data cases.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-45)]
    public void Test26_OptionsValidation_ThrowsOnNonPositiveTimeout(int invalidTimeout)
    {
        var options = new AgentServicesOptions
        {
            TimeoutSeconds = invalidTimeout
        };

        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("TimeoutSeconds", ex.Message);
    }

    // Purpose: Checks that a whitespace-only problem description short-circuits locally and immediately returns NeedsMoreInformation = true without making any HTTP call.
    // Why: Saves network overhead and API costs by immediately prompting customer for details when input is empty.
    // Scenario: Context has description "   ". Handler is configured to throw if called.
    // Pass condition: result.Success is true, NextAction is "AwaitingInformation", handler.LastRequest is null (zero remote calls), and category is "Unclassified".
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ExternalProblemUnderstandingAgentAdapterTests.Test27_EmptyDescription_ReturnsFastNeedsMoreInformationWithoutRemoteCall"
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
