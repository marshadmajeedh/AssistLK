using AssistLK.Agents.Adapters;
using AssistLK.Agents.Core;
using AssistLK.Agents.DTOs;
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

// What this class tests:
//   Multi-round clarification workflow rules for Component 1 against a real PostgreSQL database,
//   including question generation, answer persistence, capping at round 2, description updates
//   superseding questions, answer submission locks, and database unique constraints.
// Why these tests are needed:
//   Clarifications allow the AI problem understanding agent to collect missing details before
//   matching providers. These tests verify that the system enforces the 2-round limit, preserves audit
//   history, prevents duplicate sequence numbers, and stops race conditions when analyzing.
// What is real and what is mocked/faked:
//   - Real: PostgreSQL database (schema, tables, constraints, foreign keys, EF Core repositories).
//   - Real: ServiceRequestService, ProblemUnderstandingWorkflowService, AgentOrchestrator.
//   - Mocked/Faked: ExternalProblemUnderstandingAgentAdapter uses FakeProblemUnderstandingClient to return controlled AI payloads without live LLM calls.
// Prerequisites:
//   A running PostgreSQL instance accessible via connection string configured in appsettings / environment variables.
// Run from repository root:
//   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.PostgreSql.ClarificationWorkflowPostgreSqlTests"
public class ClarificationWorkflowPostgreSqlTests : PostgreSqlIntegrationTestBase
{
    public ClarificationWorkflowPostgreSqlTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    private ProblemUnderstandingWorkflowService CreateWorkflowService(AssistLKDbContext context, FakeProblemUnderstandingClient? client = null)
    {
        var workflowService = new AgentWorkflowService(context);
        var memoryService = new AgentMemoryService(context);
        var contextService = new AgentContextService(memoryService);
        var monitoringService = new AgentMonitoringService(context);
        var safetyService = new AgentSafetyService(new AgentSafetyPolicyEngine(), context);

        var fakeClient = client ?? new FakeProblemUnderstandingClient();
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
            requestService,
            new AssistLK.Application.Attachments.ProblemVisualEvidenceService(
                new AssistLK.Infrastructure.Repositories.AnalysisEvidenceRepository(context),
                new AssistLK.Tests.Shared.FakeAttachmentStorage()));
    }

    // Purpose: Checks that round 2 final analysis incorporates customer answers from round 1 and round 2 without creating an invalid round 3.
    // Why: AssistLK strictly limits clarifications to 2 rounds to prevent endless conversational loops before provider matching.
    // Scenario: Analyzing across round 1, answering round 1, analyzing round 2, answering round 2 (or attempting re-analysis without answering), and verifying round 3 questions are never persisted.
    // Pass condition: Clarification history passes through rounds 1 and 2, unanswered round 2 re-analysis throws ConflictException, and final analysis produces at most 2 clarification rounds in PostgreSQL.
    // Note: Parameterized xUnit Theory. Running this command executes all 3 InlineData variations.
    // Run from repository root:
    //   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.PostgreSql.ClarificationWorkflowPostgreSqlTests.Round2_FinalAnalysisProcessesAnswers_WithoutCreatingRound3"
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task Round2_FinalAnalysisProcessesAnswers_WithoutCreatingRound3(
        bool finalNeedsInformation, bool answerRound2)
    {
        var customer = await CreateUserAsync(email: $"round2-{Guid.NewGuid():N}@assistlk.com");
        var requestId = Guid.NewGuid();
        await using (var context = CreateDbContext())
        {
            context.ServiceRequests.Add(new ServiceRequest
            {
                Id = requestId, CustomerId = customer.Id,
                Description = "Something is wrong with my refrigerator.",
                LocationText = "Colombo", Category = "Unclassified",
                Status = ServiceRequestStatus.Created
            });
            await context.SaveChangesAsync();
        }

        var calls = 0;
        var fake = new FakeProblemUnderstandingClient
        {
            CustomHandler = input =>
            {
                calls++;
                var history = input.Input.ClarificationHistory;
                Assert.Equal(calls - 1, history.Count);
                Assert.Equal(Enumerable.Range(1, calls - 1), history.Select(c => c.Round));
                Assert.All(history, c => Assert.False(string.IsNullOrWhiteSpace(c.Answer)));
                var needsInformation = calls < 3 || finalNeedsInformation;
                return new AgentExecutionResponseDto
                {
                    RequestId = input.RequestId, Success = true,
                    Result = new ProblemUnderstandingOutputPayloadDto
                    {
                        Category = "Appliance Repair", Urgency = "Medium", Confidence = 0.85m,
                        ProblemSummary = "Possible refrigerator fault based on the supplied symptoms.",
                        NeedsMoreInformation = needsInformation,
                        FollowUpQuestions = needsInformation ? new List<string> { $"Question after analysis {calls}?" } : new List<string>()
                    }
                };
            }
        };

        for (var round = 1; round <= 2; round++)
        {
            await using var context = CreateDbContext();
            var workflow = CreateWorkflowService(context, fake);
            var result = await workflow.AnalyzeAsync(requestId, customer.Id);
            Assert.True(result.Success);
            Assert.Equal(ServiceRequestStatus.AwaitingInformation, result.Status);
            var service = new ServiceRequestService(new ServiceRequestRepository(context), new ProblemAnalysisRepository(context));
            var request = await service.GetByIdAsync(requestId, customer.Id);
            var question = Assert.Single(request.Clarifications.Where(c => c.ClarificationRound == round));
            Assert.Null(question.Answer);
            if (round == 2 && !answerRound2)
            {
                await Assert.ThrowsAsync<ConflictException>(() => workflow.AnalyzeAsync(requestId, customer.Id));
                Assert.Equal(2, calls);
                return;
            }
            var answers = await service.SubmitClarificationAnswersAsync(customer.Id, requestId,
                new SubmitClarificationAnswersRequest
                {
                    ClarificationRound = round,
                    Answers = new() { new() { ClarificationId = question.Id, Answer = $"Symptoms for round {round}" } }
                });
            Assert.Contains(answers, c => c.Id == question.Id && c.Answer == $"Symptoms for round {round}" && c.AnsweredAt != null);
        }

        await using (var context = CreateDbContext())
        {
            var result = await CreateWorkflowService(context, fake).AnalyzeAsync(requestId, customer.Id);
            Assert.True(result.Success);
            Assert.Equal(finalNeedsInformation ? ServiceRequestStatus.AwaitingInformation : ServiceRequestStatus.Analyzed, result.Status);
            Assert.Equal(3, calls);
        }
        await using (var context = CreateDbContext())
        {
            var service = new ServiceRequestService(new ServiceRequestRepository(context), new ProblemAnalysisRepository(context));
            var request = await service.GetByIdAsync(requestId, customer.Id);
            Assert.Equal(finalNeedsInformation ? ServiceRequestStatus.AwaitingInformation : ServiceRequestStatus.Analyzed, request.Status);
            Assert.Equal(2, request.Clarifications.Count);
            Assert.Equal(new[] { 1, 2 }, request.Clarifications.Select(c => c.ClarificationRound));
            Assert.All(request.Clarifications, c =>
            {
                Assert.Equal($"Symptoms for round {c.ClarificationRound}", c.Answer);
                Assert.Null(c.SupersededAt);
                Assert.True(request.LatestAnalysis!.CreatedAt > c.AnsweredAt);
            });
            Assert.Equal(3, await context.ProblemAnalyses.CountAsync(a => a.ServiceRequestId == requestId));
        }
    }

    // Purpose: Checks that submitting answers to round 1 questions preserves answers in PostgreSQL and reloading confirms the round is fully answered with no pending questions.
    // Why: Ensures customer answers are committed reliably to PostgreSQL and the application correctly detects round completion.
    // Scenario: Customer answers all round 1 questions; request is reloaded via ServiceRequestService.
    // Pass condition: Answers are persisted, currentRound is 1, fullyAnswered is true, and pending questions list is empty.
    // Run from repository root:
    //   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.ClarificationWorkflowPostgreSqlTests.ScenarioA_B_Round1FullyAnswered_ReloadPreservesAnswers_AndCurrentRoundIsRound1"
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

    // Purpose: Checks that triggering AI re-analysis while active clarification questions remain unanswered is rejected with ConflictException.
    // Why: Prevents re-analyzing before the customer responds to the questions the model requested.
    // Scenario: Service request is in AwaitingInformation with an unanswered round 1 question; AnalyzeAsync is called.
    // Pass condition: Throws ConflictException with message "Active clarification questions must be answered".
    // Run from repository root:
    //   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.ClarificationWorkflowPostgreSqlTests.ScenarioC_AnalyzeInAwaitingInformation_WithUnansweredActiveQuestions_IsRejected"
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

    // Purpose: Checks that legacy or inconsistent requests in AwaitingInformation with zero clarification rows can safely be re-analyzed to generate fresh round 1 questions.
    // Why: Provides backward compatibility and self-healing if a prior run set status to AwaitingInformation without creating question records.
    // Scenario: Request exists in AwaitingInformation with zero clarification rows; workflow AnalyzeAsync is executed.
    // Pass condition: Analysis succeeds, status remains AwaitingInformation, and Round 1 clarification rows are generated in PostgreSQL.
    // Run from repository root:
    //   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.ClarificationWorkflowPostgreSqlTests.ScenarioD_LegacyAwaitingInformation_WithZeroClarifications_HasSafeReanalyzePath"
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

    // Purpose: Checks that customer description revisions supersede active questions with a timestamp without deleting them, allowing re-analysis and progression to ReadyForMatching.
    // Why: If a customer clarifies their description directly, older questions become obsolete; marking them with SupersededAt preserves audit logs while unblocking workflow progression.
    // Scenario: Customer updates description on an AwaitingInformation request with unanswered questions; verifies question audit, executes re-analysis, and marks ready for matching.
    // Pass condition: Question has null Answer and non-null SupersededAt, re-analysis succeeds without answering the old question, and MarkReadyForMatchingAsync succeeds.
    // Run from repository root:
    //   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.ClarificationWorkflowPostgreSqlTests.ScenarioE_F_G_H_DescriptionChange_SupersedesQuestions_PreservesAudit_AllowsReanalyze_DoesNotBlockReadyForMatching"
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
                Latitude = 6.9271m,
                Longitude = 79.8612m,
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
                LocationText = "Colombo",
                Latitude = 6.9271m,
                Longitude = 79.8612m
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

    // Purpose: Checks that submitting clarification answers is idempotent on retry and is strictly locked once the request transitions to Analyzing status.
    // Why: Network issues may cause duplicate submission attempts; duplicate answers must not corrupt the DB. Furthermore, once AI analysis begins, modifying answers must be rejected to prevent race conditions.
    // Scenario: Customer submits answers, resubmits identical payload, and subsequently attempts submission after BeginAnalysisAsync moves status to Analyzing.
    // Pass condition: First and retry submissions succeed without creating duplicate clarification records, and submission during Analyzing throws ConflictException.
    // Run from repository root:
    //   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.ClarificationWorkflowPostgreSqlTests.ScenarioI_J_AnswerSubmission_IdempotentRetry_AndLockedWhenAnalyzing"
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

    // Purpose: Checks that when maximum round 2 is reached, an AI result indicating NeedsMoreInformation = true does not persist any Round 3 questions.
    // Why: Business rule enforcement that prevents infinite clarification cycles with customers.
    // Scenario: Service request has completed and answered round 1 and round 2; problem analysis is applied with NeedsMoreInformation = true and candidate questions.
    // Pass condition: Exactly 2 clarification rows remain in DB and none exist with ClarificationRound >= 3.
    // Run from repository root:
    //   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.ClarificationWorkflowPostgreSqlTests.ScenarioK_MaxRound2Reached_NeedsMoreInformation_DoesNotCreateRound3"
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

    // Purpose: Checks that fetching a service request returns authentic persisted ProblemAnalysis values rather than fabricated or default placeholders.
    // Why: Ensures downstream consumers and customers see real analysis confidence, detected problem, and agent metadata stored in PostgreSQL.
    // Scenario: ProblemAnalysis is seeded with DetectedProblem, Confidence = 0.83, and AgentName; request is reloaded.
    // Pass condition: LatestAnalysis fields match the persisted entity (Confidence == 0.83, not hardcoded 0.90).
    // Run from repository root:
    //   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.ClarificationWorkflowPostgreSqlTests.ScenarioL_M_AuthoritativeLatestAnalysisRestoration_NoFabricatedValues"
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

    // Purpose: Checks that PostgreSQL enforces a unique constraint on (ServiceRequestId, ClarificationRound, Sequence) to prevent duplicate question sequencing.
    // Why: Database-level safety net ensuring clarification questions retain deterministic ordering and uniqueness per round.
    // Scenario: Inserting two ServiceRequestClarification rows with the same ServiceRequestId, ClarificationRound (1), and Sequence (1).
    // Pass condition: SaveChangesAsync throws DbUpdateException caused by PostgresException SQL state 23505 (unique_violation).
    // Run from repository root:
    //   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.ClarificationWorkflowPostgreSqlTests.ScenarioN_UniqueConstraint_PreventsDuplicateRoundAndSequence"
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
