using System.Text.Json;
using AssistLK.Application.Interfaces;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.Application.Services;

public class AgentWorkflowService
{
    private readonly IAgentWorkflowDbContext _db;

    public AgentWorkflowService(
        IAgentWorkflowDbContext db)
    {
        _db = db;
    }

    public async Task<AgentWorkflow>
        CreateAsync(
            Guid? userId,
            string workflowType,
            string input)
    {
        var workflow = new AgentWorkflow
        {
            Id = Guid.NewGuid(),

            UserId = userId,

            WorkflowType =
                workflowType,

            Status =
                AgentWorkflowStatus.Pending,

            Input = input,

            CreatedAtUtc =
                DateTime.UtcNow,

            UpdatedAtUtc =
                DateTime.UtcNow
        };

        _db.AgentWorkflows.Add(workflow);

        await _db.SaveChangesAsync();

        await AddAuditLogAsync(
            workflow.Id,
            userId,
            "WorkflowCreated",
            $"Workflow '{workflowType}' created."
        );

        return workflow;
    }

    public async Task<AgentWorkflow?>
        GetAsync(Guid workflowId)
    {
        return await _db.AgentWorkflows
            .Include(x => x.Executions)
            .Include(x => x.Approvals)
            .Include(x => x.AuditLogs)
            .FirstOrDefaultAsync(
                x => x.Id == workflowId
            );
    }

    public async Task SetStatusAsync(
        Guid workflowId,
        string status)
    {
        var workflow =
            await _db.AgentWorkflows
                .FirstOrDefaultAsync(
                    x => x.Id == workflowId
                );

        if (workflow == null)
        {
            throw new InvalidOperationException(
                "Workflow not found."
            );
        }

        workflow.Status = status;
        workflow.UpdatedAtUtc =
            DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task<AgentExecution>
        StartExecutionAsync(
            Guid workflowId,
            string agentName,
            object? input)
    {
        var execution =
            new AgentExecution
            {
                Id = Guid.NewGuid(),

                WorkflowId = workflowId,

                AgentName = agentName,

                Status = "Started",

                Input = input == null
                    ? null
                    : JsonSerializer.Serialize(
                        input
                    ),

                StartedAtUtc =
                    DateTime.UtcNow
            };

        _db.AgentExecutions.Add(
            execution
        );

        await _db.SaveChangesAsync();

        return execution;
    }

    public async Task CompleteExecutionAsync(
        Guid executionId,
        bool success,
        object? output)
    {
        var execution =
            await _db.AgentExecutions
                .FirstOrDefaultAsync(
                    x => x.Id == executionId
                );

        if (execution == null)
        {
            throw new InvalidOperationException(
                "Agent execution not found."
            );
        }

        execution.Status =
            success
                ? "Completed"
                : "Failed";

        execution.Output =
            output == null
                ? null
                : JsonSerializer.Serialize(
                    output
                );

        execution.CompletedAtUtc =
            DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task<AgentApproval>
        RequestApprovalAsync(
            Guid workflowId,
            string action)
    {
        var approval =
            new AgentApproval
            {
                Id = Guid.NewGuid(),

                WorkflowId = workflowId,

                Action = action,

                Status =
                    AgentApprovalStatus.Pending,

                CreatedAtUtc =
                    DateTime.UtcNow
            };

        _db.AgentApprovals.Add(
            approval
        );

        await SetStatusAsync(
            workflowId,
            AgentWorkflowStatus
                .WaitingForApproval
        );

        await _db.SaveChangesAsync();

        return approval;
    }

    public async Task ApproveAsync(
        Guid approvalId,
        Guid approvedByUserId,
        string? reason)
    {
        var approval =
            await _db.AgentApprovals
                .Include(x => x.Workflow)
                .FirstOrDefaultAsync(
                    x => x.Id == approvalId
                );

        if (approval == null)
        {
            throw new InvalidOperationException(
                "Approval not found."
            );
        }

        if (approval.Status !=
            AgentApprovalStatus.Pending)
        {
            throw new InvalidOperationException(
                "This approval has already been decided."
            );
        }

        approval.Status =
            AgentApprovalStatus.Approved;

        approval.ApprovedByUserId =
            approvedByUserId;

        approval.DecisionReason =
            reason;

        approval.DecidedAtUtc =
            DateTime.UtcNow;

        approval.Workflow.Status =
            AgentWorkflowStatus.Running;

        approval.Workflow.UpdatedAtUtc =
            DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await AddAuditLogAsync(
            approval.WorkflowId,
            approvedByUserId,
            "ApprovalGranted",
            $"Approval granted for action '{approval.Action}'."
        );
    }

    public async Task RejectAsync(
        Guid approvalId,
        Guid rejectedByUserId,
        string? reason)
    {
        var approval =
            await _db.AgentApprovals
                .Include(x => x.Workflow)
                .FirstOrDefaultAsync(
                    x => x.Id == approvalId
                );

        if (approval == null)
        {
            throw new InvalidOperationException(
                "Approval not found."
            );
        }

        if (approval.Status !=
            AgentApprovalStatus.Pending)
        {
            throw new InvalidOperationException(
                "This approval has already been decided."
            );
        }

        approval.Status =
            AgentApprovalStatus.Rejected;

        approval.ApprovedByUserId =
            rejectedByUserId;

        approval.DecisionReason =
            reason;

        approval.DecidedAtUtc =
            DateTime.UtcNow;

        approval.Workflow.Status =
            AgentWorkflowStatus.Cancelled;

        approval.Workflow.UpdatedAtUtc =
            DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await AddAuditLogAsync(
            approval.WorkflowId,
            rejectedByUserId,
            "ApprovalRejected",
            $"Approval rejected for action '{approval.Action}'."
        );
    }

    private async Task AddAuditLogAsync(
        Guid workflowId,
        Guid? userId,
        string eventType,
        string description)
    {
        _db.AgentAuditLogs.Add(
            new AgentAuditLog
            {
                Id = Guid.NewGuid(),

                WorkflowId = workflowId,

                UserId = userId,

                EventType = eventType,

                Description = description,

                CreatedAtUtc =
                    DateTime.UtcNow
            }
        );

        await _db.SaveChangesAsync();
    }
}