using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Application.Services;
using AssistLK.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AssistLK.IntegrationTests.PostgreSql;

public class VisualEvidencePostgreSqlTests(PostgreSqlTestFixture fixture) : PostgreSqlIntegrationTestBase(fixture)
{
    [Fact]
    public async Task JsonbRoundTripRetainsBoundedValueAndTracksReplacements()
    {
        var user = await CreateUserAsync();
        var id = Guid.NewGuid(); var photo = Guid.NewGuid();
        await using (var db = CreateDbContext())
        {
            var request = new ServiceRequest { Id = id, CustomerId = user.Id, Description = "Possible sink leak", LocationText = "Colombo" };
            db.Add(request);
            db.Add(new ServiceRequestAttachment { Id = photo, ServiceRequestId = id, Slot = 1,
                StorageKey = Guid.NewGuid().ToString("N") + ".jpg", ContentHash = new string('a', 64), FileSizeBytes = 100, Width = 80, Height = 40 });
            await db.SaveChangesAsync();
            var service = new ServiceRequestService(new ServiceRequestRepository(db), new ProblemAnalysisRepository(db), new TestDoubles.InMemoryServiceJobRepository());
            await service.ApplyProblemAnalysisResultAsync(new()
            {
                ServiceRequestId = id, EvidenceRevision = 1, SuppliedAttachmentIds = [photo],
                Category = "Plumbing", DetectedProblem = "Possible leak.", Confidence = .85m, Urgency = ServiceRequestUrgency.Medium,
                AgentName = "ProblemUnderstandingAgent", VisualEvidence = new()
                {
                    VisionStatus = "used", AttachmentIdsUsed = [photo],
                    Observations = [new() { AttachmentId = photo, Observation = "Moisture appears visible." }],
                    Limitations = ["The internal cause is not visible."]
                }
            });
        }
        await using (var db = CreateDbContext())
        {
            var analysis = await db.ProblemAnalyses.SingleAsync(a => a.ServiceRequestId == id);
            Assert.Equal("used", analysis.VisualEvidence.VisionStatus);
            Assert.Equal(photo, Assert.Single(analysis.VisualEvidence.AttachmentIdsUsed));
            Assert.Equal("Moisture appears visible.", Assert.Single(analysis.VisualEvidence.Observations).Observation);
            Assert.Single(analysis.VisualEvidence.Limitations);
            analysis.VisualEvidence = new() { VisionStatus = "used", AttachmentIdsUsed = [photo],
                Observations = analysis.VisualEvidence.Observations, Limitations = ["The joint is partially obscured."] };
            await db.SaveChangesAsync();
        }
        await using (var db = CreateDbContext())
            Assert.Equal("The joint is partially obscured.", (await db.ProblemAnalyses.SingleAsync(a => a.ServiceRequestId == id)).VisualEvidence.Limitations[0]);
    }

    [Fact]
    public async Task MigrationUpDefaultsLegacyRowsAndDownPreservesAnalysis()
    {
        Fixture.EnsureAvailable();
        var database = $"assistlk_mig_test_{Guid.NewGuid():N}";
        try
        {
            PostgreSqlTestDatabase.EnsureSafeTestDatabase(database);
            await PostgreSqlTestDatabase.InitializeDatabaseAsync(database);
            await using var db = PostgreSqlTestDatabase.CreateDbContext(database);
            var migrator = db.GetService<IMigrator>();
            var migrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
            var targetIndex = Array.FindIndex(migrations, m => m.EndsWith("AddProblemAnalysisVisualEvidence", StringComparison.Ordinal));
            Assert.True(targetIndex > 0, "Expected AddProblemAnalysisVisualEvidence migration to exist in applied migrations after an earlier migration.");
            var previous = migrations[targetIndex - 1];
            await migrator.MigrateAsync(previous);
            var user = new User { FullName = "Legacy Test", Email = "legacy@example.test", PasswordHash = "dummy", PhoneNumber = "0771234567" };
            var request = new ServiceRequest { CustomerId = user.Id, Description = "Legacy sink leak", LocationText = "Colombo" };
            db.Add(user); db.Add(request); await db.SaveChangesAsync();
            var id = Guid.NewGuid(); var now = DateTime.UtcNow;
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"ProblemAnalyses\" (\"Id\", \"ServiceRequestId\", \"DetectedProblem\", \"Confidence\", \"AgentName\", \"CreatedAt\", \"UpdatedAt\", \"EvidenceRevision\") VALUES ({id}, {request.Id}, {"Possible legacy leak."}, {0.8m}, {"ProblemUnderstandingAgent"}, {now}, {now}, {1L})");
            await migrator.MigrateAsync();
            var legacy = await db.ProblemAnalyses.SingleAsync(a => a.Id == id);
            Assert.Equal("not_requested", legacy.VisualEvidence.VisionStatus);
            Assert.Empty(legacy.VisualEvidence.AttachmentIdsUsed);
            Assert.Empty(legacy.VisualEvidence.Observations);
            await migrator.MigrateAsync(previous);
            var pendingMigrations = (await db.Database.GetPendingMigrationsAsync()).ToList();
            Assert.Contains(pendingMigrations, m => m.EndsWith("AddProblemAnalysisVisualEvidence", StringComparison.Ordinal));
            await migrator.MigrateAsync();
            db.ChangeTracker.Clear();
            Assert.Equal("Possible legacy leak.", (await db.ProblemAnalyses.SingleAsync(a => a.Id == id)).DetectedProblem);
        }
        finally { await PostgreSqlTestDatabase.DropDatabaseAsync(database); }
    }
}
