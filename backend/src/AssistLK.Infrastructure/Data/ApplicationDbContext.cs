using AssistLK.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<ServiceJob> ServiceJobs => Set<ServiceJob>();
    public DbSet<ServiceStatusHistory> ServiceStatusHistories => Set<ServiceStatusHistory>();
    public DbSet<CompletionRecord> CompletionRecords => Set<CompletionRecord>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<Complaint> Complaints => Set<Complaint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ServiceJob එකේ Status enum එක String එකක් ලෙස PostgreSQL වල Save කිරීමට
        modelBuilder.Entity<ServiceJob>()
            .Property(e => e.Status)
            .HasConversion<string>();

        // ServiceStatusHistory එකේ Enums String ලෙස Save කිරීමට
        modelBuilder.Entity<ServiceStatusHistory>()
            .Property(e => e.OldStatus)
            .HasConversion<string>();

        modelBuilder.Entity<ServiceStatusHistory>()
            .Property(e => e.NewStatus)
            .HasConversion<string>();
    }
}