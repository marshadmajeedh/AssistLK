using AssistLK.Agents.Abstractions;
using AssistLK.Agents.Agents;
using AssistLK.Agents.Core;
using AssistLK.Agents.Models;
using AssistLK.Agents.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AssistLK.Api.Tests;

/// <summary>
/// Regression suite — Gemini Tool Validation Architecture (Component 1).
///
/// These tests verify the boundaries between the Gemini LLM (primary reasoning)
/// and the deterministic tools (validation / enrichment / safety).
///
/// Design principle enforced by every test:
///   "The LLM provides reasoning capability while deterministic tools enforce
///    reliability, validation, taxonomy consistency, and safety constraints."
///
/// Test coverage:
///   Test 1 — ClassificationToolFailure_DoesNotPreventGeminiExecution
///   Test 2 — GeminiUnavailable_ReturnsSafeFallback
///   Test 3 — InvalidGeminiCategory_IsNormalizedByValidator
///   Test 4 — GeminiReasoning_RemainsAuthoritativeOverValidator
/// </summary>
public class GeminiToolValidationTests
{
    // -----------------------------------------------------------------------
    // Shared Gemini JSON fixtures
    // -----------------------------------------------------------------------

    private const string GeminiPlumbingJson =
        """
        {
          "category": "Plumbing",
          "problemSummary": "Possible plumbing leak. Water may be escaping from pipework.",
          "urgency": "High",
          "needsMoreInformation": false,
          "followUpQuestions": [],
          "confidence": 0.9,
          "additionalInformation": {}
        }
        """;

    private const string GeminiElectricalJson =
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

    /// <summary>
    /// Gemini returns a near-miss category ("Plumber") that is not in the
    /// canonical taxonomy. The validator is responsible for normalizing it.
    /// </summary>
    private const string GeminiInvalidCategoryJson =
        """
        {
          "category": "Plumber",
          "problemSummary": "Possible pipe leak. Immediate professional attention may be required.",
          "urgency": "High",
          "needsMoreInformation": false,
          "followUpQuestions": [],
          "confidence": 0.9,
          "additionalInformation": {}
        }
        """;

    // -----------------------------------------------------------------------
    // Helpers — mirrors the pattern in ProblemUnderstandingAgentTests.cs
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds the agent with a controlled ToolRegistry and a mocked IGeminiService.
    /// Tools not explicitly registered cause ToolExecutor to return Success=false
    /// (tool not found), which is a non-fatal, non-throwing outcome.
    /// </summary>
    private static (ProblemUnderstandingAgent Agent, Mock<IGeminiService> GeminiMock) BuildAgent(
        Action<ToolRegistry>? configureTool = null,
        string? geminiResponse = GeminiPlumbingJson)
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

        return (new ProblemUnderstandingAgent(executor, geminiMock.Object, logger), geminiMock);
    }

    /// <summary>Creates a mock tool that always throws.</summary>
    private static IAgentTool ThrowingTool(string name, Exception exception)
    {
        var mock = new Mock<IAgentTool>();
        mock.Setup(t => t.Name).Returns(name);
        mock.Setup(t => t.Description).Returns($"Mock throwing {name}");
        mock.Setup(t => t.ExecuteAsync(
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);
        return mock.Object;
    }

    /// <summary>Creates a mock tool that returns success with the supplied data.</summary>
    private static IAgentTool SucceedingTool(string name, object data)
    {
        var mock = new Mock<IAgentTool>();
        mock.Setup(t => t.Name).Returns(name);
        mock.Setup(t => t.Description).Returns($"Mock succeeding {name}");
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
    // Test 1: Classification tool failure must NOT prevent Gemini execution
    // -----------------------------------------------------------------------

    /// <summary>
    /// Regression: ProblemClassificationTool throwing must not short-circuit the
    /// pipeline before Gemini executes. Tool failures are non-fatal; the LLM is
    /// the primary reasoning engine and must always run when a description is present.
    /// </summary>
    [Fact]
    public async Task ClassificationToolFailure_DoesNotPreventGeminiExecution()
    {
        // Arrange
        // The classification tool always throws; all other tools are not registered
        // (ToolExecutor returns Success=false for missing tools, which is non-fatal).
        var (agent, geminiMock) = BuildAgent(
            configureTool: registry =>
            {
                registry.Register(ThrowingTool(
                    "ProblemClassificationTool",
                    new InvalidOperationException("Simulated classification service failure")));
            },
            geminiResponse: GeminiPlumbingJson);

        var context = MakeContext("My pipe is leaking and water is flooding the kitchen floor");

        // Act
        var result = await agent.ExecuteAsync(context);

        // Assert — Gemini reasoning produced a valid result despite the tool failure
        Assert.True(result.Success, "Agent must succeed even when classification tool throws");
        Assert.Equal("Analysed", result.NextAction);

        var output = Assert.IsType<ProblemUnderstandingOutput>(result.Data);
        Assert.Equal("Plumbing", output.Category);
        Assert.False(output.NeedsMoreInformation);

        // Confirm Gemini was actually invoked — it must not have been bypassed
        geminiMock.Verify(
            g => g.GenerateContentAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Gemini must be called regardless of classification tool failure");
    }

    // -----------------------------------------------------------------------
    // Test 2: Gemini unavailable → safe fallback (no corrupted analysis)
    // -----------------------------------------------------------------------

    /// <summary>
    /// When GeminiService returns null (API key absent, network error, quota exceeded),
    /// the agent must produce a safe degraded output — never a corrupted or partially
    /// filled analysis. Status must be AwaitingInformation, not Analyzed.
    /// </summary>
    [Fact]
    public async Task GeminiUnavailable_ReturnsSafeFallback()
    {
        // Arrange — null simulates Gemini unavailable (offline / no API key)
        var (agent, _) = BuildAgent(
            configureTool: null,
            geminiResponse: null);

        var context = MakeContext("My pipe is leaking and water is flooding the kitchen floor");

        // Act
        var result = await agent.ExecuteAsync(context);

        // Assert — safe degraded output, not a corrupted partial analysis
        Assert.True(result.Success, "Agent must return Success=true even in degraded mode");
        Assert.Equal("AwaitingInformation", result.NextAction);

        var output = Assert.IsType<ProblemUnderstandingOutput>(result.Data);
        Assert.Equal("Unclassified", output.Category);
        Assert.True(output.NeedsMoreInformation,
            "Degraded output must always require more information");
        Assert.NotEmpty(output.FollowUpQuestions);

        // Confidence must be low — degraded output must signal low reliability
        Assert.True(output.Confidence <= 0.4m,
            $"Degraded output confidence must be ≤ 0.4, got {output.Confidence}");

        // The Degraded marker key must be present so consumers can detect fallback mode
        Assert.True(output.AdditionalInformation.ContainsKey("Degraded"),
            "Degraded output must contain the 'Degraded' marker in AdditionalInformation");
    }

    // -----------------------------------------------------------------------
    // Test 3: Invalid Gemini category is normalized via the validator
    // -----------------------------------------------------------------------

    /// <summary>
    /// Taxonomy normalization path:
    ///
    ///   Gemini returns "Plumber" (not a canonical category)
    ///   → TryParseGeminiResponse maps it to "Unclassified" (no exact match)
    ///   → ProblemClassificationTool (as validator) detects plumbing keywords
    ///   → Alignment logic promotes "Unclassified" → "Plumbing"
    ///   → Final category: "Plumbing"
    ///
    /// This is the intended collaboration between the LLM and the deterministic
    /// validator: Gemini provides reasoning intent, the tool enforces the taxonomy.
    /// </summary>
    [Fact]
    public async Task InvalidGeminiCategory_IsNormalizedByValidator()
    {
        // Arrange
        // Gemini returns "Plumber" — not in the canonical set:
        //   { Plumbing, Electrical, Vehicle Repair, Appliance Repair, Unclassified }
        // The classification tool (acting as taxonomy validator) returns "Plumbing"
        // because the description contains plumbing keywords.
        var (agent, _) = BuildAgent(
            configureTool: registry =>
            {
                registry.Register(SucceedingTool(
                    "ProblemClassificationTool",
                    new ProblemClassificationData(
                        Category: "Plumbing",
                        Confidence: 0.75m,
                        SupportingTerms: new[] { "pipe", "leak", "water" })));
            },
            geminiResponse: GeminiInvalidCategoryJson);

        var context = MakeContext("My pipe is leaking badly and water is flooding the room");

        // Act
        var result = await agent.ExecuteAsync(context);

        // Assert — taxonomy normalization has occurred
        Assert.True(result.Success);

        var output = Assert.IsType<ProblemUnderstandingOutput>(result.Data);

        // "Plumber" (invalid) → parser maps to "Unclassified" → validator promotes to "Plumbing"
        Assert.True(
            output.Category == "Plumbing",
            $"Invalid category 'Plumber' must be normalized to canonical 'Plumbing' via taxonomy validation. Got: '{output.Category}'");
    }

    // -----------------------------------------------------------------------
    // Test 4: Gemini reasoning remains authoritative over the validator
    // -----------------------------------------------------------------------

    /// <summary>
    /// Validation policy decision (documented):
    ///
    ///   The ProblemClassificationTool may only PROMOTE a category if Gemini
    ///   returned "Unclassified". It must NEVER override a valid canonical
    ///   category that Gemini already provided with high confidence.
    ///
    ///   Rationale: Tools validate and constrain LLM output; they do not replace
    ///   LLM reasoning. Allowing a deterministic keyword-matcher to override a
    ///   confident LLM classification would degrade analysis quality, especially
    ///   for ambiguous descriptions that trip naive keyword rules.
    ///
    ///   Example that would break with tool-override: "My lights keep tripping
    ///   the water-pump circuit breaker" — keywords match both Plumbing and
    ///   Electrical. Gemini correctly identifies Electrical; the tool must not
    ///   override this with Plumbing just because "water" appeared.
    /// </summary>
    [Fact]
    public async Task GeminiReasoning_RemainsAuthoritativeOverValidator()
    {
        // Arrange
        // Gemini says "Electrical" (canonical, high confidence).
        // The classification tool returns "Plumbing" — a conflicting answer.
        // The alignment logic must NOT override Gemini because Gemini did not
        // return "Unclassified"; it returned a valid category it is confident about.
        var (agent, geminiMock) = BuildAgent(
            configureTool: registry =>
            {
                registry.Register(SucceedingTool(
                    "ProblemClassificationTool",
                    new ProblemClassificationData(
                        Category: "Plumbing",
                        Confidence: 0.70m,
                        SupportingTerms: new[] { "water" })));
            },
            geminiResponse: GeminiElectricalJson);

        var context = MakeContext("My electrical socket is sparking and there is a burning smell");

        // Act
        var result = await agent.ExecuteAsync(context);

        // Assert — Gemini's "Electrical" is preserved; tool's "Plumbing" is rejected
        Assert.True(result.Success);
        Assert.Equal("Analysed", result.NextAction);

        var output = Assert.IsType<ProblemUnderstandingOutput>(result.Data);

        Assert.True(
            output.Category == "Electrical",
            $"Gemini's canonical category must not be overridden by the classification tool. " +
            $"Tool alignment only activates when Gemini returns 'Unclassified'. Got: '{output.Category}'");

        // Gemini must have been called (not short-circuited by the tool)
        geminiMock.Verify(
            g => g.GenerateContentAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Gemini must be the primary reasoning engine — called exactly once per analysis");
    }
}
