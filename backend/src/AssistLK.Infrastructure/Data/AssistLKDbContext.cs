using AssistLK.Application.Interfaces;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.Infrastructure.Data;

public class AssistLKDbContext : DbContext, IAgentWorkflowDbContext
{
    public AssistLKDbContext(
        DbContextOptions<AssistLKDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();

    public DbSet<ProblemAnalysis> ProblemAnalyses => Set<ProblemAnalysis>();

    public DbSet<ServiceRequestClarification> ServiceRequestClarifications => Set<ServiceRequestClarification>();

    public DbSet<AgentWorkflow> AgentWorkflows =>
        Set<AgentWorkflow>();

    public DbSet<AgentExecution> AgentExecutions =>
        Set<AgentExecution>();

    public DbSet<AgentExecutionMetric> AgentExecutionMetrics =>
        Set<AgentExecutionMetric>();

    public DbSet<AgentApproval> AgentApprovals =>
        Set<AgentApproval>();

    public DbSet<AgentAuditLog> AgentAuditLogs =>
        Set<AgentAuditLog>();

    public DbSet<AgentMemory> AgentMemories =>
        Set<AgentMemory>();

    public DbSet<AgentAction> AgentActions =>
        Set<AgentAction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AgentWorkflow>(
            entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.WorkflowType)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(x => x.Status)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(x => x.CurrentAgent)
                    .HasMaxLength(150);

                entity.Property(x => x.Input)
                    .HasColumnType("text");

                entity.HasMany(x => x.Executions)
                    .WithOne(x => x.Workflow)
                    .HasForeignKey(x => x.WorkflowId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(x => x.Approvals)
                    .WithOne(x => x.Workflow)
                    .HasForeignKey(x => x.WorkflowId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(x => x.AuditLogs)
                    .WithOne(x => x.Workflow)
                    .HasForeignKey(x => x.WorkflowId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        );

        modelBuilder.Entity<AgentExecution>(
            entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.AgentName)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.Property(x => x.Status)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(x => x.Input)
                    .HasColumnType("text");

                entity.Property(x => x.Output)
                    .HasColumnType("text");
            }
        );

        modelBuilder.Entity<AgentExecutionMetric>(
            entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.AgentName)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.Property(x => x.Status)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.HasOne(x => x.Workflow)
                    .WithMany()
                    .HasForeignKey(x => x.WorkflowId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        );

        modelBuilder.Entity<AgentApproval>(
            entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Action)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(x => x.Status)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(x => x.DecisionReason)
                    .HasColumnType("text");
            }
        );

        modelBuilder.Entity<AgentAuditLog>(
            entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.EventType)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(x => x.Description)
                    .IsRequired()
                    .HasColumnType("text");
            }
        );

        modelBuilder.Entity<AgentMemory>(
            entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Key)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(x => x.Value)
                    .IsRequired()
                    .HasColumnType("text");

                entity.Property(x => x.SourceAgent)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.HasOne(x => x.Workflow)
                    .WithMany()
                    .HasForeignKey(x => x.WorkflowId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        );

        modelBuilder.Entity<AgentAction>(
            entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.ActionType)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(x => x.Description)
                    .IsRequired()
                    .HasColumnType("text");

                entity.Property(x => x.RiskLevel)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(x => x.Status)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.HasOne(x => x.Workflow)
                    .WithMany()
                    .HasForeignKey(x => x.WorkflowId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        );

        ConfigureUser(modelBuilder);
        ConfigureServiceRequest(modelBuilder);
        ConfigureProblemAnalysis(modelBuilder);
        ConfigureServiceRequestClarification(modelBuilder);
    }

    private static void ConfigureUser(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<User>();

        user.ToTable("Users");

        user.HasKey(x => x.Id);

        user.Property(x => x.FullName)
            .IsRequired()
            .HasMaxLength(150);

        user.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(255);

        user.HasIndex(x => x.Email)
            .IsUnique();

        user.Property(x => x.PasswordHash)
            .IsRequired()
            .HasMaxLength(500);

        user.Property(x => x.PhoneNumber)
            .HasMaxLength(20);

        user.Property(x => x.Role)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(30);

        user.Property(x => x.IsActive)
            .HasDefaultValue(true);

        user.Property(x => x.CreatedAt)
            .IsRequired();

        user.Property(x => x.UpdatedAt)
            .IsRequired();
    }

    private static void ConfigureServiceRequest(ModelBuilder modelBuilder)
    {
        var request = modelBuilder.Entity<ServiceRequest>();

        request.ToTable(
            "ServiceRequests",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_ServiceRequests_Latitude",
                    "\"Latitude\" IS NULL OR \"Latitude\" BETWEEN -90 AND 90");

                table.HasCheckConstraint(
                    "CK_ServiceRequests_Longitude",
                    "\"Longitude\" IS NULL OR \"Longitude\" BETWEEN -180 AND 180");
            });

        request.HasKey(x => x.Id);

        request.Property(x => x.CustomerId)
            .IsRequired();

        request.Property(x => x.CategoryHint)
            .HasMaxLength(100)
            .IsRequired(false);

        request.Property(x => x.Category)
            .IsRequired()
            .HasMaxLength(100);

        request.Property(x => x.Description)
            .IsRequired()
            .HasColumnType("text");

        request.Property(x => x.LocationText)
            .IsRequired()
            .HasMaxLength(255);

        request.Property(x => x.Latitude)
            .HasPrecision(9, 6);

        request.Property(x => x.LocationSource)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(LocationSource.Manual)
            .IsRequired();

        request.Property(x => x.Longitude)
            .HasPrecision(9, 6);

        request.Property(x => x.Urgency)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(30);

        request.Property(x => x.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(40);

        request.Property(x => x.CreatedAt)
            .IsRequired();

        request.Property(x => x.UpdatedAt)
            .IsRequired();

        request.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        request.HasIndex(x => x.CustomerId);
        request.HasIndex(x => x.Status);
    }

    private static void ConfigureProblemAnalysis(ModelBuilder modelBuilder)
    {
        var analysis = modelBuilder.Entity<ProblemAnalysis>();

        analysis.ToTable(
            "ProblemAnalyses",
            table => table.HasCheckConstraint(
                "CK_ProblemAnalyses_Confidence",
                "\"Confidence\" >= 0 AND \"Confidence\" <= 1"));

        analysis.HasKey(x => x.Id);

        analysis.Property(x => x.ServiceRequestId)
            .IsRequired();

        analysis.Property(x => x.DetectedProblem)
            .IsRequired()
            .HasColumnType("text");

        analysis.Property(x => x.Confidence)
            .IsRequired()
            .HasPrecision(5, 4);

        analysis.Property(x => x.AgentName)
            .IsRequired()
            .HasMaxLength(100);

        analysis.Property(x => x.CreatedAt)
            .IsRequired();

        analysis.Property(x => x.UpdatedAt)
            .IsRequired();

        analysis.HasOne(x => x.ServiceRequest)
            .WithMany(x => x.ProblemAnalyses)
            .HasForeignKey(x => x.ServiceRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        analysis.HasIndex(x => x.ServiceRequestId);
    }

    private static void ConfigureServiceRequestClarification(ModelBuilder modelBuilder)
    {
        var clarification = modelBuilder.Entity<ServiceRequestClarification>();

        clarification.ToTable(
            "ServiceRequestClarifications",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_ServiceRequestClarifications_Round",
                    "\"ClarificationRound\" >= 1");

                table.HasCheckConstraint(
                    "CK_ServiceRequestClarifications_Sequence",
                    "\"Sequence\" >= 1");
            });

        clarification.HasKey(x => x.Id);

        clarification.Property(x => x.ServiceRequestId)
            .IsRequired();

        clarification.Property(x => x.ClarificationRound)
            .IsRequired();

        clarification.Property(x => x.Sequence)
            .IsRequired();

        clarification.Property(x => x.Question)
            .IsRequired()
            .HasMaxLength(500);

        clarification.Property(x => x.Answer)
            .HasMaxLength(1000)
            .IsRequired(false);

        clarification.Property(x => x.AnsweredAt)
            .IsRequired(false);

        clarification.Property(x => x.SupersededAt)
            .IsRequired(false);

        clarification.Property(x => x.CreatedAt)
            .IsRequired();

        clarification.Property(x => x.UpdatedAt)
            .IsRequired();

        clarification.HasOne(x => x.ServiceRequest)
            .WithMany(x => x.Clarifications)
            .HasForeignKey(x => x.ServiceRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        clarification.HasIndex(x => x.ServiceRequestId);

        clarification.HasIndex(x => new { x.ServiceRequestId, x.ClarificationRound, x.Sequence })
            .IsUnique();
    }

    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker
            .Entries<BaseEntity>();

        var now = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        return await base.SaveChangesAsync(
            cancellationToken);
    }
}
