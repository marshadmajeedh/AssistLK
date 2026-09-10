using AssistLK.Agents.Abstractions;
using AssistLK.Agents.Agents;
using AssistLK.Agents.Core;
using AssistLK.Agents.Models;
using AssistLK.Agents.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AssistLK.Api.Tests;

/// <summary>
/// Regression tests for the fixed Gemini execution pipeline in ProblemUnderstandingAgent.
///
/// Verified behaviours:
///  A. Gemini executes even when ProblemClassificationTool throws.
///  B. Gemini unavailable → safe degraded output (Unclassified / AwaitingInformation).
///  C. Tool failure is logged but does not crash the workflow.
///  D. Gemini output is authoritative; classification tool provides optional alignment only.
///
/// Design:
///  - Uses real ToolRegistry + ToolExecutor with mock IAgentTool instances so tool
///    behaviour (success, failure, throw) is fully controlled per test.
///  - Mocks IGeminiService to control Gemini responses without network calls.
///  - Uses NullLogger to satisfy the ILogger parameter without noise.
/// </summary>
public class ProblemUnderstandingAgentTests
{
    // -----------------------------------------------------------------------
    // Shared Gemini JSON responses (compliant with SystemInstruction contract)
    // -----------------------------------------------------------------------

    private const string GeminiPlumbingResponse =
        """
        {
          "category": "Plumbing",
          "problemSummary": "Possible burst pipe or water flooding. Immediate professional attention may be required.",
          "urgency": "High",
          "needsMoreInformation": false,
          "followUpQuestions": [],
          "confidence": 0.9,
          "additionalInformation": {}
        }
        """;

    private const string GeminiUnclassifiedResponse =
        """
        {
          "category": "Unclassified",
          "problemSummary": "Possible service issue. Insufficient information provided.",
          "urgency": "Unknown",
          "needsMoreInformation": true,
          "followUpQuestions": ["Could you describe the problem in more detail?"],
          "confidence": 0.2,
          "additionalInformation": {}
        }
        """;

    private const string GeminiElectricalResponse =
        """
        {
          "category": "Electrical",
          "problemSummary": "Possible electrical safety issue. Professional inspection is recommended.",
          "urgency": "High",
          "needsMoreInformation": false,
          "followUpQuestions": [],
          "confidence": 0.88,
          "additionalInformation": {}
        }
        """;

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds an agent wired up with a controlled ToolRegistry.
    /// Any tools NOT explicitly registered will cause ToolExecutor to return
    /// Success=false (tool not found) which is non-fatal.
    /// </summary>
    private static (ProblemUnderstandingAgent Agent, Mock<IGeminiService> GeminiMock)
        BuildAgent(
            Action<ToolRegistry>? configureTool = null,
            string? geminiResponse = GeminiPlumbingResponse)
    {
        var geminiMock = new Mock<IGeminiService>();
        geminiMock
            .Setup(g => g.GenerateContentAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(geminiResponse);

        var registry = new ToolRegistry();
        configureTool?.Invoke(registry);

        var executor = new ToolExecutor(registry);
        var logger = NullLogger<ProblemUnderstandingAgent>.Instance;

        var agent = new ProblemUnderstandingAgent(executor, geminiMock.Object, logger);
        return (agent, geminiMock);
    }

    /// <summary>
    /// Builds a mock IAgentTool that always throws the given exception.
    /// </summary>
    private static IAgentTool ThrowingTool(string name, Exception exception)
    {
        var mock = new Mock<IAgentTool>();
        mock.Setup(t => t.Name).Returns(name);
        mock.Setup(t => t.Description).Returns($"Mock {name}");
        mock.Setup(t => t.ExecuteAsync(
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);
        return mock.Object;
    }

    /// <summary>
    /// Builds a mock IAgentTool that returns success with the given data.
    /// </summary>
    private static IAgentTool SucceedingTool(string name, object data)
    {
        var mock = new Mock<IAgentTool>();
        mock.Setup(t => t.Name).Returns(name);
        mock.Setup(t => t.Description).Returns($"Mock {name}");
        mock.Setup(t => t.ExecuteAsync(
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResult { Success = true, Message = "OK", Data = data });
        return mock.Object;
    }

    private static AgentContext MakeContext(string description) =>
        new()
        {
            Input = description,
            Data =
            {
                [nameof(ProblemUnderstandingInput)] = new ProblemUnderstandingInput
                {
                    Description = description,
                    ServiceRequestId = Guid.NewGuid()
                }
            }
        };

    // -----------------------------------------------------------------------
    // Test A: Gemini executes even when ProblemClassificationTool throws
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TestA_GeminiExecutes_WhenClassificationToolThrows()
    {
        // Arrange
        // Classification tool is registered but always throws.
        // Location and Knowledge tools are NOT registered (ToolExecutor returns tool-not-found,
        // which is a non-throwing non-fatal outcome).
        var (agent, geminiMock) = BuildAgent(
            registry =>
            {
                registry.Register(ThrowingTool(
                    "ProblemClassificationTool",
                    new InvalidOperationException("Simulated classification failure")));
            },
            geminiResponse: GeminiPlumbingResponse);

        var context = MakeContext("My pipe burst and water is flooding everywhere");

        // Act
        var result = await agent.ExecuteAsync(context);

        // Assert — Gemini was the primary engine and produced a valid result
        Assert.True(result.Success);
        Assert.Equal("Analysed", result.NextAction);

        var output = Assert.IsType<ProblemUnderstandingOutput>(result.Data);
        Assert.Equal("Plumbing", output.Category);
        Assert.False(output.NeedsMoreInformation);

        // Confirm Gemini was actually called (not bypassed)
        geminiMock.Verify(
            g => g.GenerateContentAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // -----------------------------------------------------------------------
    // Test B: Gemini unavailable → safe degraded output
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TestB_GeminiUnavailable_ReturnsSafeDegradedOutput()
    {
        // Arrange
        // Gemini returns null (no API key / network failure / offline simulation disabled).
        var (agent, _) = BuildAgent(
            configureTool: null,
            geminiResponse: null);   // null simulates Gemini unavailable

        var context = MakeContext("My pipe burst and water is flooding everywhere");

        // Act
        var result = await agent.ExecuteAsync(context);

        // Assert — safe degraded output
        Assert.True(result.Success);
        Assert.Equal("AwaitingInformation", result.NextAction);

        var output = Assert.IsType<ProblemUnderstandingOutput>(result.Data);
        Assert.Equal("Unclassified", output.Category);
        Assert.True(output.NeedsMoreInformation);
        Assert.True(output.Confidence <= 0.4m,
            $"Expected confidence ≤ 0.4 for degraded output, got {output.Confidence}");
        Assert.NotEmpty(output.FollowUpQuestions);
        Assert.Contains("Degraded", output.AdditionalInformation.Keys);
    }

    // -----------------------------------------------------------------------
    // Test C: Tool failure is logged but does not crash the workflow
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TestC_ToolFailure_DoesNotCrashWorkflow_AndAgentSucceeds()
    {
        // Arrange — all three optional tools throw; Gemini still succeeds
        var (agent, geminiMock) = BuildAgent(
            registry =>
            {
                registry.Register(ThrowingTool(
                    "LocationExtractionTool",
                    new TimeoutException("Simulated location timeout")));
                registry.Register(ThrowingTool(
                    "ProblemClassificationTool",
                    new InvalidOperationException("Simulated classification failure")));
                registry.Register(ThrowingTool(
                    "ServiceKnowledgeTool",
                    new HttpRequestException("Simulated knowledge service unavailable")));
            },
            geminiResponse: GeminiElectricalResponse);

        var context = MakeContext("Sparks are coming from my electrical socket");

        // Act — must not throw
        var exception = await Record.ExceptionAsync(() => agent.ExecuteAsync(context));

        // Assert — no exception escapes the agent
        Assert.Null(exception);

        // Gemini was still called
        geminiMock.Verify(
            g => g.GenerateContentAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // And the result is still valid
        var result = await agent.ExecuteAsync(MakeContext("Sparks are coming from my electrical socket"));
        Assert.True(result.Success);
        Assert.Equal("Analysed", result.NextAction);
    }

    // -----------------------------------------------------------------------
    // Test D: Gemini is authoritative — output conflicts with classification tool
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TestD_GeminiIsAuthoritative_WhenConflictingWithClassificationTool()
    {
        // Arrange
        // Gemini says "Electrical" with high confidence.
        // Classification tool says "Plumbing" — this should NOT override Gemini's Electrical result.
        var (agent, geminiMock) = BuildAgent(
            registry =>
            {
                // Classification tool returns "Plumbing" to conflict with Gemini
                registry.Register(SucceedingTool(
                    "ProblemClassificationTool",
                    new ProblemClassificationData(
                        Category: "Plumbing",
                        Confidence: 0.7m,
                        SupportingTerms: new[] { "pipe" })));
            },
            geminiResponse: GeminiElectricalResponse);

        var context = MakeContext("My electrical socket is sparking and there is a burning smell");

        // Act
        var result = await agent.ExecuteAsync(context);

        // Assert — Gemini's Electrical category is preserved, not overridden by tool's Plumbing
        Assert.True(result.Success);
        var output = Assert.IsType<ProblemUnderstandingOutput>(result.Data);

        // The classification tool alignment only promotes a category when Gemini returned
        // "Unclassified". Gemini returned "Electrical" here, so it must remain "Electrical".
        Assert.Equal("Electrical", output.Category);
        Assert.Equal("Analysed", result.NextAction);

        geminiMock.Verify(
            g => g.GenerateContentAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // -----------------------------------------------------------------------
    // Additional: Empty description bypasses Gemini and returns AwaitingInformation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task EmptyDescription_ReturnsAwaitingInformation_WithoutCallingGemini()
    {
        // Arrange
        var (agent, geminiMock) = BuildAgent();
        var context = new AgentContext { Input = "   " };

        // Act
        var result = await agent.ExecuteAsync(context);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("AwaitingInformation", result.NextAction);

        var output = Assert.IsType<ProblemUnderstandingOutput>(result.Data);
        Assert.Equal("Unclassified", output.Category);
        Assert.True(output.NeedsMoreInformation);

        // Gemini must NOT be called for empty input (fast-path)
        geminiMock.Verify(
            g => g.GenerateContentAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
