using AssistLK.Application.Interfaces;
using AssistLK.Domain.Entities;
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

    public DbSet<AgentWorkflow> AgentWorkflows =>
        Set<AgentWorkflow>();

    public DbSet<AgentExecution> AgentExecutions =>
        Set<AgentExecution>();

    public DbSet<AgentApproval> AgentApprovals =>
        Set<AgentApproval>();

    public DbSet<AgentAuditLog> AgentAuditLogs =>
        Set<AgentAuditLog>();

    public DbSet<AgentMemory> AgentMemories =>
        Set<AgentMemory>();

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

        ConfigureUser(modelBuilder);
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