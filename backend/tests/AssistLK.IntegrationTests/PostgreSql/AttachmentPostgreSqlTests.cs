using AssistLK.Application.Attachments;
using AssistLK.Application.Common.Exceptions;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using AssistLK.Infrastructure.Repositories;
using AssistLK.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssistLK.IntegrationTests.PostgreSql;

// =========================================================================================
// Class: AttachmentPostgreSqlTests
// What this class tests:
//   PostgreSQL concurrency, database check constraints, cascade deletes, and binary reconciliation
//   for ServiceRequestAttachment entities.
// Why these tests are needed:
//   Validates that PostgreSQL transactions and optimistic concurrency control enforce the 3-slot
//   attachment ceiling, prevent stale status overwrites when evidence revisions advance,
//   enforce check constraints (slots 1..3, dimensions > 0), and clean up orphaned files.
// What is real vs mocked/faked:
//   - REAL: Live PostgreSQL instance, EF Core repository, transactions, and check constraints.
//   - FAKED: Attachment blob storage is faked via FakeAttachmentStorage; image normalization is faked via Normalizer.
// Prerequisites:
//   A running PostgreSQL instance configured via ASSISTLK_TEST_POSTGRESQL_CONNECTION,
//   ConnectionStrings__TestConnection, or local User Secrets.
// Run from repository root:
//   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.PostgreSql.AttachmentPostgreSqlTests"
// =========================================================================================
public class AttachmentPostgreSqlTests(PostgreSqlTestFixture fixture) : PostgreSqlIntegrationTestBase(fixture)
{
    private sealed class Normalizer(Func<Task>? before = null) : IAttachmentImageNormalizer
    {
        public async Task<NormalizedImage> NormalizeAsync(Stream input, string fileName, string contentType, CancellationToken cancellationToken = default)
        {
            if (before != null) await before();
            return new(new byte[] { 1, 2, 3 }, 20, 10, new string('a', 64));
        }
    }
    private static ServiceRequestAttachmentService Service(AssistLKDbContext db, FakeAttachmentStorage storage, IAttachmentImageNormalizer? normalizer = null)
        => new(new ServiceRequestAttachmentRepository(db), storage, normalizer ?? new Normalizer(), NullLogger<ServiceRequestAttachmentService>.Instance);

    private async Task<ServiceRequest> Seed()
    {
        var user = await CreateUserAsync();
        await using var db = CreateDbContext();
        var request = new ServiceRequest { CustomerId = user.Id, Description = "Leaking kitchen pipe", LocationText = "Colombo" };
        db.ServiceRequests.Add(request); await db.SaveChangesAsync(); return request;
    }

    // Purpose: Verifies that 4 concurrent upload requests resolve safely so only 1 succeeds on an empty slot, subsequent uploads fill remaining slots up to 3, and a 4th upload fails with ConflictException.
    // Why: Business rule: Maximum 3 attachments per service request; concurrent requests must not bypass this limit or leave orphaned files in storage.
    // Scenario: 4 concurrent uploads execute against a single request, followed by filling slots 2 and 3, then attempting a 4th upload.
    // Pass condition: First wave has exactly 1 success; after filling 3 slots, further uploads throw ConflictException, slots 1..3 exist, and EvidenceRevision is 4.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.AttachmentPostgreSqlTests.ConcurrentUploadsConflictWithoutOrphansAndNeverExceedThreeSlots"
    [Fact]
    public async Task ConcurrentUploadsConflictWithoutOrphansAndNeverExceedThreeSlots()
    {
        var request = await Seed(); var storage = new FakeAttachmentStorage();
        var allRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readers = 0;
        var normalizer = new Normalizer(async () =>
        {
            if (Interlocked.Increment(ref readers) == 4) allRead.SetResult();
            await allRead.Task.WaitAsync(TimeSpan.FromSeconds(15));
        });
        async Task<bool> Upload()
        {
            await using var db = CreateDbContext();
            try { await Service(db, storage, normalizer).UploadAsync(request.Id, request.CustomerId, Stream.Null, "a.jpg", "image/jpeg"); return true; }
            catch (ConflictException) { return false; }
        }
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Upload()));
        Assert.Single(results.Where(x => x));
        Assert.Single(storage.Files);
        for (var i = 0; i < 2; i++)
        {
            await using var db = CreateDbContext();
            await Service(db, storage).UploadAsync(request.Id, request.CustomerId, Stream.Null, "a.jpg", "image/jpeg");
        }
        await using var verify = CreateDbContext();
        await Assert.ThrowsAsync<ConflictException>(() => Service(verify, storage).UploadAsync(request.Id, request.CustomerId, Stream.Null, "a.jpg", "image/jpeg"));
        Assert.Equal(new[] { 1, 2, 3 }, await verify.ServiceRequestAttachments.OrderBy(a => a.Slot).Select(a => a.Slot).ToArrayAsync());
        Assert.Equal(4, (await verify.ServiceRequests.SingleAsync()).EvidenceRevision);
        Assert.Equal(3, storage.Files.Count);
    }

    // Purpose: Verifies that if the request status transitions to Analyzing or Cancelled while an upload is normalizing, the upload fails with ConflictException and rolls back stored files.
    // Why: Attachments cannot be added while an analysis is in progress or after a request is cancelled; compensations must delete the uploaded blob.
    // Scenario: Parameterized test for Analyzing and Cancelled statuses simulated mid-upload.
    // Pass condition: Throws ConflictException, database attachments table is empty, and storage files collection is empty (compensated).
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.PostgreSql.AttachmentPostgreSqlTests.ConcurrentStatusTransitionRollsBackUploadAndCompensates"
    // Note: Parameterized theory test; this command executes both inline data cases.
    [Theory]
    [InlineData(ServiceRequestStatus.Analyzing)]
    [InlineData(ServiceRequestStatus.Cancelled)]
    public async Task ConcurrentStatusTransitionRollsBackUploadAndCompensates(ServiceRequestStatus status)
    {
        var request = await Seed(); var storage = new FakeAttachmentStorage();
        var normalizer = new Normalizer(async () =>
        {
            await using var statusDb = CreateDbContext();
            (await statusDb.ServiceRequests.SingleAsync()).Status = status;
            await statusDb.SaveChangesAsync();
        });
        await using var db = CreateDbContext();
        await Assert.ThrowsAsync<ConflictException>(() => Service(db, storage, normalizer).UploadAsync(request.Id, request.CustomerId, Stream.Null, "a.jpg", "image/jpeg"));
        await using var verify = CreateDbContext();
        Assert.Empty(await verify.ServiceRequestAttachments.ToListAsync());
        Assert.Empty(storage.Files);
        var saved = await verify.ServiceRequests.SingleAsync();
        Assert.Equal(status, saved.Status); Assert.Equal(1, saved.EvidenceRevision);
    }

    // Purpose: Checks that optimistic concurrency control prevents a stale process from updating request status when a new attachment has incremented EvidenceRevision.
    // Why: Protects evidence consistency; analysis should only run against the specific evidence revision it inspected.
    // Scenario: Process A reads request snapshot; Process B uploads a new photo (incrementing EvidenceRevision); Process A tries to set status to Analyzing.
    // Pass condition: Process A throws DbUpdateConcurrencyException, and database retains Status = Created and EvidenceRevision = 2.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.AttachmentPostgreSqlTests.StaleLifecycleWriterCannotOverwriteNewEvidence"
    [Fact]
    public async Task StaleLifecycleWriterCannotOverwriteNewEvidence()
    {
        var request = await Seed(); var storage = new FakeAttachmentStorage();
        await using var stale = CreateDbContext();
        var snapshot = await stale.ServiceRequests.SingleAsync();
        await using (var fresh = CreateDbContext())
            await Service(fresh, storage).UploadAsync(request.Id, request.CustomerId, Stream.Null, "a.jpg", "image/jpeg");
        snapshot.Status = ServiceRequestStatus.Analyzing;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
        await using var verify = CreateDbContext();
        Assert.Equal(ServiceRequestStatus.Created, (await verify.ServiceRequests.SingleAsync()).Status);
        Assert.Equal(2, (await verify.ServiceRequests.SingleAsync()).EvidenceRevision);
    }

    // Purpose: Verifies that PostgreSQL database-level check constraints block invalid slots (0, 4), duplicates, zero file bytes, zero dimensions, and negative revisions.
    // Why: Database-level constraints are the last line of defense against corrupted data bypassing application logic.
    // Scenario: Parameterized test attempting to save invalid slot values, duplicate keys/slots, zero bytes, zero dimensions, and negative revisions.
    // Pass condition: Every invalid case throws DbUpdateException from PostgreSQL.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.PostgreSql.AttachmentPostgreSqlTests.DatabaseEnforcesAttachmentConstraints"
    // Note: Parameterized theory test; this command executes all 8 inline data cases.
    [Theory]
    [InlineData("slot-zero")][InlineData("slot-four")][InlineData("duplicate-slot")][InlineData("duplicate-key")]
    [InlineData("bytes")][InlineData("width")][InlineData("height")][InlineData("revision")]
    public async Task DatabaseEnforcesAttachmentConstraints(string invalid)
    {
        var request = await Seed(); var storage = new FakeAttachmentStorage();
        await using var db = CreateDbContext();
        await Service(db, storage).UploadAsync(request.Id, request.CustomerId, Stream.Null, "a.jpg", "image/jpeg");
        var original = await db.ServiceRequestAttachments.SingleAsync();
        var row = new ServiceRequestAttachment { ServiceRequestId = request.Id, Slot = 2, StorageKey = Guid.NewGuid().ToString("N") + ".jpg", FileSizeBytes = 3, Width = 20, Height = 10, ContentHash = new string('b', 64) };
        switch (invalid)
        {
            case "slot-zero": row.Slot = 0; break;
            case "slot-four": row.Slot = 4; break;
            case "duplicate-slot": row.Slot = 1; break;
            case "duplicate-key": row.StorageKey = original.StorageKey; break;
            case "bytes": row.FileSizeBytes = 0; break;
            case "width": row.Width = 0; break;
            case "height": row.Height = 0; break;
            case "revision": (await db.ServiceRequests.SingleAsync()).EvidenceRevision = -1; break;
        }
        db.ServiceRequestAttachments.Add(row);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    // Purpose: Verifies that hard-deleting a ServiceRequest cascades to delete its attachment metadata rows in PostgreSQL, while orphaned binary files in storage are cleaned up by AttachmentReconciliationService.
    // Why: Foreign key cascade removes database rows, but physical files in blob storage require background reconciliation to prevent storage bloat.
    // Scenario: Deletes a request with an attachment, checks DB cascade, verifies file still in storage, runs reconciliation service.
    // Pass condition: Attachments table is empty, storage has 1 file initially, reconciliation deletes it and returns 1 reconciled file.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.AttachmentPostgreSqlTests.InternalHardDeleteCascadesMetadataButRequiresBinaryReconciliation"
    [Fact]
    public async Task InternalHardDeleteCascadesMetadataButRequiresBinaryReconciliation()
    {
        var request = await Seed(); var storage = new FakeAttachmentStorage();
        await using (var db = CreateDbContext())
            await Service(db, storage).UploadAsync(request.Id, request.CustomerId, Stream.Null, "a.jpg", "image/jpeg");
        await using (var db = CreateDbContext())
        {
            db.ServiceRequests.Remove(await db.ServiceRequests.SingleAsync());
            await db.SaveChangesAsync();
        }
        await using var verify = CreateDbContext();
        Assert.Empty(await verify.ServiceRequestAttachments.ToListAsync());
        Assert.Single(storage.Files);
        var cleanup = new AttachmentReconciliationService(new ServiceRequestAttachmentRepository(verify), storage, NullLogger<AttachmentReconciliationService>.Instance);
        Assert.Equal(1, await cleanup.ReconcileAsync()); Assert.Empty(storage.Files);
    }
}
