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

    public DbSet<ServiceRequestAttachment> ServiceRequestAttachments => Set<ServiceRequestAttachment>();

    public DbSet<ProblemAnalysis> ProblemAnalyses => Set<ProblemAnalysis>();

    public DbSet<ServiceRequestClarification> ServiceRequestClarifications => Set<ServiceRequestClarification>();


    //My Part
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationItem> QuotationItems => Set<QuotationItem>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingStatusHistory> BookingStatusHistories => Set<BookingStatusHistory>(); 
    //My Part


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

    public DbSet<ProviderProfile> ProviderProfiles =>
        Set<ProviderProfile>();

    public DbSet<ProviderSkill> ProviderSkills =>
        Set<ProviderSkill>();

    public DbSet<ProviderLocation> ProviderLocations =>
        Set<ProviderLocation>();

    public DbSet<ProviderAvailability> ProviderAvailabilities =>
        Set<ProviderAvailability>();

    public DbSet<MatchingExecution> MatchingExecutions =>
        Set<MatchingExecution>();

    public DbSet<MatchedCandidate> MatchedCandidates =>
        Set<MatchedCandidate>();

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
        modelBuilder.Entity<ServiceRequest>().Property(x => x.EvidenceRevision).HasDefaultValue(1L).IsConcurrencyToken();
        modelBuilder.Entity<ServiceRequest>().Property(x => x.Status).IsConcurrencyToken();
        modelBuilder.Entity<ProblemAnalysis>().Property(x => x.EvidenceRevision).HasDefaultValue(1L);
        modelBuilder.Entity<ServiceRequest>().ToTable(t => t.HasCheckConstraint("CK_ServiceRequests_EvidenceRevision", "\"EvidenceRevision\" > 0"));
        modelBuilder.Entity<ProblemAnalysis>().ToTable(t => t.HasCheckConstraint("CK_ProblemAnalyses_EvidenceRevision", "\"EvidenceRevision\" > 0"));
        var attachment = modelBuilder.Entity<ServiceRequestAttachment>();
        attachment.ToTable("ServiceRequestAttachments", t =>
        {
            t.HasCheckConstraint("CK_Attachments_Slot", "\"Slot\" BETWEEN 1 AND 3");
            t.HasCheckConstraint("CK_Attachments_Size", "\"FileSizeBytes\" > 0");
            t.HasCheckConstraint("CK_Attachments_Dimensions", "\"Width\" > 0 AND \"Height\" > 0");
        });
        attachment.HasKey(x => x.Id);
        attachment.Property(x => x.StorageKey).HasMaxLength(36).IsRequired();
        attachment.Property(x => x.ContentType).HasMaxLength(32).IsRequired();
        attachment.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
        attachment.HasIndex(x => x.StorageKey).IsUnique();
        attachment.HasIndex(x => new { x.ServiceRequestId, x.Slot }).IsUnique();
        attachment.HasOne(x => x.ServiceRequest).WithMany(x => x.Attachments)
            .HasForeignKey(x => x.ServiceRequestId).OnDelete(DeleteBehavior.Cascade);
        ConfigureProblemAnalysis(modelBuilder);
        ConfigureServiceRequestClarification(modelBuilder);
        ConfigureProviderProfile(modelBuilder);
        ConfigureProviderSkill(modelBuilder);
        ConfigureProviderLocation(modelBuilder);
        ConfigureProviderAvailability(modelBuilder);
        ConfigureMatchingExecution(modelBuilder);
        ConfigureMatchedCandidate(modelBuilder);
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
        analysis.Property(x => x.VisualEvidence)
            .HasColumnType("jsonb")
            .HasConversion(value => VisualEvidenceJson.Write(value), json => VisualEvidenceJson.Read(json))
            .HasDefaultValueSql("'{\"visionStatus\":\"not_requested\",\"attachmentIdsUsed\":[],\"observations\":[],\"limitations\":[]}'::jsonb")
            .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<ProblemAnalysisVisualEvidence>(
                (a, b) => VisualEvidenceJson.Write(a!) == VisualEvidenceJson.Write(b!),
                value => VisualEvidenceJson.Write(value).GetHashCode(),
                value => VisualEvidenceJson.Read(VisualEvidenceJson.Write(value))));

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

    private static void ConfigureProviderProfile(ModelBuilder modelBuilder)
    {
        var profile = modelBuilder.Entity<ProviderProfile>();

        profile.ToTable("ProviderProfiles");
        profile.HasKey(x => x.Id);

        profile.Property(x => x.UserId).IsRequired();
        profile.Property(x => x.BusinessName).IsRequired().HasMaxLength(200);
        profile.Property(x => x.VerificationStatus).HasConversion<string>().IsRequired().HasMaxLength(30);
        profile.Property(x => x.Rating).IsRequired().HasPrecision(3, 2);
        profile.Property(x => x.TotalCompletedJobs).IsRequired();
        profile.Property(x => x.MaxActiveJobs).IsRequired();
        profile.Property(x => x.IsOnline).IsRequired();
        profile.Property(x => x.CreatedAt).IsRequired();
        profile.Property(x => x.UpdatedAt).IsRequired();

        profile.HasIndex(x => x.UserId).IsUnique();
        profile.HasIndex(x => x.VerificationStatus);

        profile.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureProviderSkill(ModelBuilder modelBuilder)
    {
        var skill = modelBuilder.Entity<ProviderSkill>();

        skill.ToTable("ProviderSkills");
        skill.HasKey(x => x.Id);

        skill.Property(x => x.ProviderId).IsRequired();
        skill.Property(x => x.Category).IsRequired().HasMaxLength(100);
        skill.Property(x => x.SkillName).IsRequired().HasMaxLength(150);
        skill.Property(x => x.CertificationUrl).HasMaxLength(500).IsRequired(false);
        skill.Property(x => x.IsVerified).IsRequired();
        skill.Property(x => x.CreatedAt).IsRequired();
        skill.Property(x => x.UpdatedAt).IsRequired();

        skill.HasOne(x => x.Provider)
            .WithMany(x => x.Skills)
            .HasForeignKey(x => x.ProviderId)
            .OnDelete(DeleteBehavior.Cascade);

        skill.HasIndex(x => x.ProviderId);
        skill.HasIndex(x => x.Category);
    }

    private static void ConfigureProviderLocation(ModelBuilder modelBuilder)
    {
        var location = modelBuilder.Entity<ProviderLocation>();

        location.ToTable(
            "ProviderLocations",
            table =>
            {
                table.HasCheckConstraint("CK_ProviderLocations_Latitude", "\"Latitude\" BETWEEN -90 AND 90");
                table.HasCheckConstraint("CK_ProviderLocations_Longitude", "\"Longitude\" BETWEEN -180 AND 180");
                table.HasCheckConstraint("CK_ProviderLocations_OperatingRadiusKm", "\"OperatingRadiusKm\" >= 0");
            });

        location.HasKey(x => x.Id);

        location.Property(x => x.ProviderId).IsRequired();
        location.Property(x => x.Latitude).HasPrecision(9, 6);
        location.Property(x => x.Longitude).HasPrecision(9, 6);
        location.Property(x => x.OperatingRadiusKm).HasPrecision(6, 2);
        location.Property(x => x.LastLocationUpdate).IsRequired();
        location.Property(x => x.CreatedAt).IsRequired();
        location.Property(x => x.UpdatedAt).IsRequired();

        location.HasOne(x => x.Provider)
            .WithMany(x => x.Locations)
            .HasForeignKey(x => x.ProviderId)
            .OnDelete(DeleteBehavior.Cascade);

        location.HasIndex(x => x.ProviderId);
    }

    private static void ConfigureProviderAvailability(ModelBuilder modelBuilder)
    {
        var availability = modelBuilder.Entity<ProviderAvailability>();

        availability.ToTable(
            "ProviderAvailabilities",
            table =>
            {
                table.HasCheckConstraint("CK_ProviderAvailabilities_DayOfWeek", "\"DayOfWeek\" BETWEEN 0 AND 6");
            });

        availability.HasKey(x => x.Id);

        availability.Property(x => x.ProviderId).IsRequired();
        availability.Property(x => x.DayOfWeek).IsRequired();
        availability.Property(x => x.StartTime).IsRequired();
        availability.Property(x => x.EndTime).IsRequired();
        availability.Property(x => x.IsAvailable).IsRequired();
        availability.Property(x => x.CreatedAt).IsRequired();
        availability.Property(x => x.UpdatedAt).IsRequired();

        availability.HasOne(x => x.Provider)
            .WithMany(x => x.Availability)
            .HasForeignKey(x => x.ProviderId)
            .OnDelete(DeleteBehavior.Cascade);

        availability.HasIndex(x => x.ProviderId);
    }

    private static void ConfigureMatchingExecution(ModelBuilder modelBuilder)
    {
        var execution = modelBuilder.Entity<MatchingExecution>();

        execution.ToTable("MatchingExecutions");
        execution.HasKey(x => x.Id);

        execution.Property(x => x.ServiceRequestId).IsRequired();
        execution.Property(x => x.StrategyUsed).HasConversion<string>().IsRequired().HasMaxLength(40);
        execution.Property(x => x.Status).HasConversion<string>().IsRequired().HasMaxLength(30);
        execution.Property(x => x.ThreadId).HasMaxLength(100);
        execution.Property(x => x.ExecutedAt).IsRequired();
        execution.Property(x => x.StartedAt);
        execution.Property(x => x.CompletedAt);
        execution.Property(x => x.CreatedAt).IsRequired();
        execution.Property(x => x.UpdatedAt).IsRequired();

        execution.HasIndex(x => x.ServiceRequestId);
        execution.HasIndex(x => x.Status);
        execution.HasIndex(x => x.ThreadId);
    }

    private static void ConfigureMatchedCandidate(ModelBuilder modelBuilder)
    {
        var candidate = modelBuilder.Entity<MatchedCandidate>();

        candidate.ToTable(
            "MatchedCandidates",
            table => table.HasCheckConstraint("CK_MatchedCandidates_Score", "\"Score\" >= 0 AND \"Score\" <= 1"));

        candidate.HasKey(x => x.Id);

        candidate.Property(x => x.MatchingExecutionId).IsRequired();
        candidate.Property(x => x.ProviderId).IsRequired();
        candidate.Property(x => x.Score).HasPrecision(5, 4);
        candidate.Property(x => x.Rank).IsRequired();
        candidate.Property(x => x.DistanceKm).HasPrecision(8, 2);
        candidate.Property(x => x.MatchRationale).IsRequired().HasColumnType("text");
        candidate.Property(x => x.Status).HasConversion<string>().IsRequired().HasMaxLength(30);
        candidate.Property(x => x.CreatedAt).IsRequired();
        candidate.Property(x => x.UpdatedAt).IsRequired();

        candidate.HasOne(x => x.MatchingExecution)
            .WithMany(x => x.Candidates)
            .HasForeignKey(x => x.MatchingExecutionId)
            .OnDelete(DeleteBehavior.Cascade);

        candidate.HasOne(x => x.Provider)
            .WithMany()
            .HasForeignKey(x => x.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        candidate.HasIndex(x => x.MatchingExecutionId);
        candidate.HasIndex(x => x.ProviderId);
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
