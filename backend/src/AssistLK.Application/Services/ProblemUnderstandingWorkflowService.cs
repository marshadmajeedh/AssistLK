using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using AssistLK.Agents.Agents;
using AssistLK.Agents.Core;
using AssistLK.Agents.Models;
using AssistLK.Application.Common.Exceptions;
using AssistLK.Application.Interfaces;
using AssistLK.Application.ServiceRequests.DTOs;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssistLK.Application.Services;

/// <summary>
/// Workflow service that coordinates the Problem Understanding Agent with the
/// shared agent foundation (workflow tracking, memory, safety, monitoring) and domain services.
/// Does not reference DbContext directly.
/// </summary>
public class ProblemUnderstandingWorkflowService
{
    private readonly AgentWorkflowService _workflowService;
    private readonly AgentContextService _contextService;
    private readonly AgentMemoryService _memoryService;
    private readonly AgentMonitoringService _monitoringService;
    private readonly AgentSafetyService _safetyService;
    private readonly AgentOrchestrator _orchestrator;
    private readonly AgentRegistry _registry;
    private readonly IServiceRequestService _serviceRequestService;
    private readonly ILogger<ProblemUnderstandingWorkflowService> _logger;

    public ProblemUnderstandingWorkflowService(
        AgentWorkflowService workflowService,
        AgentContextService contextService,
        AgentMemoryService memoryService,
        AgentMonitoringService monitoringService,
        AgentSafetyService safetyService,
        AgentOrchestrator orchestrator,
        AgentRegistry registry,
        IServiceRequestService serviceRequestService,
        ILogger<ProblemUnderstandingWorkflowService>? logger = null)
    {
        _workflowService = workflowService;
        _contextService = contextService;
        _memoryService = memoryService;
        _monitoringService = monitoringService;
        _safetyService = safetyService;
        _orchestrator = orchestrator;
        _registry = registry;
        _serviceRequestService = serviceRequestService;
        _logger = logger ?? NullLogger<ProblemUnderstandingWorkflowService>.Instance;
    }

    public async Task<ProblemUnderstandingWorkflowResult> AnalyzeAsync(
        Guid serviceRequestId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        // Enforce customer ownership at the application boundary before beginning analysis or mutating state
        var serviceRequest = await _serviceRequestService.GetByIdAsync(
            serviceRequestId,
            customerId,
            cancellationToken);

        var input = new ProblemUnderstandingInput
        {
            ServiceRequestId = serviceRequest.ServiceRequestId,
            Description = serviceRequest.Description,
            LocationText = serviceRequest.LocationText,
            Latitude = serviceRequest.Latitude,
            Longitude = serviceRequest.Longitude,
            CategoryHint = serviceRequest.CategoryHint
        };

        return await AnalyzeAsync(input, cancellationToken);
    }

    public async Task<ProblemUnderstandingWorkflowResult> AnalyzeAsync(
        ProblemUnderstandingInput input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var workflow = await _workflowService.CreateAsync(
            null,
            "ProblemUnderstanding",
            input.ServiceRequestId.ToString());

        await _workflowService.SetStatusAsync(workflow.Id, "Running");

        // 1. Safety check
        var safetyAction = await _safetyService.CheckActionAsync(
            workflow.Id,
            "ANALYZE_PROBLEM",
            $"Analyze problem for service request {input.ServiceRequestId}");

        if (safetyAction.RequiresApproval)
        {
            await _workflowService.RequestApprovalAsync(workflow.Id, "ANALYZE_PROBLEM");
            return new ProblemUnderstandingWorkflowResult
            {
                Success = false,
                WorkflowId = workflow.Id,
                Outcome = "AwaitingApproval",
                ErrorMessage = "Action requires human approval."
            };
        }

        AgentExecution? execution = null;
        ServiceRequestStatus? preAnalysisStatus = null;
        bool analysisBegun = false;

        try
        {
            // 2. Preserve valid pre-analysis status (Created / AwaitingInformation)
            preAnalysisStatus = await _serviceRequestService.GetPreAnalysisStatusAsync(
                input.ServiceRequestId,
                cancellationToken);

            // 3. Domain state transition: Created/AwaitingInformation -> Analyzing
            await _serviceRequestService.BeginAnalysisAsync(input.ServiceRequestId, cancellationToken);
            analysisBegun = true;

            // 4. Start agent execution
            execution = await _workflowService.StartExecutionAsync(
                workflow.Id,
                "ProblemUnderstandingAgent",
                input);

            // 5. Setup AgentContext and load prior memory if any
            var context = new AgentContext
            {
                WorkflowId = workflow.Id,
                Input = input.Description,
                Data =
                {
                    [nameof(ProblemUnderstandingInput)] = input,
                    ["ServiceRequestId"] = input.ServiceRequestId
                }
            };

            await _contextService.LoadMemoryAsync(context);

            // 6. Execute agent via orchestrator
            var agentResult = await _orchestrator.ExecuteAsync(
                "ProblemUnderstandingAgent",
                context,
                cancellationToken);

            if (!agentResult.Success || agentResult.Data is not ProblemUnderstandingOutput output)
            {
                throw new InvalidOperationException(
                    agentResult.Message ?? "Problem understanding agent execution failed.");
            }

            // 7. Validate output semantics
            if (output.Confidence < 0m || output.Confidence > 1m)
            {
                throw new InvalidOperationException("Agent produced invalid confidence score outside 0-1 range.");
            }

            if (string.IsNullOrWhiteSpace(output.ProblemSummary))
            {
                throw new InvalidOperationException("Agent produced an empty problem summary.");
            }

            // 8. Store semantic memory (concise structured facts only)
            await StoreMemoryAsync(workflow.Id, output);

            // 9. Apply analysis result to domain
            // Persist using CancellationToken.None so that if Gemini/agent execution successfully
            // completes but the HTTP client disconnects before final persistence, the completed
            // analysis is safely preserved rather than discarded solely because RequestAborted is cancelled.
            var applyResult = new ApplyProblemAnalysisResult
            {
                ServiceRequestId = input.ServiceRequestId,
                Category = output.Category,
                DetectedProblem = output.ProblemSummary,
                Confidence = output.Confidence,
                Urgency = output.Urgency,
                NeedsMoreInformation = output.NeedsMoreInformation,
                AgentName = "ProblemUnderstandingAgent"
            };

            await _serviceRequestService.ApplyProblemAnalysisResultAsync(applyResult, CancellationToken.None);

            // 10. Complete workflow & execution
            await _workflowService.CompleteExecutionAsync(execution!.Id, true, output);
            await _workflowService.SetStatusAsync(workflow.Id, "Completed");

            stopwatch.Stop();

            // 11. Record monitoring metrics (read from execution-scoped context data)
            int toolCalls = context.Data.TryGetValue("ToolCallCount", out var tc) && tc is int count ? count : 0;

            await _monitoringService.RecordAsync(
                workflow.Id,
                execution.Id,
                "ProblemUnderstandingAgent",
                "Completed",
                stopwatch.ElapsedMilliseconds,
                toolCalls);

            return new ProblemUnderstandingWorkflowResult
            {
                Success = true,
                WorkflowId = workflow.Id,
                ExecutionId = execution.Id,
                Outcome = output.NeedsMoreInformation ? "AwaitingInformation" : "Analyzed",
                Output = output,
                ServiceRequestId = input.ServiceRequestId,
                Status = output.NeedsMoreInformation ? ServiceRequestStatus.AwaitingInformation : ServiceRequestStatus.Analyzed,
                Category = output.Category,
                ProblemSummary = output.ProblemSummary,
                Urgency = output.Urgency,
                Confidence = output.Confidence,
                NeedsMoreInformation = output.NeedsMoreInformation,
                FollowUpQuestions = output.FollowUpQuestions
            };
        }
        catch (ConflictException)
        {
            stopwatch.Stop();

            if (analysisBegun && preAnalysisStatus.HasValue)
            {
                try
                {
                    await _serviceRequestService.RecoverFailedAnalysisAsync(
                        input.ServiceRequestId,
                        preAnalysisStatus.Value,
                        CancellationToken.None);
                }
                catch (Exception recoveryEx)
                {
                    _logger.LogError(
                        recoveryEx,
                        "Failed to recover service request {ServiceRequestId} back to pre-analysis status {PreAnalysisStatus}.",
                        input.ServiceRequestId,
                        preAnalysisStatus.Value);
                }
            }

            if (execution is not null)
            {
                await _workflowService.CompleteExecutionAsync(execution.Id, false, null);

                await _monitoringService.RecordAsync(
                    workflow.Id,
                    execution.Id,
                    "ProblemUnderstandingAgent",
                    "Failed",
                    stopwatch.ElapsedMilliseconds,
                    0);
            }

            await _workflowService.SetStatusAsync(workflow.Id, "Failed");
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            if (analysisBegun && preAnalysisStatus.HasValue)
            {
                try
                {
                    await _serviceRequestService.RecoverFailedAnalysisAsync(
                        input.ServiceRequestId,
                        preAnalysisStatus.Value,
                        CancellationToken.None);
                }
                catch (Exception recoveryEx)
                {
                    _logger.LogError(
                        recoveryEx,
                        "Failed to recover service request {ServiceRequestId} back to pre-analysis status {PreAnalysisStatus}.",
                        input.ServiceRequestId,
                        preAnalysisStatus.Value);
                }
            }

            if (execution is not null)
            {
                await _workflowService.CompleteExecutionAsync(execution.Id, false, null);
            }

            await _workflowService.SetStatusAsync(workflow.Id, "Failed");

            await _monitoringService.RecordAsync(
                workflow.Id,
                execution?.Id ?? Guid.Empty,
                "ProblemUnderstandingAgent",
                "Failed",
                stopwatch.ElapsedMilliseconds,
                0);

            return new ProblemUnderstandingWorkflowResult
            {
                Success = false,
                WorkflowId = workflow.Id,
                ExecutionId = execution?.Id ?? Guid.Empty,
                Outcome = "Failed",
                ErrorMessage = ex.Message,
                ServiceRequestId = input.ServiceRequestId
            };
        }
    }

    private async Task StoreMemoryAsync(Guid workflowId, ProblemUnderstandingOutput output)
    {
        const string sourceAgent = "ProblemUnderstandingAgent";

        await _memoryService.SaveAsync(workflowId, "problem.category", output.Category, sourceAgent);
        await _memoryService.SaveAsync(workflowId, "problem.summary", output.ProblemSummary, sourceAgent);
        await _memoryService.SaveAsync(workflowId, "problem.urgency", output.Urgency.ToString(), sourceAgent);
        await _memoryService.SaveAsync(workflowId, "problem.confidence", output.Confidence.ToString(CultureInfo.InvariantCulture), sourceAgent);
        await _memoryService.SaveAsync(workflowId, "problem.needs_more_information", output.NeedsMoreInformation.ToString().ToLowerInvariant(), sourceAgent);
        await _memoryService.SaveAsync(workflowId, "problem.follow_up_questions", JsonSerializer.Serialize(output.FollowUpQuestions), sourceAgent);
        await _memoryService.SaveAsync(workflowId, "problem.location", output.ExtractedLocation ?? string.Empty, sourceAgent);
        await _memoryService.SaveAsync(workflowId, "problem.additional_information", JsonSerializer.Serialize(output.AdditionalInformation), sourceAgent);
    }
}
