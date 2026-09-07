using System.Text.Json;
using AssistLK.Agents.Core;
using AssistLK.Agents.Models;
using AssistLK.Application.Services;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.IntegrationTests;

public class Component1MemoryTests
{
    private static AssistLKDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AssistLKDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AssistLKDbContext(options);
    }

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
