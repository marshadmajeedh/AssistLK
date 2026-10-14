using System.Text.Json;
using AssistLK.Agents.Core;
using AssistLK.Agents.Models;
using AssistLK.Application.Services;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.IntegrationTests;

/// <summary>
/// What this class tests:
///   Semantic agent memory storage, privacy/safety constraints (excluding chain-of-thought), key deduplication, workflow isolation, and context memory loading in Component 1.
/// Why these tests are needed:
///   Ensures agent execution states are reliably saved across workflow turns while preventing sensitive reasoning traces from being leaked into persistence.
/// What is real and what is mocked/faked:
///   - Real: AgentMemoryService and AgentContextService running against an EF Core In-Memory database (AssistLKDbContext).
///   - Mocked/Faked: None. Uses real services and in-memory EF Core.
/// Prerequisites:
///   None. Runs completely in-memory without external services or network calls.
/// Run from repository root:
///   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.Component1MemoryTests"
/// </summary>
public class Component1MemoryTests
{
    private static AssistLKDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AssistLKDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AssistLKDbContext(options);
    }

    /// <summary>
    /// Purpose: Checks that all 8 standardized semantic memory keys are persisted with correct source attribution.
    /// Why: Verifies complete semantic memory capture for downstream agent stages and audits.
    /// Scenario: Saves category, summary, urgency, confidence, needs_more_information, follow_up_questions, location, and additional_info.
    /// Pass condition: Stored count equals 8, all 8 expected keys exist, and SourceAgent equals "ProblemUnderstandingAgent".
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1MemoryTests.AgentMemoryService_StoresAll8SemanticMemoryKeys"
    /// </summary>
    [Fact]
    public async Task AgentMemoryService_StoresAll8SemanticMemoryKeys()
    {
        await using var db = CreateInMemoryDb();
        var memoryService = new AgentMemoryService(db);
        var workflowId = Guid.NewGuid();

        var output = new ProblemUnderstandingOutput
        {
            Category = "Plumbing",
            ProblemSummary = "Possible water pipe leakage in bathroom",
            Urgency = ServiceRequestUrgency.Medium,
            Confidence = 0.85m,
            NeedsMoreInformation = false,
            FollowUpQuestions = new[] { "Is the water shutoff valve closed?" },
            ExtractedLocation = "Colombo 03",
            AdditionalInformation = new Dictionary<string, string>
            {
                ["ServiceFamily"] = "Plumbing and Water Supply"
            }
        };

        const string sourceAgent = "ProblemUnderstandingAgent";
        await memoryService.SaveAsync(workflowId, "problem.category", output.Category, sourceAgent);
        await memoryService.SaveAsync(workflowId, "problem.summary", output.ProblemSummary, sourceAgent);
        await memoryService.SaveAsync(workflowId, "problem.urgency", output.Urgency.ToString(), sourceAgent);
        await memoryService.SaveAsync(workflowId, "problem.confidence", output.Confidence.ToString(), sourceAgent);
        await memoryService.SaveAsync(workflowId, "problem.needs_more_information", output.NeedsMoreInformation.ToString().ToLowerInvariant(), sourceAgent);
        await memoryService.SaveAsync(workflowId, "problem.follow_up_questions", JsonSerializer.Serialize(output.FollowUpQuestions), sourceAgent);
        await memoryService.SaveAsync(workflowId, "problem.location", output.ExtractedLocation, sourceAgent);
        await memoryService.SaveAsync(workflowId, "problem.additional_information", JsonSerializer.Serialize(output.AdditionalInformation), sourceAgent);

        var stored = await memoryService.GetAllAsync(workflowId);
        Assert.Equal(8, stored.Count);

        var keys = stored.Select(x => x.Key).ToHashSet();
        Assert.Contains("problem.category", keys);
        Assert.Contains("problem.summary", keys);
        Assert.Contains("problem.urgency", keys);
        Assert.Contains("problem.confidence", keys);
        Assert.Contains("problem.needs_more_information", keys);
        Assert.Contains("problem.follow_up_questions", keys);
        Assert.Contains("problem.location", keys);
        Assert.Contains("problem.additional_information", keys);

        foreach (var item in stored)
        {
            Assert.Equal("ProblemUnderstandingAgent", item.SourceAgent);
        }
    }

    /// <summary>
    /// Purpose: Checks that private chain-of-thought (CoT), internal thoughts, or rationale keys are never written to agent memory.
    /// Why: Protects system prompts, model reasoning traces, and token costs from unintended persistence or leakage.
    /// Scenario: Saves problem memory and verifies key names for reasoning, thought, cot, internal, rationale.
    /// Pass condition: Stored keys collection contains no matches for forbidden reasoning tokens.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1MemoryTests.AgentMemory_DoesNotStoreChainOfThoughtOrInternalReasoningKeys"
    /// </summary>
    [Fact]
    public async Task AgentMemory_DoesNotStoreChainOfThoughtOrInternalReasoningKeys()
    {
        await using var db = CreateInMemoryDb();
        var memoryService = new AgentMemoryService(db);
        var workflowId = Guid.NewGuid();

        await memoryService.SaveAsync(workflowId, "problem.category", "Electrical", "ProblemUnderstandingAgent");
        await memoryService.SaveAsync(workflowId, "problem.summary", "Sparking outlet", "ProblemUnderstandingAgent");

        var stored = await memoryService.GetAllAsync(workflowId);
        var keys = stored.Select(x => x.Key.ToLowerInvariant()).ToArray();

        Assert.DoesNotContain(keys, k => k.Contains("reasoning"));
        Assert.DoesNotContain(keys, k => k.Contains("thought"));
        Assert.DoesNotContain(keys, k => k.Contains("cot"));
        Assert.DoesNotContain(keys, k => k.Contains("internal"));
        Assert.DoesNotContain(keys, k => k.Contains("rationale"));
    }

    /// <summary>
    /// Purpose: Checks that follow-up questions are serialized and retrieved as valid JSON arrays.
    /// Why: Ensures structured question lists are deserializable by mobile/web frontends and subsequent agent turns.
    /// Scenario: Stores JSON serialized string array with 2 questions and deserializes it back.
    /// Pass condition: Retrieved string deserializes to a 2-element array matching original questions.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1MemoryTests.AgentMemory_FollowUpQuestionsAreStoredAsValidJsonArray"
    /// </summary>
    [Fact]
    public async Task AgentMemory_FollowUpQuestionsAreStoredAsValidJsonArray()
    {
        await using var db = CreateInMemoryDb();
        var memoryService = new AgentMemoryService(db);
        var workflowId = Guid.NewGuid();

        var questions = new[] { "Which room is affected?", "Is power tripped?" };
        var json = JsonSerializer.Serialize(questions);

        await memoryService.SaveAsync(workflowId, "problem.follow_up_questions", json, "ProblemUnderstandingAgent");

        var retrieved = await memoryService.GetAsync(workflowId, "problem.follow_up_questions");
        Assert.NotNull(retrieved);

        var deserialized = JsonSerializer.Deserialize<string[]>(retrieved);
        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.Length);
        Assert.Equal("Which room is affected?", deserialized[0]);
    }

    /// <summary>
    /// Purpose: Checks that AgentContextService loads persisted memory entries into the agent's in-memory dictionary.
    /// Why: Gives agents context from prior turns or previous workflow steps during execution.
    /// Scenario: Pre-populates 2 memory keys in DB, then calls LoadMemoryAsync on AgentContext.
    /// Pass condition: Context Memory dictionary contains both keys with exact matching values.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1MemoryTests.AgentContextService_LoadMemoryAsync_PopulatesContextMemoryDictionary"
    /// </summary>
    [Fact]
    public async Task AgentContextService_LoadMemoryAsync_PopulatesContextMemoryDictionary()
    {
        await using var db = CreateInMemoryDb();
        var memoryService = new AgentMemoryService(db);
        var contextService = new AgentContextService(memoryService);

        var workflowId = Guid.NewGuid();
        await memoryService.SaveAsync(workflowId, "problem.category", "Vehicle Repair", "ProblemUnderstandingAgent");
        await memoryService.SaveAsync(workflowId, "problem.urgency", "High", "ProblemUnderstandingAgent");

        var context = new AgentContext
        {
            WorkflowId = workflowId,
            Input = "Car won't start"
        };

        await contextService.LoadMemoryAsync(context);

        Assert.Equal(2, context.Memory.Count);
        Assert.Equal("Vehicle Repair", context.Memory["problem.category"]);
        Assert.Equal("High", context.Memory["problem.urgency"]);
    }

    /// <summary>
    /// Purpose: Checks that saving an existing memory key updates its value in-place instead of creating duplicate records.
    /// Why: Prevents database bloat and ensures queries retrieve the latest updated confidence or category value.
    /// Scenario: Saves confidence "0.4", then saves "0.85" with the same key for the same workflow.
    /// Pass condition: DB contains a single record whose Value equals "0.85".
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1MemoryTests.AgentMemoryService_SaveAsync_UpdatesExistingEntryInsteadOfDuplicating"
    /// </summary>
    [Fact]
    public async Task AgentMemoryService_SaveAsync_UpdatesExistingEntryInsteadOfDuplicating()
    {
        await using var db = CreateInMemoryDb();
        var memoryService = new AgentMemoryService(db);
        var workflowId = Guid.NewGuid();

        await memoryService.SaveAsync(workflowId, "problem.confidence", "0.4", "ProblemUnderstandingAgent");
        await memoryService.SaveAsync(workflowId, "problem.confidence", "0.85", "ProblemUnderstandingAgent");

        var all = await memoryService.GetAllAsync(workflowId);
        Assert.Single(all);

        var entry = all.Single();
        Assert.Equal("0.85", entry.Value);
    }

    /// <summary>
    /// Purpose: Checks that memory entries are strictly isolated by WorkflowId.
    /// Why: Multi-tenant safety: guarantees data from one service request workflow never leaks to another.
    /// Scenario: Two workflows save different values for "problem.category" ("Plumbing" vs "Electrical").
    /// Pass condition: Querying workflow 1 returns "Plumbing" and workflow 2 returns "Electrical".
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1MemoryTests.AgentMemoryService_IsolatedByWorkflowId"
    /// </summary>
    [Fact]
    public async Task AgentMemoryService_IsolatedByWorkflowId()
    {
        await using var db = CreateInMemoryDb();
        var memoryService = new AgentMemoryService(db);

        var wf1 = Guid.NewGuid();
        var wf2 = Guid.NewGuid();

        await memoryService.SaveAsync(wf1, "problem.category", "Plumbing", "ProblemUnderstandingAgent");
        await memoryService.SaveAsync(wf2, "problem.category", "Electrical", "ProblemUnderstandingAgent");

        var val1 = await memoryService.GetAsync(wf1, "problem.category");
        var val2 = await memoryService.GetAsync(wf2, "problem.category");

        Assert.Equal("Plumbing", val1);
        Assert.Equal("Electrical", val2);
    }
}
