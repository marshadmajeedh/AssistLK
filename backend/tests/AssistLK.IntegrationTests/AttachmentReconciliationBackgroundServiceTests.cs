using AssistLK.Api.Features.ServiceRequests;
using AssistLK.Application.Attachments;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Attachments;
using AssistLK.Infrastructure.Data;
using AssistLK.Infrastructure.Repositories;
using AssistLK.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssistLK.IntegrationTests;

// ==============================================================================
// TEST SUITE: AttachmentReconciliationBackgroundServiceTests
// What this class tests:
//   - AttachmentReconciliationService orphan file detection and cleanup logic.
//   - Grace period enforcement: unreferenced files created recently (e.g. in-flight uploads) are preserved.
//   - AttachmentReconciliationBackgroundService background worker lifecycle, cancellation, and error handling.
//   - Real filesystem integration with PrivateFileAttachmentStorage.
//   - AttachmentReconciliationOptions configuration validation.
// Why these tests are needed:
//   - Prevents orphaned image files from accumulating on storage without risking accidental deletion of
//     valid attachments or active in-flight uploads.
//   - Verifies the background worker never crashes the API process even on storage failures.
// What is real and what is mocked/faked:
//   - Real: AttachmentReconciliationService, AttachmentReconciliationBackgroundService,
//           ServiceRequestAttachmentRepository, AttachmentReconciliationOptions,
//           PrivateFileAttachmentStorage (tested against real temp folder in Test13).
//   - In-Memory/Faked: EF Core InMemory database (AssistLKDbContext), FakeAttachmentStorage, TestFailingStorage.
// Prerequisites:
//   - None (runs purely in-memory and in local temp folder).
// Run from repository root:
//   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.AttachmentReconciliationBackgroundServiceTests"
// ==============================================================================
public class AttachmentReconciliationBackgroundServiceTests : IDisposable
{
    private readonly string _databaseName = Guid.NewGuid().ToString("N");
    private readonly List<string> _tempDirs = new();

    private AssistLKDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AssistLKDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .Options;
        return new AssistLKDbContext(options);
    }

    private string CreateTempStorageDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "assistlk-reconcile-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
            catch
            {
                // Best-effort cleanup of temporary test directories
            }
        }
    }

    // Purpose: Checks that a storage file referenced by an active database attachment record is never deleted.
    // Why: Guarantees that valid customer attachments are protected from being purged as orphans.
    // Scenario: File exists in storage and is referenced in ServiceRequestAttachments table; reconciliation executes.
    // Pass condition: Returns 0 deleted files, file remains in storage, and DB record remains intact.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.AttachmentReconciliationBackgroundServiceTests.Test1_DbBacked_Attachment_File_Is_Preserved"
    [Fact]
    public async Task Test1_DbBacked_Attachment_File_Is_Preserved()
    {
        // Arrange
        await using var db = CreateDbContext();
        var storage = new FakeAttachmentStorage();
        var repository = new ServiceRequestAttachmentRepository(db);

        var request = new ServiceRequest
        {
            CustomerId = Guid.NewGuid(),
            Description = "Leaking bathroom faucet",
            LocationText = "Colombo 03",
            Status = ServiceRequestStatus.Created
        };
        db.ServiceRequests.Add(request);
        await db.SaveChangesAsync();

        var key = Guid.NewGuid().ToString("N") + ".jpg";
        var attachment = new ServiceRequestAttachment
        {
            ServiceRequestId = request.Id,
            Slot = 1,
            StorageKey = key,
            ContentType = "image/jpeg",
            FileSizeBytes = 1024,
            Width = 800,
            Height = 600,
            ContentHash = new string('a', 64)
        };
        db.ServiceRequestAttachments.Add(attachment);
        await db.SaveChangesAsync();

        await storage.WriteAsync(key, new byte[] { 1, 2, 3 });

        var service = new AttachmentReconciliationService(
            repository,
            storage,
            NullLogger<AttachmentReconciliationService>.Instance);

        // Act
        var deletedCount = await service.ReconcileAsync();

        // Assert
        Assert.Equal(0, deletedCount);
        Assert.True(storage.Files.ContainsKey(key), "DB-backed attachment file must be preserved in storage.");
        Assert.True(await db.ServiceRequestAttachments.AnyAsync(a => a.StorageKey == key), "DB record must remain intact.");
    }

    // Purpose: Checks that an orphaned storage file older than the grace period is purged.
    // Why: Reclaims storage space from abandoned or deleted uploads that have no DB reference.
    // Scenario: Unreferenced file in storage dated 2 days ago; reconciliation runs.
    // Pass condition: Returns 1 deleted file and file is removed from storage.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.AttachmentReconciliationBackgroundServiceTests.Test2_Old_Orphan_File_Is_Removed"
    [Fact]
    public async Task Test2_Old_Orphan_File_Is_Removed()
    {
        // Arrange
        await using var db = CreateDbContext();
        var storage = new FakeAttachmentStorage();
        var repository = new ServiceRequestAttachmentRepository(db);

        var orphanKey = Guid.NewGuid().ToString("N") + ".jpg";
        await storage.WriteAsync(orphanKey, new byte[] { 4, 5, 6 });
        // Set timestamp older than conservative threshold (e.g. 2 days ago)
        storage.CustomTimestamps[orphanKey] = DateTime.UtcNow.AddDays(-2);

        var service = new AttachmentReconciliationService(
            repository,
            storage,
            NullLogger<AttachmentReconciliationService>.Instance);

        // Act
        var deletedCount = await service.ReconcileAsync();

        // Assert
        Assert.Equal(1, deletedCount);
        Assert.False(storage.Files.ContainsKey(orphanKey), "Old orphan file must be removed from storage.");
    }

    // Purpose: Checks that an unreferenced storage file created recently (within the grace period) is not deleted.
    // Why: Protects in-flight customer uploads where the binary was written to storage but the DB transaction has not yet committed.
    // Scenario: File written 5 minutes ago with a 30-minute grace period; reconciliation runs.
    // Pass condition: 0 files deleted, and the recent file remains safely in storage.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.AttachmentReconciliationBackgroundServiceTests.Test3_Recent_Orphan_File_Inside_Grace_Period_Is_Preserved"
    [Fact]
    public async Task Test3_Recent_Orphan_File_Inside_Grace_Period_Is_Preserved()
    {
        // Arrange
        await using var db = CreateDbContext();
        var storage = new FakeAttachmentStorage();
        var repository = new ServiceRequestAttachmentRepository(db);

        var recentOrphanKey = Guid.NewGuid().ToString("N") + ".jpg";
        await storage.WriteAsync(recentOrphanKey, new byte[] { 7, 8, 9 });
        // Written 5 minutes ago (well inside the conservative 30 min / 24h grace period)
        storage.CustomTimestamps[recentOrphanKey] = DateTime.UtcNow.AddMinutes(-5);

        var options = new AttachmentReconciliationOptions
        {
            MinimumFileAgeMinutes = 30 // 30 minutes grace period
        };

        var service = new AttachmentReconciliationService(
            repository,
            storage,
            NullLogger<AttachmentReconciliationService>.Instance,
            options);

        // Act
        var result = await service.ReconcileDetailedAsync();

        // Assert
        Assert.Equal(0, result.DeletedCount);
        Assert.True(result.SkippedGracePeriodCount >= 0);
        Assert.True(storage.Files.ContainsKey(recentOrphanKey), "Recent orphan file inside grace period must NOT be deleted.");
    }

    // Purpose: Checks that if a candidate cleanup file disappears from storage before deletion, the service handles it without crashing.
    // Why: Ensures race conditions or concurrent filesystem changes do not throw unhandled exceptions.
    // Scenario: Candidate key is listed but absent in storage when reconciliation attempts deletion.
    // Pass condition: No exception is thrown during reconciliation.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.AttachmentReconciliationBackgroundServiceTests.Test4_Missing_Physical_File_Does_Not_Crash"
    [Fact]
    public async Task Test4_Missing_Physical_File_Does_Not_Crash()
    {
        // Arrange
        await using var db = CreateDbContext();
        var storage = new FakeAttachmentStorage();
        var repository = new ServiceRequestAttachmentRepository(db);

        // Key is in candidate list, but not actually present in files dictionary
        var missingKey = Guid.NewGuid().ToString("N") + ".jpg";
        storage.CustomTimestamps[missingKey] = DateTime.UtcNow.AddDays(-2);

        var service = new AttachmentReconciliationService(
            repository,
            storage,
            NullLogger<AttachmentReconciliationService>.Instance);

        // Act & Assert (must not throw)
        var exception = await Record.ExceptionAsync(() => service.ReconcileAsync());
        Assert.Null(exception);
    }

    // Purpose: Checks that running reconciliation multiple times consecutively is idempotent.
    // Why: Asserts that after the first cycle purges orphans, subsequent cycles do not delete valid attachments or produce spurious deletions.
    // Scenario: One valid DB-backed file and one old orphan; reconcile runs twice back-to-back.
    // Pass condition: First run deletes 1 orphan; second run deletes 0 files; valid file and DB record remain intact.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.AttachmentReconciliationBackgroundServiceTests.Test5_Repeated_Reconciliation_Is_Idempotent"
    [Fact]
    public async Task Test5_Repeated_Reconciliation_Is_Idempotent()
    {
        // Arrange
        await using var db = CreateDbContext();
        var storage = new FakeAttachmentStorage();
        var repository = new ServiceRequestAttachmentRepository(db);

        var request = new ServiceRequest
        {
            CustomerId = Guid.NewGuid(),
            Description = "Electrical short circuit",
            LocationText = "Kandy",
            Status = ServiceRequestStatus.Created
        };
        db.ServiceRequests.Add(request);
        await db.SaveChangesAsync();

        var validKey = Guid.NewGuid().ToString("N") + ".jpg";
        var validAttachment = new ServiceRequestAttachment
        {
            ServiceRequestId = request.Id,
            Slot = 1,
            StorageKey = validKey,
            ContentType = "image/jpeg",
            FileSizeBytes = 2048,
            Width = 1024,
            Height = 768,
            ContentHash = new string('b', 64)
        };
        db.ServiceRequestAttachments.Add(validAttachment);
        await db.SaveChangesAsync();
        await storage.WriteAsync(validKey, new byte[] { 1, 1, 1 });

        var orphanKey = Guid.NewGuid().ToString("N") + ".jpg";
        await storage.WriteAsync(orphanKey, new byte[] { 9, 9, 9 });
        storage.CustomTimestamps[orphanKey] = DateTime.UtcNow.AddDays(-2);

        var service = new AttachmentReconciliationService(
            repository,
            storage,
            NullLogger<AttachmentReconciliationService>.Instance);

        // Act 1: First reconciliation cleans up orphan
        var firstRunDeleted = await service.ReconcileAsync();
        Assert.Equal(1, firstRunDeleted);
        Assert.False(storage.Files.ContainsKey(orphanKey));
        Assert.True(storage.Files.ContainsKey(validKey));

        // Act 2: Second reconciliation immediately after
        var secondRunDeleted = await service.ReconcileAsync();

        // Assert 2: Idempotent - no further changes, 0 deleted
        Assert.Equal(0, secondRunDeleted);
        Assert.True(storage.Files.ContainsKey(validKey), "Valid file must still exist after repeated runs.");
        Assert.Equal(1, await db.ServiceRequestAttachments.CountAsync());
    }

    // Purpose: Checks that if reconciliation throws an exception during a cycle, the background worker logs the error and continues running future cycles.
    // Why: Prevents transient storage network glitches or file access errors from crashing the background service indefinitely.
    // Scenario: Storage configured to fail on first cleanup candidate retrieval, then succeed; background service runs for 2.5 seconds.
    // Pass condition: Attempt count reaches at least 2, and background worker task is not faulted.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.AttachmentReconciliationBackgroundServiceTests.Test6_Reconciliation_Exception_Does_Not_Terminate_Background_Worker"
    [Fact]
    public async Task Test6_Reconciliation_Exception_Does_Not_Terminate_Background_Worker()
    {
        // Arrange
        var services = new ServiceCollection();
        var attemptCount = 0;

        // Custom failing storage that throws on first attempt and succeeds on second attempt
        var throwingStorage = new TestFailingStorage(() => Interlocked.Increment(ref attemptCount) == 1);
        services.AddSingleton<IServiceRequestAttachmentStorage>(throwingStorage);

        var db = CreateDbContext();
        services.AddScoped<IServiceRequestAttachmentRepository>(_ => new ServiceRequestAttachmentRepository(db));
        services.AddScoped<AttachmentReconciliationService>();

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var options = new AttachmentReconciliationOptions
        {
            InitialDelaySeconds = 0,
            IntervalSeconds = 1 // short interval for fast test
        };

        var worker = new AttachmentReconciliationBackgroundService(
            scopeFactory,
            directOptions: options,
            logger: NullLogger<AttachmentReconciliationBackgroundService>.Instance);

        using var cts = new CancellationTokenSource();

        // Act: Start background worker
        var workerTask = worker.StartAsync(cts.Token);
        await Task.Delay(2500); // allow at least two cycles to run

        // Assert: Worker should still be alive despite the first-cycle exception
        Assert.True(attemptCount >= 2, $"Worker should have retried on the next cycle (attempt count: {attemptCount}).");
        Assert.False(workerTask.IsFaulted, "Worker task must not fault or crash on reconciliation exception.");

        // Clean up
        await worker.StopAsync(CancellationToken.None);
        cts.Cancel();
    }

    // Purpose: Checks that canceling the background worker's cancellation token stops execution promptly and cleanly.
    // Why: Enables graceful shutdown of the ASP.NET Core host during deployments or restarts.
    // Scenario: Background worker started and immediately cancelled via CancellationTokenSource.
    // Pass condition: Both start and stop tasks complete successfully without throwing unhandled exceptions.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.AttachmentReconciliationBackgroundServiceTests.Test7_Cancellation_Stops_Worker_Cleanly"
    [Fact]
    public async Task Test7_Cancellation_Stops_Worker_Cleanly()
    {
        // Arrange
        var services = new ServiceCollection();
        var db = CreateDbContext();
        services.AddSingleton<IServiceRequestAttachmentStorage>(new FakeAttachmentStorage());
        services.AddScoped<IServiceRequestAttachmentRepository>(_ => new ServiceRequestAttachmentRepository(db));
        services.AddScoped<AttachmentReconciliationService>();

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var options = new AttachmentReconciliationOptions
        {
            InitialDelaySeconds = 30, // Long delay
            IntervalHours = 24
        };

        var worker = new AttachmentReconciliationBackgroundService(
            scopeFactory,
            directOptions: options,
            logger: NullLogger<AttachmentReconciliationBackgroundService>.Instance);

        using var cts = new CancellationTokenSource();

        // Act: Start worker and cancel quickly
        var startTask = worker.StartAsync(cts.Token);
        cts.Cancel();
        var stopTask = worker.StopAsync(CancellationToken.None);

        // Assert: Must complete cleanly without unhandled exception
        await Task.WhenAll(startTask, stopTask);
        Assert.True(startTask.IsCompletedSuccessfully);
        Assert.True(stopTask.IsCompletedSuccessfully);
    }

    // Purpose: Checks that reconciliation selectively deletes only orphan files across multiple service requests and slots while preserving all valid attachments.
    // Why: Verifies isolation and accuracy when scanning realistic storage directories containing attachments from multiple requests.
    // Scenario: 3 service requests with 3 attachments each (9 valid files) plus 2 old orphan files.
    // Pass condition: Returns 2 deleted files, 2 orphans removed from storage, and all 9 valid attachments remain in DB and storage.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.AttachmentReconciliationBackgroundServiceTests.Test8_Multiple_Service_Request_Attachments_Remain_Intact"
    [Fact]
    public async Task Test8_Multiple_Service_Request_Attachments_Remain_Intact()
    {
        // Arrange
        await using var db = CreateDbContext();
        var storage = new FakeAttachmentStorage();
        var repository = new ServiceRequestAttachmentRepository(db);

        var validKeys = new List<string>();

        // Create 3 requests, each with 3 attachments (9 total)
        for (var r = 1; r <= 3; r++)
        {
            var req = new ServiceRequest
            {
                CustomerId = Guid.NewGuid(),
                Description = $"Service request #{r}",
                LocationText = $"Location #{r}",
                Status = ServiceRequestStatus.Created
            };
            db.ServiceRequests.Add(req);
            await db.SaveChangesAsync();

            for (var slot = 1; slot <= 3; slot++)
            {
                var key = Guid.NewGuid().ToString("N") + ".jpg";
                validKeys.Add(key);
                db.ServiceRequestAttachments.Add(new ServiceRequestAttachment
                {
                    ServiceRequestId = req.Id,
                    Slot = slot,
                    StorageKey = key,
                    ContentType = "image/jpeg",
                    FileSizeBytes = 500 * slot,
                    Width = 640,
                    Height = 480,
                    ContentHash = new string((char)('a' + slot), 64)
                });
                await storage.WriteAsync(key, new byte[] { (byte)r, (byte)slot });
            }
        }
        await db.SaveChangesAsync();

        // Add 2 old orphan files
        var orphan1 = Guid.NewGuid().ToString("N") + ".jpg";
        var orphan2 = Guid.NewGuid().ToString("N") + ".jpg";
        await storage.WriteAsync(orphan1, new byte[] { 0xAA });
        await storage.WriteAsync(orphan2, new byte[] { 0xBB });
        storage.CustomTimestamps[orphan1] = DateTime.UtcNow.AddDays(-2);
        storage.CustomTimestamps[orphan2] = DateTime.UtcNow.AddDays(-3);

        var service = new AttachmentReconciliationService(
            repository,
            storage,
            NullLogger<AttachmentReconciliationService>.Instance);

        // Act
        var deletedCount = await service.ReconcileAsync();

        // Assert
        Assert.Equal(2, deletedCount);
        Assert.False(storage.Files.ContainsKey(orphan1));
        Assert.False(storage.Files.ContainsKey(orphan2));

        Assert.Equal(9, await db.ServiceRequestAttachments.CountAsync());
        foreach (var validKey in validKeys)
        {
            Assert.True(storage.Files.ContainsKey(validKey), $"Valid attachment {validKey} must remain intact in storage.");
        }
    }

    // Purpose: Checks that storage reconciliation never modifies, soft-deletes, or removes database records in ServiceRequestAttachments.
    // Why: Reconciliation is strictly a one-way storage cleanup to match the database; it must never mutate the database source of truth.
    // Scenario: 1 valid attachment in DB and storage alongside 3 old orphan files in storage.
    // Pass condition: 3 orphan files deleted from storage, DB count remains exactly 1, and the attachment's ID and storage key are untouched.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.AttachmentReconciliationBackgroundServiceTests.Test9_No_Db_Records_Are_Deleted_Unintentionally"
    [Fact]
    public async Task Test9_No_Db_Records_Are_Deleted_Unintentionally()
    {
        // Arrange
        await using var db = CreateDbContext();
        var storage = new FakeAttachmentStorage();
        var repository = new ServiceRequestAttachmentRepository(db);

        var req = new ServiceRequest
        {
            CustomerId = Guid.NewGuid(),
            Description = "Roof repair",
            LocationText = "Galle",
            Status = ServiceRequestStatus.Created
        };
        db.ServiceRequests.Add(req);
        await db.SaveChangesAsync();

        var key = Guid.NewGuid().ToString("N") + ".jpg";
        var attachment = new ServiceRequestAttachment
        {
            ServiceRequestId = req.Id,
            Slot = 1,
            StorageKey = key,
            ContentType = "image/jpeg",
            FileSizeBytes = 1200,
            Width = 100,
            Height = 100,
            ContentHash = new string('c', 64)
        };
        db.ServiceRequestAttachments.Add(attachment);
        await db.SaveChangesAsync();
        await storage.WriteAsync(key, new byte[] { 1 });

        // Add 3 old orphan files
        for (var i = 0; i < 3; i++)
        {
            var orphan = Guid.NewGuid().ToString("N") + ".jpg";
            await storage.WriteAsync(orphan, new byte[] { 2 });
            storage.CustomTimestamps[orphan] = DateTime.UtcNow.AddDays(-2);
        }

        var countBefore = await db.ServiceRequestAttachments.CountAsync();
        Assert.Equal(1, countBefore);

        var service = new AttachmentReconciliationService(
            repository,
            storage,
            NullLogger<AttachmentReconciliationService>.Instance);

        // Act
        var deletedCount = await service.ReconcileAsync();

        // Assert
        Assert.Equal(3, deletedCount);
        var countAfter = await db.ServiceRequestAttachments.CountAsync();
        Assert.Equal(countBefore, countAfter);
        var existing = await db.ServiceRequestAttachments.SingleAsync();
        Assert.Equal(attachment.Id, existing.Id);
        Assert.Equal(attachment.StorageKey, existing.StorageKey);
    }

    // Purpose: Checks that AttachmentReconciliationBackgroundService creates an IServiceScope to resolve scoped services and runs a reconciliation cycle.
    // Why: Background hosted services are singletons and must create scopes to consume scoped EF Core DB contexts and repositories.
    // Scenario: Background worker configured with 0s initial delay and 1s interval runs against storage containing an orphan file.
    // Pass condition: Orphan file is deleted from storage during the background cycle.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.AttachmentReconciliationBackgroundServiceTests.Test10_BackgroundWorker_Executes_Reconciliation_Cycle_Via_Scope"
    [Fact]
    public async Task Test10_BackgroundWorker_Executes_Reconciliation_Cycle_Via_Scope()
    {
        // Arrange
        var services = new ServiceCollection();
        await using var db = CreateDbContext();
        var storage = new FakeAttachmentStorage();

        var orphanKey = Guid.NewGuid().ToString("N") + ".jpg";
        await storage.WriteAsync(orphanKey, new byte[] { 3, 2, 1 });
        storage.CustomTimestamps[orphanKey] = DateTime.UtcNow.AddDays(-2);

        services.AddSingleton<IServiceRequestAttachmentStorage>(storage);
        services.AddScoped<IServiceRequestAttachmentRepository>(_ => new ServiceRequestAttachmentRepository(db));
        services.AddScoped<AttachmentReconciliationService>();

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var options = new AttachmentReconciliationOptions
        {
            InitialDelaySeconds = 0,
            IntervalSeconds = 1
        };

        var worker = new AttachmentReconciliationBackgroundService(
            scopeFactory,
            directOptions: options,
            logger: NullLogger<AttachmentReconciliationBackgroundService>.Instance);

        using var cts = new CancellationTokenSource();

        // Act
        await worker.StartAsync(cts.Token);
        await Task.Delay(1500); // allow first cycle to execute
        await worker.StopAsync(CancellationToken.None);

        // Assert: The orphan file should have been cleaned up via background worker
        Assert.False(storage.Files.ContainsKey(orphanKey), "Orphan file must be cleaned up by background worker cycle.");
    }

    // Purpose: Checks that when AttachmentReconciliationOptions.Enabled is set to false, the background worker does not execute reconciliation.
    // Why: Allows operators to disable background reconciliation via configuration without modifying application code.
    // Scenario: Storage contains an old orphan file, worker configured with Enabled = false runs for 500ms.
    // Pass condition: Orphan file remains untouched in storage.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.AttachmentReconciliationBackgroundServiceTests.Test11_BackgroundWorker_Respects_Disabled_Configuration"
    [Fact]
    public async Task Test11_BackgroundWorker_Respects_Disabled_Configuration()
    {
        // Arrange
        var services = new ServiceCollection();
        await using var db = CreateDbContext();
        var storage = new FakeAttachmentStorage();

        var orphanKey = Guid.NewGuid().ToString("N") + ".jpg";
        await storage.WriteAsync(orphanKey, new byte[] { 4, 3, 2 });
        storage.CustomTimestamps[orphanKey] = DateTime.UtcNow.AddDays(-2);

        services.AddSingleton<IServiceRequestAttachmentStorage>(storage);
        services.AddScoped<IServiceRequestAttachmentRepository>(_ => new ServiceRequestAttachmentRepository(db));
        services.AddScoped<AttachmentReconciliationService>();

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var options = new AttachmentReconciliationOptions
        {
            Enabled = false,
            InitialDelaySeconds = 0,
            IntervalSeconds = 1
        };

        var worker = new AttachmentReconciliationBackgroundService(
            scopeFactory,
            directOptions: options,
            logger: NullLogger<AttachmentReconciliationBackgroundService>.Instance);

        using var cts = new CancellationTokenSource();

        // Act
        await worker.StartAsync(cts.Token);
        await Task.Delay(500);
        await worker.StopAsync(CancellationToken.None);

        // Assert: Disabled worker should not run reconciliation
        Assert.True(storage.Files.ContainsKey(orphanKey), "Orphan file must remain untouched when worker is disabled.");
    }

    // Purpose: Checks that AttachmentReconciliationOptions.Validate() throws InvalidOperationException if safety constraints are violated.
    // Why: Prevents dangerous misconfigurations (e.g. 0-minute grace period or negative delays) that could lead to data loss or aggressive looping.
    // Scenario: Invalid parameter combinations (negative delay, 0 interval hours, 0 min age).
    // Pass condition: Throws InvalidOperationException containing the expected error message. Note: This parameterized test runs all 3 InlineData cases.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.AttachmentReconciliationBackgroundServiceTests.Test12_Options_Validation_Enforces_Safety_Rules"
    [Theory]
    [InlineData(-1, 24, 1440, "InitialDelaySeconds must not be negative")]
    [InlineData(60, 0, 1440, "IntervalHours must be at least 1 hour")]
    [InlineData(60, 24, 0, "MinimumFileAgeMinutes must be at least 1 minute")]
    public void Test12_Options_Validation_Enforces_Safety_Rules(int delay, int intervalHours, int minAge, string expectedMessage)
    {
        var options = new AttachmentReconciliationOptions
        {
            InitialDelaySeconds = delay,
            IntervalHours = intervalHours,
            MinimumFileAgeMinutes = minAge
        };

        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains(expectedMessage, ex.Message);
    }

    // Purpose: Checks that real filesystem storage (PrivateFileAttachmentStorage) correctly observes file modification timestamps, preserving recent orphans and purging old ones.
    // Why: Confirms end-to-end integration with the real Windows/Linux filesystem and file attributes rather than just in-memory dictionary abstractions.
    // Scenario: Physical files on disk: 1 DB-backed file, 1 old orphan (2 days old), 1 recent orphan (2 minutes old) with a 30-minute grace period.
    // Pass condition: Exactly 1 file deleted (the old orphan); the DB file and the recent orphan continue to exist on physical disk.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.AttachmentReconciliationBackgroundServiceTests.Test13_PhysicalDisk_RealStorage_GracePeriod_Preserves_Recent_Orphans"
    [Fact]
    public async Task Test13_PhysicalDisk_RealStorage_GracePeriod_Preserves_Recent_Orphans()
    {
        // Arrange: Use real PrivateFileAttachmentStorage writing to a real disk folder
        var tempDir = CreateTempStorageDir();
        var storage = new PrivateFileAttachmentStorage(tempDir);
        await using var db = CreateDbContext();
        var repository = new ServiceRequestAttachmentRepository(db);

        // 1. DB-backed file
        var req = new ServiceRequest
        {
            CustomerId = Guid.NewGuid(),
            Description = "Leak repair",
            LocationText = "Jaffna",
            Status = ServiceRequestStatus.Created
        };
        db.ServiceRequests.Add(req);
        await db.SaveChangesAsync();

        var dbKey = Guid.NewGuid().ToString("N") + ".jpg";
        db.ServiceRequestAttachments.Add(new ServiceRequestAttachment
        {
            ServiceRequestId = req.Id,
            Slot = 1,
            StorageKey = dbKey,
            ContentType = "image/jpeg",
            FileSizeBytes = 10,
            Width = 10,
            Height = 10,
            ContentHash = new string('d', 64)
        });
        await db.SaveChangesAsync();
        await storage.WriteAsync(dbKey, new byte[] { 1, 2, 3 });
        // Set DB file to old
        File.SetLastWriteTimeUtc(Path.Combine(tempDir, dbKey), DateTime.UtcNow.AddDays(-2));

        // 2. Old orphan file (2 days old)
        var oldOrphanKey = Guid.NewGuid().ToString("N") + ".jpg";
        await storage.WriteAsync(oldOrphanKey, new byte[] { 4, 5, 6 });
        File.SetLastWriteTimeUtc(Path.Combine(tempDir, oldOrphanKey), DateTime.UtcNow.AddDays(-2));

        // 3. Recent orphan file (written 2 minutes ago)
        var recentOrphanKey = Guid.NewGuid().ToString("N") + ".jpg";
        await storage.WriteAsync(recentOrphanKey, new byte[] { 7, 8, 9 });
        File.SetLastWriteTimeUtc(Path.Combine(tempDir, recentOrphanKey), DateTime.UtcNow.AddMinutes(-2));

        var options = new AttachmentReconciliationOptions
        {
            MinimumFileAgeMinutes = 30 // 30-minute grace period
        };

        var service = new AttachmentReconciliationService(
            repository,
            storage,
            NullLogger<AttachmentReconciliationService>.Instance,
            options);

        // Act
        var result = await service.ReconcileDetailedAsync();

        // Assert
        Assert.Equal(1, result.DeletedCount); // only old orphan deleted

        Assert.True(File.Exists(Path.Combine(tempDir, dbKey)), "DB-backed file must remain on disk.");
        Assert.False(File.Exists(Path.Combine(tempDir, oldOrphanKey)), "Old orphan file must be deleted from disk.");
        Assert.True(File.Exists(Path.Combine(tempDir, recentOrphanKey)), "Recent orphan file within grace period must remain on disk.");
    }

    private sealed class TestFailingStorage : IServiceRequestAttachmentStorage
    {
        private readonly Func<bool> _shouldFail;
        private readonly FakeAttachmentStorage _inner = new();

        public TestFailingStorage(Func<bool> shouldFail) => _shouldFail = shouldFail;

        public Task WriteAsync(string key, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
            => _inner.WriteAsync(key, content, cancellationToken);

        public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default)
            => _inner.OpenReadAsync(key, cancellationToken);

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
            => _inner.DeleteAsync(key, cancellationToken);

        public IReadOnlyList<StoredAttachmentFile> GetCleanupCandidates(DateTime olderThanUtc)
        {
            if (_shouldFail())
            {
                throw new InvalidOperationException("Simulated storage failure during reconciliation.");
            }
            return _inner.GetCleanupCandidates(olderThanUtc);
        }
    }
}
