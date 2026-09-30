using AssistLK.Api.Authentication;
using AssistLK.Application.Auth;
using AssistLK.Application.Interfaces;
using AssistLK.Application.Services.Auth;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using AssistLK.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssistLK.IntegrationTests;

public class RegistrationChallengeCleanupTests
{
    private readonly string _databaseName = Guid.NewGuid().ToString("N");

    private AssistLKDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AssistLKDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .Options;
        return new AssistLKDbContext(options);
    }

    private static RegistrationChallenge CreateChallenge(
        string email,
        string phoneNumber,
        DateTime expiresAtUtc,
        bool isConsumed = false)
    {
        return new RegistrationChallenge
        {
            Id = Guid.NewGuid(),
            FullName = "Test User",
            Email = email,
            PhoneNumber = phoneNumber,
            PasswordHash = "hash",
            Role = UserRole.Customer,
            OtpHash = "otphash",
            ExpiresAtUtc = expiresAtUtc,
            AttemptCount = 0,
            MaxAttempts = 5,
            ResendCount = 0,
            LastSentAtUtc = expiresAtUtc.AddMinutes(-5),
            IsConsumed = isConsumed
        };
    }

    [Fact]
    public async Task Test1_Active_Unexpired_Challenge_Preserved()
    {
        // Arrange
        await using var db = CreateDbContext();
        var repo = new RegistrationChallengeRepository(db);
        var options = new RegistrationChallengeRetentionOptions { RetentionHours = 24 };
        var service = new RegistrationChallengeCleanupService(repo, directOptions: options);

        var active = CreateChallenge("active@test.com", "+94771111111", DateTime.UtcNow.AddMinutes(5), isConsumed: false);
        await db.RegistrationChallenges.AddAsync(active);
        await db.SaveChangesAsync();

        // Act
        var deletedCount = await service.CleanupObsoleteChallengesAsync();

        // Assert
        Assert.Equal(0, deletedCount);
        var remaining = await db.RegistrationChallenges.FindAsync(active.Id);
        Assert.NotNull(remaining);
        Assert.False(remaining.IsConsumed);
    }

    [Fact]
    public async Task Test2_Recently_Expired_Challenge_Inside_Retention_Preserved()
    {
        // Arrange
        await using var db = CreateDbContext();
        var repo = new RegistrationChallengeRepository(db);
        var options = new RegistrationChallengeRetentionOptions { RetentionHours = 24 };
        var service = new RegistrationChallengeCleanupService(repo, directOptions: options);

        // Expired 1 hour ago, but retention is 24 hours
        var recentExpired = CreateChallenge("recent_expired@test.com", "+94772222222", DateTime.UtcNow.AddHours(-1), isConsumed: false);
        await db.RegistrationChallenges.AddAsync(recentExpired);
        await db.SaveChangesAsync();

        // Act
        var deletedCount = await service.CleanupObsoleteChallengesAsync();

        // Assert: newly expired challenge inside retention must NOT be deleted immediately
        Assert.Equal(0, deletedCount);
        var remaining = await db.RegistrationChallenges.FindAsync(recentExpired.Id);
        Assert.NotNull(remaining);
    }

    [Fact]
    public async Task Test3_Old_Expired_Challenge_Deleted()
    {
        // Arrange
        await using var db = CreateDbContext();
        var repo = new RegistrationChallengeRepository(db);
        var options = new RegistrationChallengeRetentionOptions { RetentionHours = 24 };
        var service = new RegistrationChallengeCleanupService(repo, directOptions: options);

        // Expired 25 hours ago (> 24 hour retention)
        var oldExpired = CreateChallenge("old_expired@test.com", "+94773333333", DateTime.UtcNow.AddHours(-25), isConsumed: false);
        await db.RegistrationChallenges.AddAsync(oldExpired);
        await db.SaveChangesAsync();

        // Act
        var deletedCount = await service.CleanupObsoleteChallengesAsync();

        // Assert: old expired challenge past retention must be deleted
        Assert.Equal(1, deletedCount);
        var remaining = await db.RegistrationChallenges.FindAsync(oldExpired.Id);
        Assert.Null(remaining);
    }

    [Fact]
    public async Task Test4_Old_Consumed_Challenge_Deleted()
    {
        // Arrange
        await using var db = CreateDbContext();
        var repo = new RegistrationChallengeRepository(db);
        var options = new RegistrationChallengeRetentionOptions { RetentionHours = 24 };
        var service = new RegistrationChallengeCleanupService(repo, directOptions: options);

        // Consumed and expired 26 hours ago (> 24 hour retention)
        var oldConsumed = CreateChallenge("old_consumed@test.com", "+94774444444", DateTime.UtcNow.AddHours(-26), isConsumed: true);
        await db.RegistrationChallenges.AddAsync(oldConsumed);
        await db.SaveChangesAsync();

        // Act
        var deletedCount = await service.CleanupObsoleteChallengesAsync();

        // Assert: old consumed challenge must be deleted
        Assert.Equal(1, deletedCount);
        var remaining = await db.RegistrationChallenges.FindAsync(oldConsumed.Id);
        Assert.Null(remaining);
    }

    [Fact]
    public async Task Test5_Recent_Consumed_Challenge_Preserved()
    {
        // Arrange
        await using var db = CreateDbContext();
        var repo = new RegistrationChallengeRepository(db);
        var options = new RegistrationChallengeRetentionOptions { RetentionHours = 24 };
        var service = new RegistrationChallengeCleanupService(repo, directOptions: options);

        // Consumed 10 minutes ago
        var recentConsumed = CreateChallenge("recent_consumed@test.com", "+94775555555", DateTime.UtcNow.AddMinutes(-5), isConsumed: true);
        await db.RegistrationChallenges.AddAsync(recentConsumed);
        await db.SaveChangesAsync();

        // Act
        var deletedCount = await service.CleanupObsoleteChallengesAsync();

        // Assert: recent consumed challenge within retention must be preserved
        Assert.Equal(0, deletedCount);
        var remaining = await db.RegistrationChallenges.FindAsync(recentConsumed.Id);
        Assert.NotNull(remaining);
        Assert.True(remaining.IsConsumed);
    }

    [Fact]
    public async Task Test6_Active_Challenge_Never_Deleted()
    {
        // Arrange: mix of active, recently expired, recently consumed, and obsolete records
        await using var db = CreateDbContext();
        var repo = new RegistrationChallengeRepository(db);
        var options = new RegistrationChallengeRetentionOptions { RetentionHours = 24 };
        var service = new RegistrationChallengeCleanupService(repo, directOptions: options);

        var active1 = CreateChallenge("active1@test.com", "+94771000001", DateTime.UtcNow.AddMinutes(5), isConsumed: false);
        var active2 = CreateChallenge("active2@test.com", "+94771000002", DateTime.UtcNow.AddMinutes(3), isConsumed: false);
        var recentExp = CreateChallenge("recexp@test.com", "+94771000003", DateTime.UtcNow.AddHours(-2), isConsumed: false);
        var recentCon = CreateChallenge("reccon@test.com", "+94771000004", DateTime.UtcNow.AddHours(-1), isConsumed: true);
        var oldExp = CreateChallenge("oldexp@test.com", "+94771000005", DateTime.UtcNow.AddHours(-30), isConsumed: false);
        var oldCon = CreateChallenge("oldcon@test.com", "+94771000006", DateTime.UtcNow.AddHours(-40), isConsumed: true);

        await db.RegistrationChallenges.AddRangeAsync(active1, active2, recentExp, recentCon, oldExp, oldCon);
        await db.SaveChangesAsync();

        // Act
        var deletedCount = await service.CleanupObsoleteChallengesAsync();

        // Assert: only the 2 obsolete records are deleted
        Assert.Equal(2, deletedCount);

        // Active challenges are NEVER deleted
        Assert.NotNull(await db.RegistrationChallenges.FindAsync(active1.Id));
        Assert.NotNull(await db.RegistrationChallenges.FindAsync(active2.Id));
        // Recent records inside retention preserved
        Assert.NotNull(await db.RegistrationChallenges.FindAsync(recentExp.Id));
        Assert.NotNull(await db.RegistrationChallenges.FindAsync(recentCon.Id));
        // Obsolete records deleted
        Assert.Null(await db.RegistrationChallenges.FindAsync(oldExp.Id));
        Assert.Null(await db.RegistrationChallenges.FindAsync(oldCon.Id));
    }

    [Fact]
    public async Task Test7_Multiple_Obsolete_Records_Deleted_Safely()
    {
        // Arrange
        await using var db = CreateDbContext();
        var repo = new RegistrationChallengeRepository(db);
        var options = new RegistrationChallengeRetentionOptions { RetentionHours = 24 };
        var service = new RegistrationChallengeCleanupService(repo, directOptions: options);

        var obsoleteList = new List<RegistrationChallenge>();
        for (int i = 0; i < 5; i++)
        {
            obsoleteList.Add(CreateChallenge($"old_exp_{i}@test.com", $"+9477200000{i}", DateTime.UtcNow.AddHours(-30 - i), isConsumed: false));
            obsoleteList.Add(CreateChallenge($"old_con_{i}@test.com", $"+9477300000{i}", DateTime.UtcNow.AddHours(-30 - i), isConsumed: true));
        }

        var preservedList = new List<RegistrationChallenge>
        {
            CreateChallenge("pres_act@test.com", "+94774000001", DateTime.UtcNow.AddMinutes(5), isConsumed: false),
            CreateChallenge("pres_exp@test.com", "+94774000002", DateTime.UtcNow.AddHours(-5), isConsumed: false),
            CreateChallenge("pres_con@test.com", "+94774000003", DateTime.UtcNow.AddHours(-5), isConsumed: true)
        };

        await db.RegistrationChallenges.AddRangeAsync(obsoleteList);
        await db.RegistrationChallenges.AddRangeAsync(preservedList);
        await db.SaveChangesAsync();

        // Act
        var deletedCount = await service.CleanupObsoleteChallengesAsync();

        // Assert: exactly 10 obsolete records deleted
        Assert.Equal(10, deletedCount);
        foreach (var obs in obsoleteList)
        {
            Assert.Null(await db.RegistrationChallenges.FindAsync(obs.Id));
        }
        foreach (var pres in preservedList)
        {
            Assert.NotNull(await db.RegistrationChallenges.FindAsync(pres.Id));
        }
    }

    [Fact]
    public async Task Test8_Cleanup_Idempotent()
    {
        // Arrange
        await using var db = CreateDbContext();
        var repo = new RegistrationChallengeRepository(db);
        var options = new RegistrationChallengeRetentionOptions { RetentionHours = 24 };
        var service = new RegistrationChallengeCleanupService(repo, directOptions: options);

        var old = CreateChallenge("idem@test.com", "+94775000001", DateTime.UtcNow.AddHours(-48), isConsumed: false);
        await db.RegistrationChallenges.AddAsync(old);
        await db.SaveChangesAsync();

        // Act 1: first run cleans up 1 record
        var firstRun = await service.CleanupObsoleteChallengesAsync();
        Assert.Equal(1, firstRun);
        Assert.Null(await db.RegistrationChallenges.FindAsync(old.Id));

        // Act 2: second run immediately following must safely delete 0 records without errors
        var secondRun = await service.CleanupObsoleteChallengesAsync();
        Assert.Equal(0, secondRun);
    }

    [Fact]
    public async Task Test9_Worker_Exception_Does_Not_Crash_Host()
    {
        // Arrange
        var services = new ServiceCollection();
        var failingService = new FailingCleanupService();
        services.AddScoped<IRegistrationChallengeCleanupService>(_ => failingService);

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var options = new RegistrationChallengeRetentionOptions
        {
            InitialDelaySeconds = 0,
            IntervalSeconds = 1
        };

        var worker = new RegistrationChallengeCleanupBackgroundService(
            scopeFactory,
            directOptions: options,
            logger: NullLogger<RegistrationChallengeCleanupBackgroundService>.Instance);

        using var cts = new CancellationTokenSource();

        // Act: Start worker and allow cycle to encounter simulated exception
        await worker.StartAsync(cts.Token);
        await Task.Delay(1200);
        await worker.StopAsync(CancellationToken.None);

        // Assert: Worker executed despite exception and did not throw / crash
        Assert.True(failingService.InvocationCount > 0, "Worker should have attempted execution cycle.");
    }

    [Fact]
    public async Task Test10_Cancellation_Works()
    {
        // Arrange
        var services = new ServiceCollection();
        await using var db = CreateDbContext();
        services.AddScoped<IRegistrationChallengeRepository>(_ => new RegistrationChallengeRepository(db));
        services.AddScoped<IRegistrationChallengeCleanupService, RegistrationChallengeCleanupService>();

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var options = new RegistrationChallengeRetentionOptions
        {
            InitialDelaySeconds = 60,
            IntervalHours = 24
        };

        var worker = new RegistrationChallengeCleanupBackgroundService(
            scopeFactory,
            directOptions: options,
            logger: NullLogger<RegistrationChallengeCleanupBackgroundService>.Instance);

        using var cts = new CancellationTokenSource();

        // Act
        await worker.StartAsync(cts.Token);
        // Cancel token during initial delay
        cts.Cancel();
        var stopTask = worker.StopAsync(CancellationToken.None);
        var completed = await Task.WhenAny(stopTask, Task.Delay(3000));

        // Assert: Worker terminated gracefully on cancellation within reasonable timeout
        Assert.Same(stopTask, completed);
    }

    [Theory]
    [InlineData(-1, 24, 24, "InitialDelaySeconds must not be negative")]
    [InlineData(60, 0, 24, "IntervalHours must be at least 1 hour")]
    [InlineData(60, 24, 0, "RetentionHours must be at least 1 hour")]
    public void Test11_Options_Validation_Enforces_Rules(int delay, int intervalHours, int retentionHours, string expectedMessage)
    {
        var options = new RegistrationChallengeRetentionOptions
        {
            InitialDelaySeconds = delay,
            IntervalHours = intervalHours,
            RetentionHours = retentionHours
        };

        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains(expectedMessage, ex.Message);
    }

    [Fact]
    public async Task Test12_BackgroundWorker_Executes_Cleanup_Cycle_Successfully()
    {
        // Arrange
        var services = new ServiceCollection();
        await using var db = CreateDbContext();

        var oldExpired = CreateChallenge("worker_old@test.com", "+94776000001", DateTime.UtcNow.AddHours(-30), isConsumed: false);
        var active = CreateChallenge("worker_act@test.com", "+94776000002", DateTime.UtcNow.AddMinutes(5), isConsumed: false);
        await db.RegistrationChallenges.AddRangeAsync(oldExpired, active);
        await db.SaveChangesAsync();

        services.AddScoped<IRegistrationChallengeRepository>(_ => new RegistrationChallengeRepository(db));
        services.AddScoped<IRegistrationChallengeCleanupService, RegistrationChallengeCleanupService>();

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var options = new RegistrationChallengeRetentionOptions
        {
            InitialDelaySeconds = 0,
            IntervalSeconds = 1,
            RetentionHours = 24
        };

        var worker = new RegistrationChallengeCleanupBackgroundService(
            scopeFactory,
            directOptions: options,
            logger: NullLogger<RegistrationChallengeCleanupBackgroundService>.Instance);

        using var cts = new CancellationTokenSource();

        // Act
        await worker.StartAsync(cts.Token);
        await Task.Delay(1500); // allow cycle to complete
        await worker.StopAsync(CancellationToken.None);

        // Assert: Old challenge cleaned up, active challenge preserved
        Assert.Null(await db.RegistrationChallenges.FindAsync(oldExpired.Id));
        Assert.NotNull(await db.RegistrationChallenges.FindAsync(active.Id));
    }

    private sealed class FailingCleanupService : IRegistrationChallengeCleanupService
    {
        public int InvocationCount { get; private set; }

        public Task<int> CleanupObsoleteChallengesAsync(CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            throw new InvalidOperationException("Simulated failure in cleanup service.");
        }

        public Task<int> CleanupObsoleteChallengesAsync(DateTime? cutoffUtcOverride, CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            throw new InvalidOperationException("Simulated failure in cleanup service.");
        }
    }
}
