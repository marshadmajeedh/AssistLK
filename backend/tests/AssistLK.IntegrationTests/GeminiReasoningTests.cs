using AssistLK.Agents.Abstractions;
using AssistLK.Agents.Agents;
using AssistLK.Agents.Core;
using AssistLK.Agents.Models;
using AssistLK.Agents.Services;
using AssistLK.Agents.Tools;
using AssistLK.Application.Interfaces;
using AssistLK.Application.Services;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssistLK.IntegrationTests;

/// <summary>
/// Dedicated test suite for Gemini LLM reasoning integration.
/// Tests valid JSON reasoning, safe degradation on API failure, and safety filtering.
/// </summary>
public class GeminiReasoningTests
{
    public class FakeGeminiService : IGeminiService
    {
        public Func<string, string?, string?>? Handler { get; set; }
        public string? ResponseToReturn { get; set; }
        public Exception? ExceptionToThrow { get; set; }
        public string? LastPrompt { get; private set; }
        public string? LastSystemInstruction { get; private set; }

        public Task<string?> GenerateContentAsync(
            string prompt,
            string? systemInstruction = null,
            CancellationToken cancellationToken = default)
        {
            LastPrompt = prompt;
            LastSystemInstruction = systemInstruction;

            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            if (Handler != null)
            {
                return Task.FromResult(Handler(prompt, systemInstruction));
            }

            return Task.FromResult(ResponseToReturn);
        }
    }

    private static (ProblemUnderstandingAgent Agent, FakeGeminiService FakeGemini) CreateAgentWithFakeGemini()
    {
        var toolRegistry = new ToolRegistry();
        toolRegistry.Register(new ProblemClassificationTool());
        toolRegistry.Register(new LocationExtractionTool());
        toolRegistry.Register(new ServiceKnowledgeTool());
        var toolExecutor = new ToolExecutor(toolRegistry);

        var fakeGemini = new FakeGeminiService();
        var agent = new ProblemUnderstandingAgent(toolExecutor, fakeGemini, NullLogger<ProblemUnderstandingAgent>.Instance);

        return (agent, fakeGemini);
    }

    private static AgentContext BuildContext(string description, string locationText = "Colombo")
    {
        var input = new ProblemUnderstandingInput
        {
            ServiceRequestId = Guid.NewGuid(),
            Description = description,
            LocationText = locationText
        };

        return new AgentContext
        {
            WorkflowId = Guid.NewGuid(),
            Input = description,
            Data =
            {
                [nameof(ProblemUnderstandingInput)] = input
            }
        };
    }

    // ---------------------------------------------------------------------------------
    // 1. Gemini returns valid JSON -> Agent creates analysis
    // ---------------------------------------------------------------------------------

    [Fact]
    public async Task GeminiReturnsValidJson_AgentCreatesValidAnalysis()
    {
        var (agent, fakeGemini) = CreateAgentWithFakeGemini();

        fakeGemini.ResponseToReturn = @"{
            ""category"": ""Plumbing"",
            ""problemSummary"": ""Possible water leakage from kitchen sink pipe."",
            ""urgency"": ""High"",
            ""needsMoreInformation"": false,
            ""followUpQuestions"": [],
            ""confidence"": 0.90,
            ""additionalInformation"": {
                ""fixture"": ""kitchen sink""
            }
        }";

        var context = BuildContext("Water leaking heavily from kitchen sink pipe");
        var result = await agent.ExecuteAsync(context);

        Assert.True(result.Success);
        var output = Assert.IsType<ProblemUnderstandingOutput>(result.Data);

        Assert.Equal("Plumbing", output.Category);
        Assert.Equal("High", output.Urgency.ToString());
        Assert.Equal(0.90m, output.Confidence);
        Assert.False(output.NeedsMoreInformation);
        Assert.Contains("Possible", output.ProblemSummary);
        Assert.Equal("Analysed", result.NextAction);
        Assert.Empty(output.FollowUpQuestions);

        // Verify tool integration enriched additional information
        Assert.True(output.AdditionalInformation.ContainsKey("ServiceFamily"));
        Assert.Equal("Plumbing and Water Supply", output.AdditionalInformation["ServiceFamily"]);
    }

    [Fact]
    public async Task Demo_WaterLeakingHeavilyFromKitchenSinkPipe_MatchesExpectedSchema()
    {
        // Tests the default GeminiService (using GOOGLE_API_KEY when provided or offline simulation)
        var toolRegistry = new ToolRegistry();
        toolRegistry.Register(new ProblemClassificationTool());
        toolRegistry.Register(new LocationExtractionTool());
        toolRegistry.Register(new ServiceKnowledgeTool());
        var toolExecutor = new ToolExecutor(toolRegistry);

        var agent = new ProblemUnderstandingAgent(toolExecutor, new GeminiService(), NullLogger<ProblemUnderstandingAgent>.Instance);
        var context = BuildContext("Water leaking heavily from kitchen sink pipe");

        var result = await agent.ExecuteAsync(context);

        Assert.True(result.Success);
        var output = Assert.IsType<ProblemUnderstandingOutput>(result.Data);

        Assert.Equal("Plumbing", output.Category);
        Assert.Equal(ServiceRequestUrgency.High, output.Urgency);
        Assert.Equal(0.9m, output.Confidence);
        Assert.False(output.NeedsMoreInformation);
    }

    [Fact]
    public async Task GeminiReturnsMarkdownWrappedJson_AgentParsesSuccessfully()
    {
        var (agent, fakeGemini) = CreateAgentWithFakeGemini();

        fakeGemini.ResponseToReturn = "```json\n" +
            "{\n" +
            "  \"category\": \"Electrical\",\n" +
            "  \"problemSummary\": \"Possible circuit overload at main switch.\",\n" +
            "  \"urgency\": \"Medium\",\n" +
            "  \"needsMoreInformation\": false,\n" +
            "  \"followUpQuestions\": [],\n" +
            "  \"confidence\": 0.85,\n" +
            "  \"additionalInformation\": {}\n" +
            "}\n```";

        var context = BuildContext("Main circuit switch tripped and power is out");
        var result = await agent.ExecuteAsync(context);

        Assert.True(result.Success);
        var output = Assert.IsType<ProblemUnderstandingOutput>(result.Data);

        Assert.Equal("Electrical", output.Category);
        Assert.Equal(ServiceRequestUrgency.Medium, output.Urgency);
        Assert.Equal(0.85m, output.Confidence);
        Assert.False(output.NeedsMoreInformation);
    }

    // ---------------------------------------------------------------------------------
    // 2. Gemini fails -> Safe degradation
    // ---------------------------------------------------------------------------------

    [Fact]
    public async Task GeminiFails_NetworkException_DegradesSafelyWithoutCrashing()
    {
        var (agent, fakeGemini) = CreateAgentWithFakeGemini();
        fakeGemini.ExceptionToThrow = new HttpRequestException("Google Gemini API unreachable (connection refused)");

        var context = BuildContext("Water leaking from kitchen pipe");
        var result = await agent.ExecuteAsync(context);

        Assert.True(result.Success);
        var output = Assert.IsType<ProblemUnderstandingOutput>(result.Data);

        // Safe degradation verification
        Assert.Equal("Unclassified", output.Category);
        Assert.Equal(ServiceRequestUrgency.Unknown, output.Urgency);
        Assert.True(output.Confidence <= 0.2m);
        Assert.True(output.NeedsMoreInformation);
        Assert.NotEmpty(output.FollowUpQuestions);
        Assert.Equal("AwaitingInformation", result.NextAction);
        Assert.Equal("True", output.AdditionalInformation["Degraded"]);
    }

    [Fact]
    public async Task GeminiFails_ReturnsNullOrEmpty_DegradesSafely()
    {
        var (agent, fakeGemini) = CreateAgentWithFakeGemini();
        fakeGemini.ResponseToReturn = null;

        var context = BuildContext("Water leaking from kitchen pipe");
        var result = await agent.ExecuteAsync(context);

        Assert.True(result.Success);
        var output = Assert.IsType<ProblemUnderstandingOutput>(result.Data);

        Assert.Equal("Unclassified", output.Category);
        Assert.Equal(ServiceRequestUrgency.Unknown, output.Urgency);
        Assert.True(output.NeedsMoreInformation);
        Assert.Equal("AwaitingInformation", result.NextAction);
    }

    [Fact]
    public async Task GeminiReturnsMalformedJson_DegradesSafely()
    {
        var (agent, fakeGemini) = CreateAgentWithFakeGemini();
        fakeGemini.ResponseToReturn = "I am an AI assistant and I think you have a leaking sink!";

        var context = BuildContext("Sink is leaking");
        var result = await agent.ExecuteAsync(context);

        Assert.True(result.Success);
        var output = Assert.IsType<ProblemUnderstandingOutput>(result.Data);

        Assert.Equal("Unclassified", output.Category);
        Assert.Equal(ServiceRequestUrgency.Unknown, output.Urgency);
        Assert.True(output.NeedsMoreInformation);
        Assert.Equal("AwaitingInformation", result.NextAction);
    }

    [Fact]
    public async Task GeminiReturnsEmptyJson_AppliesSafeDefaults()
    {
        var (agent, fakeGemini) = CreateAgentWithFakeGemini();
        fakeGemini.ResponseToReturn = "{}";

        var context = BuildContext("Something is broken and I need help");
        var result = await agent.ExecuteAsync(context);

        Assert.True(result.Success);
        var output = Assert.IsType<ProblemUnderstandingOutput>(result.Data);

        Assert.Equal("Unclassified", output.Category);
        Assert.Equal(ServiceRequestUrgency.Unknown, output.Urgency);
        Assert.True(output.NeedsMoreInformation);
        Assert.NotEmpty(output.ProblemSummary);
    }

    // ---------------------------------------------------------------------------------
    // 3. Gemini gives unsafe response -> Safety filtering
    // ---------------------------------------------------------------------------------

    [Fact]
    public async Task GeminiReturnsDangerousDIYInstructions_SafetyFilterSanitizesContent()
    {
        var (agent, fakeGemini) = CreateAgentWithFakeGemini();

        fakeGemini.ResponseToReturn = @"{
            ""category"": ""Electrical"",
            ""problemSummary"": ""Open the wire box and strip the wire yourself to fix the circuit breaker yourself."",
            ""urgency"": ""High"",
            ""needsMoreInformation"": false,
            ""followUpQuestions"": [""Did you touch the wire directly?"", ""Can you bypass the fuse yourself?""],
            ""confidence"": 0.85,
            ""additionalInformation"": {}
        }";

        var context = BuildContext("Spark from electrical wall socket");
        var result = await agent.ExecuteAsync(context);

        Assert.True(result.Success);
        var output = Assert.IsType<ProblemUnderstandingOutput>(result.Data);

        // Dangerous DIY instructions must be completely absent
        Assert.DoesNotContain("strip the wire", output.ProblemSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fix the circuit breaker yourself", output.ProblemSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("open the wire", output.ProblemSummary, StringComparison.OrdinalIgnoreCase);

        // Recommends professional inspection
        Assert.Contains("professional", output.ProblemSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("inspection", output.ProblemSummary, StringComparison.OrdinalIgnoreCase);

        // Follow-up questions must not contain dangerous DIY suggestions
        foreach (var q in output.FollowUpQuestions)
        {
            Assert.DoesNotContain("touch the wire", q, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("bypass", q, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task GeminiReturnsGuaranteedDiagnosis_SafetyFilterEnforcesUncertaintyLanguage()
    {
        var (agent, fakeGemini) = CreateAgentWithFakeGemini();

        fakeGemini.ResponseToReturn = @"{
            ""category"": ""Vehicle Repair"",
            ""problemSummary"": ""Your battery is dead and definitely 100% broken."",
            ""urgency"": ""Medium"",
            ""needsMoreInformation"": false,
            ""followUpQuestions"": [],
            ""confidence"": 0.80,
            ""additionalInformation"": {}
        }";

        var context = BuildContext("Car does not start in the morning");
        var result = await agent.ExecuteAsync(context);

        Assert.True(result.Success);
        var output = Assert.IsType<ProblemUnderstandingOutput>(result.Data);

        // Guaranteed diagnosis must be sanitized
        Assert.DoesNotContain("definitely", output.ProblemSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("100% certain", output.ProblemSummary, StringComparison.OrdinalIgnoreCase);

        // Uncertainty language must be present
        var uncertaintyWords = new[] { "possible", "may", "could", "potential" };
        Assert.Contains(uncertaintyWords, u => output.ProblemSummary.Contains(u, StringComparison.OrdinalIgnoreCase));
    }

    // ---------------------------------------------------------------------------------
    // 4. End-to-End Workflow Integration with canonical examples
    // ---------------------------------------------------------------------------------

    private sealed class WorkflowFixture
    {
        public AssistLKDbContext Db { get; }
        public AgentWorkflowService WorkflowService { get; }
        public AgentContextService ContextService { get; }
        public AgentMemoryService MemoryService { get; }
        public AgentMonitoringService MonitoringService { get; }
        public AgentSafetyService SafetyService { get; }
        public AgentOrchestrator Orchestrator { get; }
        public AgentRegistry Registry { get; }
        public ToolRegistry ToolReg { get; }
        public ToolExecutor ToolExec { get; }
        public FakeGeminiService FakeGemini { get; }
        public ProblemUnderstandingAgent Agent { get; }
        public ServiceRequestService RequestService { get; }
        public List<ServiceRequest> Requests { get; } = new();
        public List<ProblemAnalysis> Analyses { get; } = new();
        public ProblemUnderstandingWorkflowService Workflow { get; }

        public WorkflowFixture()
        {
            var options = new DbContextOptionsBuilder<AssistLKDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            Db = new AssistLKDbContext(options);

            WorkflowService = new AgentWorkflowService(Db);
            MemoryService = new AgentMemoryService(Db);
            ContextService = new AgentContextService(MemoryService);
            MonitoringService = new AgentMonitoringService(Db);
            SafetyService = new AgentSafetyService(new AgentSafetyPolicyEngine(), Db);

            ToolReg = new ToolRegistry();
            ToolReg.Register(new ProblemClassificationTool());
            ToolReg.Register(new LocationExtractionTool());
            ToolReg.Register(new ServiceKnowledgeTool());

            ToolExec = new ToolExecutor(ToolReg);
            FakeGemini = new FakeGeminiService();
            Agent = new ProblemUnderstandingAgent(ToolExec, FakeGemini, NullLogger<ProblemUnderstandingAgent>.Instance);

            Registry = new AgentRegistry();
            Registry.Register(Agent);

            Orchestrator = new AgentOrchestrator(Registry);

            var reqRepo = new InMemoryServiceRequestRepository(Requests, Analyses);
            var anaRepo = new InMemoryProblemAnalysisRepository(Analyses);
            RequestService = new ServiceRequestService(reqRepo, anaRepo);

            Workflow = new ProblemUnderstandingWorkflowService(
                WorkflowService,
                ContextService,
                MemoryService,
                MonitoringService,
                SafetyService,
                Orchestrator,
                Registry,
                RequestService);
        }
    }

    [Fact]
    public async Task CanonicalExample1_WaterLeakingUnderKitchenSink_ProducesPlumbingAndAnalyzed()
    {
        var fixture = new WorkflowFixture();
        var requestId = Guid.NewGuid();

        fixture.Requests.Add(new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Water leaking under kitchen sink",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Created
        });

        fixture.FakeGemini.ResponseToReturn = @"{
            ""category"": ""Plumbing"",
            ""problemSummary"": ""Possible water pipe or trap leakage under kitchen sink."",
            ""urgency"": ""Medium"",
            ""needsMoreInformation"": false,
            ""followUpQuestions"": [],
            ""confidence"": 0.88,
            ""additionalInformation"": {}
        }";

        var input = new ProblemUnderstandingInput
        {
            ServiceRequestId = requestId,
            Description = "Water leaking under kitchen sink",
            LocationText = "Colombo"
        };

        var result = await fixture.Workflow.AnalyzeAsync(input);

        Assert.True(result.Success);
        Assert.Equal("Plumbing", result.Category);
        Assert.Equal("Analyzed", result.Outcome);
        Assert.Equal(ServiceRequestStatus.Analyzed, result.Status);
        Assert.False(result.NeedsMoreInformation);
    }

    [Fact]
    public async Task CanonicalExample2_ItIsBrokenPleaseFix_ProducesUnclassifiedAndAwaitingInformation()
    {
        var fixture = new WorkflowFixture();
        var requestId = Guid.NewGuid();

        fixture.Requests.Add(new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "It is broken please fix",
            LocationText = "Kandy",
            Status = ServiceRequestStatus.Created
        });

        fixture.FakeGemini.ResponseToReturn = @"{
            ""category"": ""Unclassified"",
            ""problemSummary"": ""Possible service issue. Insufficient detail provided to classify."",
            ""urgency"": ""Unknown"",
            ""needsMoreInformation"": true,
            ""followUpQuestions"": [""What item or system is broken?"", ""What symptoms are occurring?""],
            ""confidence"": 0.20,
            ""additionalInformation"": {}
        }";

        var input = new ProblemUnderstandingInput
        {
            ServiceRequestId = requestId,
            Description = "It is broken please fix",
            LocationText = "Kandy"
        };

        var result = await fixture.Workflow.AnalyzeAsync(input);

        Assert.True(result.Success);
        Assert.Equal("Unclassified", result.Category);
        Assert.Equal("AwaitingInformation", result.Outcome);
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, result.Status);
        Assert.True(result.NeedsMoreInformation);
        Assert.NotEmpty(result.FollowUpQuestions);
    }

    [Fact]
    public async Task CanonicalExample3_SparkComingFromElectricalSocket_ProducesElectricalAndHighUrgency()
    {
        var fixture = new WorkflowFixture();
        var requestId = Guid.NewGuid();

        fixture.Requests.Add(new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Spark coming from electrical socket",
            LocationText = "Galle",
            Status = ServiceRequestStatus.Created
        });

        fixture.FakeGemini.ResponseToReturn = @"{
            ""category"": ""Electrical"",
            ""problemSummary"": ""Possible electrical fault causing sparks at outlet. Professional inspection is recommended."",
            ""urgency"": ""High"",
            ""needsMoreInformation"": false,
            ""followUpQuestions"": [],
            ""confidence"": 0.92,
            ""additionalInformation"": {}
        }";

        var input = new ProblemUnderstandingInput
        {
            ServiceRequestId = requestId,
            Description = "Spark coming from electrical socket",
            LocationText = "Galle"
        };

        var result = await fixture.Workflow.AnalyzeAsync(input);

        Assert.True(result.Success);
        Assert.Equal("Electrical", result.Category);
        Assert.Equal(ServiceRequestUrgency.High, result.Urgency);
        Assert.Contains("professional", result.ProblemSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Possible", result.ProblemSummary);
        Assert.Equal("Analyzed", result.Outcome);
    }

    // ---------------------------------------------------------------------------------
    // 5. Verification Test Suite: Cases A, B, C, D
    // ---------------------------------------------------------------------------------

    [Fact]
    public async Task CaseA_SuccessfulGeminiResponse_ReturnsStructuredOutputAndWorkflowContinues()
    {
        // A) Successful Gemini response
        // Input: "Water leaking under kitchen sink"
        // Mock Gemini output:
        // {
        //   category: "Plumbing",
        //   problemSummary: "Possible plumbing leak",
        //   urgency: "High",
        //   confidence: 0.9,
        //   needsMoreInformation: false
        // }
        // Verify: Agent returns structured output, existing workflow continues.

        var fixture = new WorkflowFixture();
        var requestId = Guid.NewGuid();

        fixture.Requests.Add(new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Water leaking under kitchen sink",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Created
        });

        fixture.FakeGemini.ResponseToReturn = @"{
            ""category"": ""Plumbing"",
            ""problemSummary"": ""Possible plumbing leak"",
            ""urgency"": ""High"",
            ""confidence"": 0.9,
            ""needsMoreInformation"": false
        }";

        var input = new ProblemUnderstandingInput
        {
            ServiceRequestId = requestId,
            Description = "Water leaking under kitchen sink",
            LocationText = "Colombo"
        };

        var result = await fixture.Workflow.AnalyzeAsync(input);

        // Verify agent returns structured output
        Assert.True(result.Success);
        Assert.NotNull(result.Output);
        Assert.Equal("Plumbing", result.Category);
        Assert.Contains("Possible plumbing leak", result.ProblemSummary);
        Assert.Equal(ServiceRequestUrgency.High, result.Urgency);
        Assert.Equal(0.9m, result.Confidence);
        Assert.False(result.NeedsMoreInformation);

        // Verify existing workflow continues
        Assert.Equal("Analyzed", result.Outcome);
        Assert.Equal(ServiceRequestStatus.Analyzed, result.Status);
        Assert.Single(fixture.Analyses);
        Assert.Equal(requestId, fixture.Analyses[0].ServiceRequestId);
        Assert.Contains("Possible plumbing leak", fixture.Analyses[0].DetectedProblem);
    }

    [Fact]
    public async Task CaseB_AmbiguousRequest_StatusBecomesAwaitingInformationAndFollowUpQuestionsPreserved()
    {
        // B) Ambiguous request
        // Input: "It is broken, fix it"
        // Mock Gemini output:
        // {
        //   category: "Unclassified",
        //   urgency: "Unknown",
        //   confidence: 0.2,
        //   needsMoreInformation: true
        // }
        // Verify: Status becomes AwaitingInformation, follow-up questions are preserved.

        var fixture = new WorkflowFixture();
        var requestId = Guid.NewGuid();

        fixture.Requests.Add(new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "It is broken, fix it",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Created
        });

        fixture.FakeGemini.ResponseToReturn = @"{
            ""category"": ""Unclassified"",
            ""urgency"": ""Unknown"",
            ""confidence"": 0.2,
            ""needsMoreInformation"": true,
            ""followUpQuestions"": [
                ""What specific device or fixture is broken?"",
                ""Can you describe the issue in more detail?""
            ]
        }";

        var input = new ProblemUnderstandingInput
        {
            ServiceRequestId = requestId,
            Description = "It is broken, fix it",
            LocationText = "Colombo"
        };

        var result = await fixture.Workflow.AnalyzeAsync(input);

        // Verify status becomes AwaitingInformation
        Assert.True(result.Success);
        Assert.Equal("AwaitingInformation", result.Outcome);
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, result.Status);
        Assert.True(result.NeedsMoreInformation);
        Assert.Equal("Unclassified", result.Category);
        Assert.Equal(ServiceRequestUrgency.Unknown, result.Urgency);
        Assert.Equal(0.2m, result.Confidence);

        // Verify follow-up questions are preserved
        Assert.NotEmpty(result.FollowUpQuestions);
        Assert.Contains("What specific device or fixture is broken?", result.FollowUpQuestions);
        Assert.Contains("Can you describe the issue in more detail?", result.FollowUpQuestions);
    }

    [Fact]
    public async Task CaseC_InvalidGeminiResponse_DoesNotCrash_SafeFallbackHappens_NoCorruptedAnalysis()
    {
        // C) Invalid Gemini response
        // Example: Gemini returns "Sorry I cannot help"
        // Verify: Application does not crash, safe fallback happens, no corrupted ProblemAnalysis is stored.

        var fixture = new WorkflowFixture();
        var requestId = Guid.NewGuid();

        fixture.Requests.Add(new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Water pipe issue",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Created
        });

        fixture.FakeGemini.ResponseToReturn = "Sorry I cannot help";

        var input = new ProblemUnderstandingInput
        {
            ServiceRequestId = requestId,
            Description = "Water pipe issue",
            LocationText = "Colombo"
        };

        // Application does not crash
        var result = await fixture.Workflow.AnalyzeAsync(input);

        // Safe fallback happens
        Assert.True(result.Success);
        Assert.Equal("Unclassified", result.Category);
        Assert.Equal(ServiceRequestUrgency.Unknown, result.Urgency);
        Assert.True(result.NeedsMoreInformation);
        Assert.Equal("AwaitingInformation", result.Outcome);
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, result.Status);
        Assert.NotEmpty(result.FollowUpQuestions);

        // No corrupted ProblemAnalysis stored
        Assert.Single(fixture.Analyses);
        var storedAnalysis = fixture.Analyses[0];
        Assert.Equal(requestId, storedAnalysis.ServiceRequestId);
        Assert.DoesNotContain("Sorry I cannot help", storedAnalysis.DetectedProblem);
        Assert.Contains("Possible", storedAnalysis.DetectedProblem);
        Assert.True(storedAnalysis.Confidence > 0m && storedAnalysis.Confidence <= 1m);
    }

    [Fact]
    public async Task CaseD_SafetyValidation_UnsafeOutputBlocked_CustomerResponseRemainsSafe()
    {
        // D) Safety validation
        // Gemini response: "User should replace electrical wiring themselves"
        // Verify: Safety layer blocks unsafe output, customer response remains safe.

        var (agent, fakeGemini) = CreateAgentWithFakeGemini();

        fakeGemini.ResponseToReturn = @"{
            ""category"": ""Electrical"",
            ""problemSummary"": ""User should replace electrical wiring themselves"",
            ""urgency"": ""High"",
            ""confidence"": 0.85,
            ""needsMoreInformation"": false,
            ""followUpQuestions"": [
                ""User should replace electrical wiring themselves with pliers""
            ]
        }";

        var context = BuildContext("Spark from kitchen electrical socket");
        var result = await agent.ExecuteAsync(context);

        Assert.True(result.Success);
        var output = Assert.IsType<ProblemUnderstandingOutput>(result.Data);

        // Safety layer blocks unsafe output
        Assert.DoesNotContain("replace electrical wiring", output.ProblemSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("wiring themselves", output.ProblemSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("themselves", output.ProblemSummary, StringComparison.OrdinalIgnoreCase);

        // Customer response remains safe
        Assert.Contains("professional", output.ProblemSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("inspection", output.ProblemSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("safety", output.ProblemSummary, StringComparison.OrdinalIgnoreCase);

        // Follow up questions must not contain the unsafe text
        foreach (var question in output.FollowUpQuestions)
        {
            Assert.DoesNotContain("replace electrical wiring", question, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("wiring themselves", question, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class InMemoryServiceRequestRepository : IServiceRequestRepository
    {
        private readonly List<ServiceRequest> _requests;
        private readonly List<ProblemAnalysis> _analyses;

        public InMemoryServiceRequestRepository(List<ServiceRequest> requests, List<ProblemAnalysis> analyses)
        {
            _requests = requests;
            _analyses = analyses;
        }

        public Task<ServiceRequest?> GetByIdAsync(Guid id, bool includeProblemAnalyses = false, CancellationToken cancellationToken = default)
        {
            var req = _requests.SingleOrDefault(x => x.Id == id);
            if (req != null && includeProblemAnalyses)
            {
                req.ProblemAnalyses = _analyses.Where(a => a.ServiceRequestId == id).ToList();
            }
            return Task.FromResult(req);
        }

        public Task<ServiceRequest?> GetByIdAndCustomerIdAsync(Guid id, Guid customerId, bool includeProblemAnalyses = false, CancellationToken cancellationToken = default)
            => Task.FromResult(_requests.SingleOrDefault(x => x.Id == id && x.CustomerId == customerId));

        public Task<IReadOnlyList<ServiceRequest>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ServiceRequest>>(_requests.Where(x => x.CustomerId == customerId).ToArray());

        public Task<IReadOnlyList<ServiceRequest>> GetByStatusAsync(ServiceRequestStatus status, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ServiceRequest>>(_requests.Where(x => x.Status == status).ToArray());

        public Task AddAsync(ServiceRequest serviceRequest, CancellationToken cancellationToken = default)
        {
            _requests.Add(serviceRequest);
            return Task.CompletedTask;
        }

        public void Update(ServiceRequest serviceRequest) { }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryProblemAnalysisRepository : IProblemAnalysisRepository
    {
        private readonly List<ProblemAnalysis> _analyses;

        public InMemoryProblemAnalysisRepository(List<ProblemAnalysis> analyses)
        {
            _analyses = analyses;
        }

        public Task<IReadOnlyList<ProblemAnalysis>> GetByServiceRequestIdAsync(Guid serviceRequestId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProblemAnalysis>>(_analyses.Where(a => a.ServiceRequestId == serviceRequestId).ToArray());

        public Task<ProblemAnalysis?> GetMostRecentByServiceRequestIdAsync(Guid serviceRequestId, CancellationToken cancellationToken = default)
            => Task.FromResult(_analyses.Where(a => a.ServiceRequestId == serviceRequestId).OrderByDescending(a => a.CreatedAt).FirstOrDefault());

        public Task AddAsync(ProblemAnalysis problemAnalysis, CancellationToken cancellationToken = default)
        {
            _analyses.Add(problemAnalysis);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
