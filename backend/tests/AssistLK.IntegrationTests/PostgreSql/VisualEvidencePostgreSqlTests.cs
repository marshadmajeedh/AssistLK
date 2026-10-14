using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Application.Services;
using AssistLK.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AssistLK.IntegrationTests.PostgreSql;

// =========================================================================================
// Class: VisualEvidencePostgreSqlTests
// What this class tests:
//   PostgreSQL persistence of jsonb VisualEvidence on ProblemAnalysis and EF Core migration safety.
// Why these tests are needed:
//   Validates that multimodal vision analysis results (vision status, observations, limitations)
//   properly serialize to PostgreSQL jsonb columns, allow in-place updates, and that EF Core
//   migrations safely upgrade legacy pre-multimodal records with sensible defaults.
// What is real vs mocked/faked:
//   - REAL: Live PostgreSQL instance, EF Core jsonb column mapping, and IMigrator executing actual DDL.
//   - MOCKED: None.
// Prerequisites:
//   Running PostgreSQL instance configured via ASSISTLK_TEST_POSTGRESQL_CONNECTION,
//   ConnectionStrings__TestConnection, or local User Secrets.
// Run from repository root:
//   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.PostgreSql.VisualEvidencePostgreSqlTests"
// =========================================================================================
public class VisualEvidencePostgreSqlTests(PostgreSqlTestFixture fixture) : PostgreSqlIntegrationTestBase(fixture)
{
    // Purpose: Verifies that VisualEvidence complex objects round-trip through PostgreSQL jsonb columns and support updates.
    // Why: Vision observations and limitations must persist in JSON without data loss or serialization mismatch.
    // Scenario: Saves problem analysis with visual evidence, verifies persisted JSON, updates limitations, and re-saves.
    // Pass condition: Loaded VisualEvidence retains vision status, attachment IDs, observation text, and updated limitations.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.VisualEvidencePostgreSqlTests.JsonbRoundTripRetainsBoundedValueAndTracksReplacements"
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
            var service = new ServiceRequestService(new ServiceRequestRepository(db), new ProblemAnalysisRepository(db));
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

    // Purpose: Tests migration safety by rolling back to before AddProblemAnalysisVisualEvidence, inserting a legacy row without visual evidence, migrating forward, and verifying default fallback values.
    // Why: Ensures backward compatibility when upgrading existing databases containing pre-multimodal analysis rows.
    // Scenario: Spins up a temporary test database, rolls back migration, inserts raw SQL legacy row, migrates up, verifies defaults (VisionStatus = "not_requested"), rolls back, and migrates up again.
    // Pass condition: Legacy row has VisionStatus = "not_requested", empty arrays, and survives rollback/re-migration with DetectedProblem intact.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.VisualEvidencePostgreSqlTests.MigrationUpDefaultsLegacyRowsAndDownPreservesAnalysis"
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
            var userId = Guid.NewGuid();
            var userNow = DateTime.UtcNow;
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Users\" (\"Id\", \"FullName\", \"Email\", \"PasswordHash\", \"Role\", \"PhoneNumber\", \"IsActive\", \"CreatedAt\", \"UpdatedAt\") VALUES ({userId}, {"Legacy Test"}, {"legacy@example.test"}, {"dummy"}, {"Customer"}, {"0771234567"}, {true}, {userNow}, {userNow})");
            var requestId = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"ServiceRequests\" (\"Id\", \"CustomerId\", \"Description\", \"LocationText\", \"Category\", \"Urgency\", \"Status\", \"LocationSource\", \"EvidenceRevision\", \"CreatedAt\", \"UpdatedAt\") VALUES ({requestId}, {userId}, {"Legacy sink leak"}, {"Colombo"}, {"Unclassified"}, {"Unknown"}, {"Created"}, {"Manual"}, {1L}, {userNow}, {userNow})");
            var id = Guid.NewGuid(); var now = DateTime.UtcNow;
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"ProblemAnalyses\" (\"Id\", \"ServiceRequestId\", \"DetectedProblem\", \"Confidence\", \"AgentName\", \"CreatedAt\", \"UpdatedAt\", \"EvidenceRevision\") VALUES ({id}, {requestId}, {"Possible legacy leak."}, {0.8m}, {"ProblemUnderstandingAgent"}, {now}, {now}, {1L})");
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
