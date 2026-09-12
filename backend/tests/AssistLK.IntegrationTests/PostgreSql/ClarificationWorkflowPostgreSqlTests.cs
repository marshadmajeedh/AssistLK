using AssistLK.Agents.Adapters;
using AssistLK.Agents.Agents;
using AssistLK.Agents.Core;
using AssistLK.Agents.Services;
using AssistLK.Agents.Tools;
using AssistLK.Application.Common.Exceptions;
using AssistLK.Application.ServiceRequests.DTOs;
using AssistLK.Application.Services;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using AssistLK.Infrastructure.Repositories;
using AssistLK.IntegrationTests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssistLK.IntegrationTests.PostgreSql;

public class ClarificationWorkflowPostgreSqlTests : PostgreSqlIntegrationTestBase
{
    public ClarificationWorkflowPostgreSqlTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    private ProblemUnderstandingWorkflowService CreateWorkflowService(AssistLKDbContext context)
    {
        var workflowService = new AgentWorkflowService(context);
        var memoryService = new AgentMemoryService(context);
        var contextService = new AgentContextService(memoryService);
        var monitoringService = new AgentMonitoringService(context);
        var safetyService = new AgentSafetyService(new AgentSafetyPolicyEngine(), context);

        var fakeClient = new FakeProblemUnderstandingClient();
        var adapter = new ExternalProblemUnderstandingAgentAdapter(
            fakeClient,
            NullLogger<ExternalProblemUnderstandingAgentAdapter>.Instance);

        var registry = new AgentRegistry();
        registry.Register(adapter);

        var orchestrator = new AgentOrchestrator(registry);

        var requestRepo = new ServiceRequestRepository(context);
        var analysisRepo = new ProblemAnalysisRepository(context);
        var requestService = new ServiceRequestService(requestRepo, analysisRepo);

        return new ProblemUnderstandingWorkflowService(
            workflowService,
            contextService,
            memoryService,
            monitoringService,
            safetyService,
            orchestrator,
            registry,
            requestService);
    }

    [Fact]
    public async Task ScenarioA_B_Round1FullyAnswered_ReloadPreservesAnswers_AndCurrentRoundIsRound1()
    {
        var customer = await CreateUserAsync(email: "scenario_ab@assistlk.com");
        var requestId = Guid.NewGuid();
        var q1Id = Guid.NewGuid();
        var q2Id = Guid.NewGuid();

        await using (var context = CreateDbContext())
        {
            var req = new ServiceRequest
            {
                Id = requestId,
                CustomerId = customer.Id,
                Category = "Appliance Repair",
                Description = "refrigerator problem",
                LocationText = "Colombo",
                Status = ServiceRequestStatus.AwaitingInformation
            };
            await context.ServiceRequests.AddAsync(req);

            await context.ServiceRequestClarifications.AddRangeAsync(
                new ServiceRequestClarification
                {
                    Id = q1Id,
                    ServiceRequestId = requestId,
                    ClarificationRound = 1,
                    Sequence = 1,
                    Question = "Is the freezer cold?",
                    Answer = null
                },
                new ServiceRequestClarification
                {
                    Id = q2Id,
                    ServiceRequestId = requestId,
                    ClarificationRound = 1,
                    Sequence = 2,
                    Question = "Is there any noise?",
                    Answer = null
                }
            );

            await context.SaveChangesAsync();
        }

        // Customer submits answers for Round 1
        await using (var context = CreateDbContext())
        {
            var service = new ServiceRequestService(
                new ServiceRequestRepository(context),
                new ProblemAnalysisRepository(context));

            var result = await service.SubmitClarificationAnswersAsync(customer.Id, requestId, new SubmitClarificationAnswersRequest
            {
                ClarificationRound = 1,
                Answers = new()
                {
                    new ClarificationAnswerSubmissionItem { ClarificationId = q1Id, Answer = "Yes, freezer is cold." },
                    new ClarificationAnswerSubmissionItem { ClarificationId = q2Id, Answer = "No noise at all." }
                }
            });

            Assert.Equal(2, result.Count);
            Assert.All(result, r => Assert.NotNull(r.Answer));
        }

        // Scenario B: Reload in that state
        await using (var context = CreateDbContext())
        {
            var service = new ServiceRequestService(
                new ServiceRequestRepository(context),
                new ProblemAnalysisRepository(context));

            var reloaded = await service.GetByIdAsync(requestId, customer.Id);

            Assert.Equal(ServiceRequestStatus.AwaitingInformation, reloaded.Status);
            Assert.NotNull(reloaded.Clarifications);
            Assert.Equal(2, reloaded.Clarifications.Count);

            // Current round is MAX(ClarificationRound) = 1
            var currentRound = reloaded.Clarifications.Max(c => c.ClarificationRound);
            Assert.Equal(1, currentRound);

            // currentRoundIsFullyAnswered is true
            var roundClarifications = reloaded.Clarifications.Where(c => c.ClarificationRound == currentRound).ToList();
            var fullyAnswered = roundClarifications.All(c => !string.IsNullOrWhiteSpace(c.Answer));
            Assert.True(fullyAnswered);

            // pendingQuestions is empty
            var pending = roundClarifications.Where(c => string.IsNullOrWhiteSpace(c.Answer) && c.SupersededAt == null).ToList();
            Assert.Empty(pending);
        }
    }

    [Fact]
    public async Task ScenarioC_AnalyzeInAwaitingInformation_WithUnansweredActiveQuestions_IsRejected()
    {
        var customer = await CreateUserAsync(email: "scenario_c@assistlk.com");
        var requestId = Guid.NewGuid();

        await using (var context = CreateDbContext())
        {
            var req = new ServiceRequest
            {
                Id = requestId,
                CustomerId = customer.Id,
                Category = "Appliance Repair",
                Description = "refrigerator",
                LocationText = "Kandy",
                Status = ServiceRequestStatus.AwaitingInformation
            };
            await context.ServiceRequests.AddAsync(req);

            await context.ServiceRequestClarifications.AddAsync(new ServiceRequestClarification
            {
                ServiceRequestId = requestId,
                ClarificationRound = 1,
                Sequence = 1,
                Question = "What brand is it?",
                Answer = null
            });

            await context.SaveChangesAsync();
        }

        await using (var context = CreateDbContext())
        {
            var workflowService = CreateWorkflowService(context);
            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                workflowService.AnalyzeAsync(requestId, customer.Id));

            Assert.Contains("Active clarification questions must be answered", ex.Message);
        }
    }

    [Fact]
    public async Task ScenarioD_LegacyAwaitingInformation_WithZeroClarifications_HasSafeReanalyzePath()
    {
        var customer = await CreateUserAsync(email: "scenario_d@assistlk.com");
        var requestId = Guid.NewGuid();

        // Legacy request in AwaitingInformation with NO clarification rows
        await using (var context = CreateDbContext())
        {
            var req = new ServiceRequest
            {
                Id = requestId,
                CustomerId = customer.Id,
                Category = "Appliance Repair",
                Description = "refrigerator",
                LocationText = "Galle",
                Status = ServiceRequestStatus.AwaitingInformation
            };
            await context.ServiceRequests.AddAsync(req);
            await context.SaveChangesAsync();
        }

        // Re-analyze should safely proceed and generate persisted clarification questions
        await using (var context = CreateDbContext())
        {
            var workflowService = CreateWorkflowService(context);
            var result = await workflowService.AnalyzeAsync(requestId, customer.Id);

            Assert.True(result.Success);
            Assert.Equal(ServiceRequestStatus.AwaitingInformation, result.Status);
            Assert.True(result.NeedsMoreInformation);
            Assert.NotEmpty(result.FollowUpQuestions);
        }

        // Verify that Round 1 clarifications were generated in PostgreSQL
        await using (var context = CreateDbContext())
        {
            var clarifications = await context.ServiceRequestClarifications
                .Where(c => c.ServiceRequestId == requestId)
                .ToListAsync();

            Assert.NotEmpty(clarifications);
            Assert.All(clarifications, c => Assert.Equal(1, c.ClarificationRound));
        }
    }

    [Fact]
    public async Task ScenarioE_F_G_H_DescriptionChange_SupersedesQuestions_PreservesAudit_AllowsReanalyze_DoesNotBlockReadyForMatching()
    {
        var customer = await CreateUserAsync(email: "scenario_efgh@assistlk.com");
        var requestId = Guid.NewGuid();
        var qId = Guid.NewGuid();

        await using (var context = CreateDbContext())
        {
            var req = new ServiceRequest
            {
                Id = requestId,
                CustomerId = customer.Id,
                Category = "Appliance Repair",
                Description = "broken fridge",
                LocationText = "Colombo",
                Status = ServiceRequestStatus.AwaitingInformation
            };
            await context.ServiceRequests.AddAsync(req);

            await context.ServiceRequestClarifications.AddAsync(new ServiceRequestClarification
            {
                Id = qId,
                ServiceRequestId = requestId,
                ClarificationRound = 1,
                Sequence = 1,
                Question = "Is it leaking water?",
                Answer = null,
                SupersededAt = null
            });

            await context.SaveChangesAsync();
        }

        // Scenario E: Customer changes core description
        await using (var context = CreateDbContext())
        {
            var service = new ServiceRequestService(
                new ServiceRequestRepository(context),
                new ProblemAnalysisRepository(context));

            await service.UpdateAsync(customer.Id, requestId, new UpdateServiceRequestRequest
            {
                Description = "The refrigerator compressor makes a buzzing noise and food is warm",
                LocationText = "Colombo"
            });
        }

        // Scenario G: Unanswered questions remain queryable for audit, marked with SupersededAt
        await using (var context = CreateDbContext())
        {
            var q = await context.ServiceRequestClarifications.FindAsync(qId);
            Assert.NotNull(q);
            Assert.Null(q.Answer);
            Assert.NotNull(q.SupersededAt); // Superseded, not deleted!
        }

        // Scenario H: Re-analysis after description revision succeeds without answering superseded question
        await using (var context = CreateDbContext())
        {
            var workflowService = CreateWorkflowService(context);
            var result = await workflowService.AnalyzeAsync(requestId, customer.Id);

            Assert.True(result.Success);
            Assert.Equal(ServiceRequestStatus.Analyzed, result.Status);
        }

        // Scenario F: Superseded unanswered questions do NOT block ReadyForMatching
        await using (var context = CreateDbContext())
        {
            var service = new ServiceRequestService(
                new ServiceRequestRepository(context),
                new ProblemAnalysisRepository(context));

            var ready = await service.MarkReadyForMatchingAsync(requestId, customer.Id);
            Assert.Equal(ServiceRequestStatus.ReadyForMatching, ready.Status);
        }
    }

    [Fact]
    public async Task ScenarioI_J_AnswerSubmission_IdempotentRetry_AndLockedWhenAnalyzing()
    {
        var customer = await CreateUserAsync(email: "scenario_ij@assistlk.com");
        var requestId = Guid.NewGuid();
        var qId = Guid.NewGuid();

        await using (var context = CreateDbContext())
        {
            var req = new ServiceRequest
            {
                Id = requestId,
                CustomerId = customer.Id,
                Category = "Appliance Repair",
                Description = "microwave not heating",
                LocationText = "Colombo",
                Status = ServiceRequestStatus.AwaitingInformation
            };
            await context.ServiceRequests.AddAsync(req);

            await context.ServiceRequestClarifications.AddAsync(new ServiceRequestClarification
            {
                Id = qId,
                ServiceRequestId = requestId,
                ClarificationRound = 1,
                Sequence = 1,
                Question = "Does the turntable spin?",
                Answer = null
            });

            await context.SaveChangesAsync();
        }

        var submission = new SubmitClarificationAnswersRequest
        {
            ClarificationRound = 1,
            Answers = new()
            {
                new ClarificationAnswerSubmissionItem { ClarificationId = qId, Answer = "Yes, it spins normally." }
            }
        };

        // First submission
        await using (var context = CreateDbContext())
        {
            var service = new ServiceRequestService(
                new ServiceRequestRepository(context),
                new ProblemAnalysisRepository(context));

            var res = await service.SubmitClarificationAnswersAsync(customer.Id, requestId, submission);
            Assert.Single(res);
            Assert.Equal("Yes, it spins normally.", res.First().Answer);
        }

        // Scenario I: Identical retry after lost response succeeds without creating duplicates
        await using (var context = CreateDbContext())
        {
            var service = new ServiceRequestService(
                new ServiceRequestRepository(context),
                new ProblemAnalysisRepository(context));

            var res = await service.SubmitClarificationAnswersAsync(customer.Id, requestId, submission);
            Assert.Single(res);
            Assert.Equal("Yes, it spins normally.", res.First().Answer);
        }

        // Verify in DB that only 1 clarification row exists
        await using (var context = CreateDbContext())
        {
            var count = await context.ServiceRequestClarifications
                .CountAsync(c => c.ServiceRequestId == requestId);
            Assert.Equal(1, count);
        }

        // Scenario J: Once status becomes Analyzing, answer modification is rejected
        await using (var context = CreateDbContext())
        {
            var service = new ServiceRequestService(
                new ServiceRequestRepository(context),
                new ProblemAnalysisRepository(context));

            await service.BeginAnalysisAsync(requestId);

            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                service.SubmitClarificationAnswersAsync(customer.Id, requestId, submission));

            Assert.Contains("AwaitingInformation", ex.Message);
        }
    }

    [Fact]
    public async Task ScenarioK_MaxRound2Reached_NeedsMoreInformation_DoesNotCreateRound3()
    {
        var customer = await CreateUserAsync(email: "scenario_k@assistlk.com");
        var requestId = Guid.NewGuid();

        await using (var context = CreateDbContext())
        {
            var req = new ServiceRequest
            {
                Id = requestId,
                CustomerId = customer.Id,
                Category = "Appliance Repair",
                Description = "vague machine",
                LocationText = "Colombo",
                Status = ServiceRequestStatus.AwaitingInformation
            };
            await context.ServiceRequests.AddAsync(req);

            // Seed Round 1 & Round 2 already answered
            await context.ServiceRequestClarifications.AddRangeAsync(
                new ServiceRequestClarification
                {
                    ServiceRequestId = requestId,
                    ClarificationRound = 1,
                    Sequence = 1,
                    Question = "Round 1 question",
                    Answer = "Round 1 answer"
                },
                new ServiceRequestClarification
                {
                    ServiceRequestId = requestId,
                    ClarificationRound = 2,
                    Sequence = 1,
                    Question = "Round 2 question",
                    Answer = "Round 2 answer"
                }
            );

            await context.SaveChangesAsync();
        }

        // Apply analysis with NeedsMoreInformation = true after Round 2
        await using (var context = CreateDbContext())
        {
            var service = new ServiceRequestService(
                new ServiceRequestRepository(context),
                new ProblemAnalysisRepository(context));

            await service.ApplyProblemAnalysisResultAsync(new ApplyProblemAnalysisResult
            {
                ServiceRequestId = requestId,
                Category = "Appliance Repair",
                DetectedProblem = "Still ambiguous after 2 rounds",
                Confidence = 0.50m,
                Urgency = ServiceRequestUrgency.Medium,
                NeedsMoreInformation = true,
                FollowUpQuestions = new[] { "Round 3 candidate question?" },
                AgentName = "ProblemUnderstandingAgent"
            });
        }

        // Verify in DB: No Round 3 questions were generated!
        await using (var context = CreateDbContext())
        {
            var clarifications = await context.ServiceRequestClarifications
                .Where(c => c.ServiceRequestId == requestId)
                .ToListAsync();

            Assert.Equal(2, clarifications.Count);
            Assert.DoesNotContain(clarifications, c => c.ClarificationRound >= 3);

            var req = await context.ServiceRequests.FindAsync(requestId);
            Assert.NotNull(req);
            Assert.Equal(ServiceRequestStatus.AwaitingInformation, req.Status);
        }
    }

    [Fact]
    public async Task ScenarioL_M_AuthoritativeLatestAnalysisRestoration_NoFabricatedValues()
    {
        var customer = await CreateUserAsync(email: "scenario_lm@assistlk.com");
        var requestId = Guid.NewGuid();

        await using (var context = CreateDbContext())
        {
            var req = new ServiceRequest
            {
                Id = requestId,
                CustomerId = customer.Id,
                Category = "Plumbing",
                Description = "Burst pipe in basement",
                LocationText = "Matara",
                Status = ServiceRequestStatus.Analyzed
            };
            await context.ServiceRequests.AddAsync(req);

            var analysis = new ProblemAnalysis
            {
                ServiceRequestId = requestId,
                DetectedProblem = "Major pipe burst causing basement flooding",
                Confidence = 0.83m,
                AgentName = "ProblemUnderstandingAgent"
            };
            await context.ProblemAnalyses.AddAsync(analysis);

            await context.SaveChangesAsync();
        }

        await using (var context = CreateDbContext())
        {
            var service = new ServiceRequestService(
                new ServiceRequestRepository(context),
                new ProblemAnalysisRepository(context));

            var reloaded = await service.GetByIdAsync(requestId, customer.Id);

            Assert.NotNull(reloaded.LatestAnalysis);
            // Scenario L: Persisted values exposed
            Assert.Equal("Major pipe burst causing basement flooding", reloaded.LatestAnalysis.DetectedProblem);
            Assert.Equal(0.83m, reloaded.LatestAnalysis.Confidence);
            Assert.Equal("ProblemUnderstandingAgent", reloaded.LatestAnalysis.AgentName);

            // Scenario M: Not hardcoded 0.90!
            Assert.NotEqual(0.90m, reloaded.LatestAnalysis.Confidence);
        }
    }

    [Fact]
    public async Task ScenarioN_UniqueConstraint_PreventsDuplicateRoundAndSequence()
    {
        var customer = await CreateUserAsync(email: "scenario_n@assistlk.com");
        var requestId = Guid.NewGuid();

        await using (var context = CreateDbContext())
        {
            var req = new ServiceRequest
            {
                Id = requestId,
                CustomerId = customer.Id,
                Category = "Plumbing",
                Description = "Leaking faucet",
                LocationText = "Negombo",
                Status = ServiceRequestStatus.AwaitingInformation
            };
            await context.ServiceRequests.AddAsync(req);

            await context.ServiceRequestClarifications.AddAsync(new ServiceRequestClarification
            {
                ServiceRequestId = requestId,
                ClarificationRound = 1,
                Sequence = 1,
                Question = "Is it dripping constantly?",
                Answer = null
            });

            await context.SaveChangesAsync();
        }

        // Attempt to insert duplicate (ServiceRequestId, ClarificationRound, Sequence)
        await using (var context = CreateDbContext())
        {
            await context.ServiceRequestClarifications.AddAsync(new ServiceRequestClarification
            {
                ServiceRequestId = requestId,
                ClarificationRound = 1,
                Sequence = 1, // Duplicate sequence in Round 1
                Question = "Another duplicate question",
                Answer = null
            });

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            Assert.True(ex.InnerException is Npgsql.PostgresException pEx && pEx.SqlState == "23505" ||
                        (ex.InnerException?.Message.Contains("duplicate key value violates unique constraint") == true));
        }
    }
}
