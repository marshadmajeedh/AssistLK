using System.Text.Json;
using AssistLK.Agents.Core;
using AssistLK.Agents.Models;
using AssistLK.Application.Services;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.IntegrationTests.PostgreSql;

// =========================================================================================
// Class: AgentMemoryPostgreSqlTests
// What this class tests:
//   PostgreSQL persistence and update semantics for AgentMemoryService and AgentWorkflow memory.
// Why these tests are needed:
//   Validates that agent semantic memory keys (category, summary, urgency, confidence, location)
//   persist across separate database contexts, internal LLM Chain of Thought (CoT) strings
//   are omitted, and updating an existing key updates in-place without duplicate rows.
// What is real vs mocked/faked:
//   - REAL: Live PostgreSQL database, EF Core database tables, and AgentMemoryService.
//   - MOCKED: None.
// Prerequisites:
//   A running PostgreSQL database instance configured via ASSISTLK_TEST_POSTGRESQL_CONNECTION,
//   ConnectionStrings__TestConnection, or local User Secrets.
// Run from repository root:
//   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.PostgreSql.AgentMemoryPostgreSqlTests"
// =========================================================================================
public class AgentMemoryPostgreSqlTests : PostgreSqlIntegrationTestBase
{
    public AgentMemoryPostgreSqlTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    private async Task<AgentWorkflow> CreateWorkflowAsync(Guid workflowId)
    {
        await using var context = CreateDbContext();
        var workflow = new AgentWorkflow
        {
            Id = workflowId,
            WorkflowType = "ProblemUnderstanding",
            Status = "Running",
            CurrentAgent = "ProblemUnderstandingAgent",
            Input = "Test Input",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        await context.AgentWorkflows.AddAsync(workflow);
        await context.SaveChangesAsync();
        return workflow;
    }

    // Purpose: Verifies that all 8 standardized semantic memory keys persist in PostgreSQL and contain no internal CoT reasoning keys.
    // Why: Downstream agents read these exact 8 keys from workflow memory; privacy and security rules mandate omitting internal LLM thoughts.
    // Scenario: Saves 8 semantic memory keys in one DbContext scope and reads them back in a second distinct DbContext.
    // Pass condition: Exactly 8 keys exist with expected values, JSON arrays/dictionaries deserialize accurately, and no keys contain "thought", "reasoning", or "cot".
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.AgentMemoryPostgreSqlTests.AgentMemory_Persists8SemanticMemoryKeys_AcrossContextDisposal_InPostgreSql"
    [Fact]
    public async Task AgentMemory_Persists8SemanticMemoryKeys_AcrossContextDisposal_InPostgreSql()
    {
        var workflowId = Guid.NewGuid();
        await CreateWorkflowAsync(workflowId);

        var output = new ProblemUnderstandingOutput
        {
            Category = "Plumbing",
            ProblemSummary = "Water pipe leakage in bathroom",
            Urgency = ServiceRequestUrgency.Medium,
            Confidence = 0.85m,
            NeedsMoreInformation = false,
            FollowUpQuestions = new[] { "Is the main valve closed?" },
            ExtractedLocation = "Colombo 03",
            AdditionalInformation = new Dictionary<string, string>
            {
                ["ServiceFamily"] = "Plumbing and Water Supply"
            }
        };

        const string sourceAgent = "ProblemUnderstandingAgent";

        // Save in first DbContext scope
        await using (var context = CreateDbContext())
        {
            var memoryService = new AgentMemoryService(context);

            await memoryService.SaveAsync(workflowId, "problem.category", output.Category, sourceAgent);
            await memoryService.SaveAsync(workflowId, "problem.summary", output.ProblemSummary, sourceAgent);
            await memoryService.SaveAsync(workflowId, "problem.urgency", output.Urgency.ToString(), sourceAgent);
            await memoryService.SaveAsync(workflowId, "problem.confidence", output.Confidence.ToString(), sourceAgent);
            await memoryService.SaveAsync(workflowId, "problem.needs_more_information", output.NeedsMoreInformation.ToString().ToLowerInvariant(), sourceAgent);
            await memoryService.SaveAsync(workflowId, "problem.follow_up_questions", JsonSerializer.Serialize(output.FollowUpQuestions), sourceAgent);
            await memoryService.SaveAsync(workflowId, "problem.location", output.ExtractedLocation, sourceAgent);
            await memoryService.SaveAsync(workflowId, "problem.additional_information", JsonSerializer.Serialize(output.AdditionalInformation), sourceAgent);
        }

        // Verify in second distinct DbContext scope
        await using (var verifyContext = CreateDbContext())
        {
            var memoryService = new AgentMemoryService(verifyContext);
            var stored = await memoryService.GetAllAsync(workflowId);

            Assert.Equal(8, stored.Count);

            var dict = stored.ToDictionary(x => x.Key, x => x.Value);
            Assert.Equal("Plumbing", dict["problem.category"]);
            Assert.Equal("Water pipe leakage in bathroom", dict["problem.summary"]);
            Assert.Equal("Medium", dict["problem.urgency"]);
            Assert.Equal("0.85", dict["problem.confidence"]);
            Assert.Equal("false", dict["problem.needs_more_information"]);
            Assert.Equal("Colombo 03", dict["problem.location"]);

            var questions = JsonSerializer.Deserialize<string[]>(dict["problem.follow_up_questions"]);
            Assert.NotNull(questions);
            Assert.Single(questions);
            Assert.Equal("Is the main valve closed?", questions[0]);

            var additionalInfo = JsonSerializer.Deserialize<Dictionary<string, string>>(dict["problem.additional_information"]);
            Assert.NotNull(additionalInfo);
            Assert.Equal("Plumbing and Water Supply", additionalInfo["ServiceFamily"]);

            // Ensure no Chain of Thought or internal thought keys exist
            var allKeys = dict.Keys.Select(k => k.ToLowerInvariant()).ToArray();
            Assert.DoesNotContain(allKeys, k => k.Contains("thought") || k.Contains("reasoning") || k.Contains("cot"));
        }
    }

    // Purpose: Verifies that updating an existing key (problem.confidence) updates the value in-place in PostgreSQL rather than inserting duplicate rows.
    // Why: Ensures workflow memory behaves as a true key-value store per workflow ID and prevents database row bloat.
    // Scenario: Saves confidence "0.40", then updates it to "0.95" in a separate DbContext.
    // Pass condition: GetAllAsync returns exactly 1 memory item with updated value "0.95".
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.AgentMemoryPostgreSqlTests.AgentMemory_UpdatesExistingKey_WithoutDuplication_InPostgreSql"
    [Fact]
    public async Task AgentMemory_UpdatesExistingKey_WithoutDuplication_InPostgreSql()
    {
        var workflowId = Guid.NewGuid();
        await CreateWorkflowAsync(workflowId);

        await using (var context = CreateDbContext())
        {
            var memoryService = new AgentMemoryService(context);
            await memoryService.SaveAsync(workflowId, "problem.confidence", "0.40", "ProblemUnderstandingAgent");
        }

        await using (var updateContext = CreateDbContext())
        {
            var memoryService = new AgentMemoryService(updateContext);
            await memoryService.SaveAsync(workflowId, "problem.confidence", "0.95", "ProblemUnderstandingAgent");
        }

        await using (var verifyContext = CreateDbContext())
        {
            var memoryService = new AgentMemoryService(verifyContext);
            var memories = await memoryService.GetAllAsync(workflowId);

            Assert.Single(memories);
            Assert.Equal("0.95", memories.First().Value);
        }
    }
}
