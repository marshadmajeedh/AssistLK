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
