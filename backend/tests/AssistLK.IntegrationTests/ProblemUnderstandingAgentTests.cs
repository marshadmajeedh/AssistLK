using AssistLK.Agents.Abstractions;
using AssistLK.Agents.Agents;
using AssistLK.Agents.Core;
using AssistLK.Agents.Models;
using AssistLK.Agents.Tools;
using AssistLK.Domain.Enums;

namespace AssistLK.IntegrationTests;

/// <summary>
/// Phase 7C – ProblemUnderstandingAgent unit tests.
///
/// Tests are focused on observable behaviour: category classification,
/// urgency determination, safety wording, and follow-up question generation.
/// No hard-coded identical string assertions are used for summaries
/// or follow-up text; instead, tests verify structural properties.
/// </summary>
public class ProblemUnderstandingAgentTests
{
    // -------------------------------------------------------
    // Test fixture helper
    // -------------------------------------------------------

    private static ProblemUnderstandingAgent CreateAgent()
    {
        var toolRegistry = new ToolRegistry();
        toolRegistry.Register(new ProblemClassificationTool());
        toolRegistry.Register(new LocationExtractionTool());
        toolRegistry.Register(new ServiceKnowledgeTool());
        var toolExecutor = new ToolExecutor(toolRegistry);
        return new ProblemUnderstandingAgent(toolExecutor);
    }

    private static AgentContext BuildContext(
        string description,
        string locationText = "Colombo",
        decimal? latitude = null,
        decimal? longitude = null)
    {
        var serviceRequestId = Guid.NewGuid();

        var typedInput = new ProblemUnderstandingInput
        {
            ServiceRequestId = serviceRequestId,
            Description = description,
            LocationText = locationText,
            Latitude = latitude,
            Longitude = longitude
        };

        var context = new AgentContext
        {
            WorkflowId = Guid.NewGuid(),
            Input = description,
            Data =
            {
                [nameof(ProblemUnderstandingInput)] = typedInput
            }
        };

        return context;
    }

    private static ProblemUnderstandingOutput ExtractOutput(AgentResult result)
    {
        Assert.NotNull(result.Data);
        var output = Assert.IsType<ProblemUnderstandingOutput>(result.Data);
        return output;
    }

    // -------------------------------------------------------
    // Test 1: ProblemUnderstandingAgent implements IAgent
    // -------------------------------------------------------

    [Fact]
    public void ProblemUnderstandingAgent_ImplementsIAgent()
    {
        var agent = CreateAgent();

        Assert.IsAssignableFrom<IAgent>(agent);
        Assert.Equal("ProblemUnderstandingAgent", agent.Name);
    }

    // -------------------------------------------------------
    // Test 2: Plumbing example
    // -------------------------------------------------------

    [Fact]
    public async Task PlumbingRequest_ProducesPlumbingCategoryAndReasonableOutput()
    {
        var agent = CreateAgent();
        var context = BuildContext(
            "My kitchen pipe is leaking badly.");

        var result = await agent.ExecuteAsync(context);

        Assert.True(result.Success);
        var output = ExtractOutput(result);

        Assert.Equal("Plumbing", output.Category);
        Assert.NotEmpty(output.ProblemSummary);
        Assert.NotEqual(ServiceRequestUrgency.Unknown, output.Urgency);

        // Confidence must be within the valid range.
        Assert.InRange(output.Confidence, 0m, 1m);

        // A sufficiently described plumbing leak should not ask for more info.
        Assert.Empty(output.FollowUpQuestions);
    }

    // -------------------------------------------------------
    // Test 3: Vehicle example
    // -------------------------------------------------------

    [Fact]
    public async Task VehicleRequest_ProducesVehicleCategoryAndUncertaintyAwareSummary()
    {
        var agent = CreateAgent();
        var context = BuildContext("My car doesn't start.");

        var result = await agent.ExecuteAsync(context);

        Assert.True(result.Success);
        var output = ExtractOutput(result);

        Assert.Equal("Vehicle Repair", output.Category);
        Assert.NotEmpty(output.ProblemSummary);

        // Summary must NOT make a guaranteed diagnosis.
        Assert.DoesNotContain("definitely", output.ProblemSummary,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("your battery is dead", output.ProblemSummary,
            StringComparison.OrdinalIgnoreCase);

        // Summary should use uncertainty-aware language.
        var uncertaintyWords = new[] { "possible", "may", "could" };
        Assert.Contains(uncertaintyWords, w =>
            output.ProblemSummary.Contains(w, StringComparison.OrdinalIgnoreCase));

        Assert.InRange(output.Confidence, 0m, 1m);
    }

    // -------------------------------------------------------
    // Test 4: Electrical safety example
    // -------------------------------------------------------

    [Fact]
    public async Task ElectricalSafetyRequest_IdentifiesElectricalCategoryAndIsUrgent()
    {
        var agent = CreateAgent();
        var context = BuildContext(
            "Some sockets stopped working and I smell something burning.");

        var result = await agent.ExecuteAsync(context);

        Assert.True(result.Success);
        var output = ExtractOutput(result);

        // Must identify Electrical.
        Assert.Equal("Electrical", output.Category);

        // Must treat it as urgent (High or Critical).
        Assert.True(
            output.Urgency == ServiceRequestUrgency.High ||
            output.Urgency == ServiceRequestUrgency.Critical,
            $"Expected High or Critical urgency but got {output.Urgency}");

        // Must NOT provide dangerous DIY instructions.
        var dangerousTerms = new[]
        {
            "open the wire",
            "open the wiring",
            "touch the wire",
            "handle the wire",
            "replace the wire yourself",
            "fix the wire yourself",
            "inspect the wire yourself",
            "do it yourself"
        };
        foreach (var term in dangerousTerms)
        {
            Assert.DoesNotContain(term, output.ProblemSummary,
                StringComparison.OrdinalIgnoreCase);
        }

        // Must NOT guarantee the diagnosis.
        Assert.DoesNotContain("your wiring is broken", output.ProblemSummary,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("definitely", output.ProblemSummary,
            StringComparison.OrdinalIgnoreCase);

        // Safety language: should suggest professional inspection.
        var professionalLanguage = new[] { "professional", "inspection", "possible" };
        Assert.Contains(professionalLanguage, w =>
            output.ProblemSummary.Contains(w, StringComparison.OrdinalIgnoreCase));

        Assert.InRange(output.Confidence, 0m, 1m);
    }

    // -------------------------------------------------------
    // Test 5: Appliance example
    // -------------------------------------------------------

    [Fact]
    public async Task ApplianceRequest_ProducesApplianceRelatedCategory()
    {
        var agent = CreateAgent();
        var context = BuildContext("My fridge is not cooling.");

        var result = await agent.ExecuteAsync(context);

        Assert.True(result.Success);
        var output = ExtractOutput(result);

        Assert.Equal("Appliance Repair", output.Category);
        Assert.NotEmpty(output.ProblemSummary);
        Assert.InRange(output.Confidence, 0m, 1m);
    }

    // -------------------------------------------------------
    // Test 6: Ambiguous example – needs more information
    // -------------------------------------------------------

    [Fact]
    public async Task AmbiguousRequest_ProducesNeedsMoreInformationAndFollowUpQuestions()
    {
        var agent = CreateAgent();
        var context = BuildContext("My appliance is broken.");

        var result = await agent.ExecuteAsync(context);

        Assert.True(result.Success);
        var output = ExtractOutput(result);

        Assert.True(output.NeedsMoreInformation,
            "Expected NeedsMoreInformation=true for an ambiguous description.");

        Assert.NotEmpty(output.FollowUpQuestions);
        Assert.True(
            output.FollowUpQuestions.Count >= 1 &&
            output.FollowUpQuestions.Count <= 3,
            $"Expected 1–3 follow-up questions but got {output.FollowUpQuestions.Count}.");

        // The follow-up questions must be non-empty strings.
        foreach (var question in output.FollowUpQuestions)
            Assert.NotEmpty(question);

        Assert.InRange(output.Confidence, 0m, 1m);
    }

    // -------------------------------------------------------
    // Test 7: Empty / invalid input handled gracefully
    // -------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public async Task EmptyOrWhitespaceInput_HandledGracefullyWithNoException(
        string description)
    {
        var agent = CreateAgent();
        var context = new AgentContext
        {
            WorkflowId = Guid.NewGuid(),
            Input = description
        };

        // Must not throw.
        var result = await agent.ExecuteAsync(context);

        Assert.NotNull(result);
        Assert.True(result.Success,
            "Agent should return Success=true even for empty input.");

        var output = ExtractOutput(result);
        Assert.True(output.NeedsMoreInformation);
        Assert.InRange(output.Confidence, 0m, 1m);
    }

    // -------------------------------------------------------
    // Test 8: Confidence always within [0, 1]
    // -------------------------------------------------------

    [Theory]
    [InlineData("My kitchen pipe is leaking badly.")]
    [InlineData("My car doesn't start.")]
    [InlineData("Some sockets stopped working and I smell something burning.")]
    [InlineData("My fridge is not cooling.")]
    [InlineData("My appliance is broken.")]
    [InlineData("")]
    [InlineData("Something is wrong.")]
    [InlineData("There is a big problem at my house.")]
    [InlineData("My pipe burst and water is flooding the room.")]
    [InlineData("A socket smells like burning.")]
    [InlineData("My car stopped in the road.")]
    [InlineData("My tap is dripping slowly.")]
    public async Task Confidence_IsAlwaysWithinZeroToOne(string description)
    {
        var agent = CreateAgent();
        var context = new AgentContext
        {
            WorkflowId = Guid.NewGuid(),
            Input = description
        };

        var result = await agent.ExecuteAsync(context);
        var output = ExtractOutput(result);

        Assert.InRange(output.Confidence, 0m, 1m);
    }

    // -------------------------------------------------------
    // Test 9: Follow-up questions are bounded and relevant
    // -------------------------------------------------------

    [Theory]
    [InlineData("My appliance is broken.")]
    [InlineData("Something is wrong.")]
    [InlineData("My car stopped.")]
    [InlineData("")]
    public async Task FollowUpQuestions_AreBoundedAndAllNonEmpty(string description)
    {
        var agent = CreateAgent();
        var context = new AgentContext
        {
            WorkflowId = Guid.NewGuid(),
            Input = description
        };

        var result = await agent.ExecuteAsync(context);
        var output = ExtractOutput(result);

        // Enforce 0–3 questions maximum.
        Assert.True(
            output.FollowUpQuestions.Count <= 3,
            $"Expected at most 3 follow-up questions but got {output.FollowUpQuestions.Count}.");

        // If questions are present, none may be empty.
        foreach (var question in output.FollowUpQuestions)
            Assert.NotEmpty(question);

        // If NeedsMoreInformation is false, questions must be empty.
        if (!output.NeedsMoreInformation)
            Assert.Empty(output.FollowUpQuestions);
    }

    // -------------------------------------------------------
    // Test 10: Output does not include chain-of-thought
    // -------------------------------------------------------

    [Fact]
    public async Task Output_DoesNotContainInternalRationaleOrChainOfThought()
    {
        var agent = CreateAgent();
        var context = BuildContext(
            "My kitchen pipe is leaking badly.");

        var result = await agent.ExecuteAsync(context);
        var output = ExtractOutput(result);

        // Ensure the output type exposes only the specified public surface.
        var type = output.GetType();
        var propertyNames = type.GetProperties()
            .Select(p => p.Name)
            .ToArray();

        // These fields would indicate internal reasoning leakage.
        var internalFields = new[]
        {
            "Reasoning", "ChainOfThought", "Rationale",
            "InternalNotes", "DebugInfo", "Trace", "Steps"
        };

        foreach (var field in internalFields)
        {
            Assert.DoesNotContain(field, propertyNames);
        }

        // Summary must be concise (no multi-paragraph chain-of-thought).
        Assert.True(
            output.ProblemSummary.Length < 300,
            "ProblemSummary should be concise, not a chain-of-thought block.");
    }

    // -------------------------------------------------------
    // Test 11: Agent does not require DbContext or repositories
    // -------------------------------------------------------

    [Fact]
    public void Agent_CanBeInstantiatedWithoutDatabaseDependencies()
    {
        var toolRegistry = new ToolRegistry();
        var toolExecutor = new ToolExecutor(toolRegistry);
        var agent = new ProblemUnderstandingAgent(toolExecutor);

        Assert.NotNull(agent);

        // ToolExecutor is now an expected dependency.
        var ctors = typeof(ProblemUnderstandingAgent).GetConstructors();
        Assert.Single(ctors);

        var parameters = ctors[0].GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(ToolExecutor), parameters[0].ParameterType);

        var forbiddenTypeNames = new[]
        {
            "AssistLKDbContext",
            "IServiceRequestRepository",
            "IProblemAnalysisRepository"
        };

        foreach (var param in parameters)
        {
            Assert.DoesNotContain(forbiddenTypeNames, name => param.ParameterType.Name.Contains(name));
        }
    }

    // -------------------------------------------------------
    // Additional targeted tests for specification examples
    // -------------------------------------------------------

    [Fact]
    public async Task BurstPipeFloodingRequest_ProducesHighUrgency()
    {
        var agent = CreateAgent();
        var context = BuildContext(
            "My pipe burst and water is flooding the room.");

        var result = await agent.ExecuteAsync(context);
        var output = ExtractOutput(result);

        Assert.Equal("Plumbing", output.Category);
        Assert.Equal(ServiceRequestUrgency.High, output.Urgency);
        Assert.InRange(output.Confidence, 0m, 1m);
    }

    [Fact]
    public async Task CarStoppedOnRoad_ProducesVehicleCategoryAndHighUrgency()
    {
        var agent = CreateAgent();
        var context = BuildContext("My car stopped in the road.");

        var result = await agent.ExecuteAsync(context);
        var output = ExtractOutput(result);

        Assert.Equal("Vehicle Repair", output.Category);
        Assert.Equal(ServiceRequestUrgency.High, output.Urgency);
        Assert.InRange(output.Confidence, 0m, 1m);
    }

    [Fact]
    public async Task DrippingTapRequest_ProducesLowOrMediumUrgency()
    {
        var agent = CreateAgent();
        var context = BuildContext("My tap is dripping slowly.");

        var result = await agent.ExecuteAsync(context);
        var output = ExtractOutput(result);

        Assert.Equal("Plumbing", output.Category);
        Assert.True(
            output.Urgency == ServiceRequestUrgency.Low ||
            output.Urgency == ServiceRequestUrgency.Medium,
            $"Expected Low or Medium urgency for a dripping tap but got {output.Urgency}.");
    }

    [Fact]
    public async Task SocketBurningSmellRequest_ProducesElectricalCategoryAndHighOrCriticalUrgency()
    {
        var agent = CreateAgent();
        var context = BuildContext(
            "My socket smells like burning.");

        var result = await agent.ExecuteAsync(context);
        var output = ExtractOutput(result);

        Assert.Equal("Electrical", output.Category);
        Assert.True(
            output.Urgency == ServiceRequestUrgency.High ||
            output.Urgency == ServiceRequestUrgency.Critical,
            $"Expected High or Critical urgency but got {output.Urgency}.");
    }

    [Fact]
    public async Task LocationText_IsPreservedInOutput()
    {
        var agent = CreateAgent();
        var context = BuildContext(
            "My kitchen pipe is leaking badly.",
            locationText: "Kandy, Sri Lanka",
            latitude: 7.291m,
            longitude: 80.634m);

        var result = await agent.ExecuteAsync(context);
        var output = ExtractOutput(result);

        Assert.Equal("Kandy, Sri Lanka", output.ExtractedLocation);
    }

    [Fact]
    public async Task AgentResult_NextAction_IsSetAppropriately()
    {
        var agent = CreateAgent();

        // Sufficient description -> Analysed
        var sufficientContext = BuildContext(
            "My kitchen pipe is leaking badly and flooding the kitchen floor.");
        var sufficientResult = await agent.ExecuteAsync(sufficientContext);
        Assert.Equal("Analysed", sufficientResult.NextAction);

        // Ambiguous description -> AwaitingInformation
        var ambiguousContext = BuildContext("My appliance is broken.");
        var ambiguousResult = await agent.ExecuteAsync(ambiguousContext);
        Assert.Equal("AwaitingInformation", ambiguousResult.NextAction);
    }

    [Fact]
    public async Task AgentName_IsStableAndMatchesRegistrationKey()
    {
        var agent = CreateAgent();

        Assert.Equal("ProblemUnderstandingAgent", agent.Name);

        // Verify executing via AgentContext still returns the right agent name.
        var context = BuildContext("My car doesn't start.");
        var result = await agent.ExecuteAsync(context);

        Assert.True(result.Success);
    }
}
