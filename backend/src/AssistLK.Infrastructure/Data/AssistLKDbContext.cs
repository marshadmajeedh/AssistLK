using AssistLK.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.Infrastructure.Data;

public class AssistLKDbContext : DbContext
{
    public AssistLKDbContext(
        DbContextOptions<AssistLKDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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