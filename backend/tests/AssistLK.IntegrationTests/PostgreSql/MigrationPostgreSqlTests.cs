using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AssistLK.IntegrationTests.PostgreSql;

[Collection("PostgreSqlDatabase")]
public class MigrationPostgreSqlTests
{
    private readonly PostgreSqlTestFixture _fixture;

    public MigrationPostgreSqlTests(PostgreSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RealPostgreSql_MigrationChain_AppliesAllMigrations_AndCreatesRequiredTables()
    {
        _fixture.EnsureAvailable();

        var migrationDbName = $"assistlk_mig_test_{Guid.NewGuid():N}";

        try
        {
            // Verify safe guard passes
            PostgreSqlTestDatabase.EnsureSafeTestDatabase(migrationDbName);

            // Create and migrate database
            await PostgreSqlTestDatabase.InitializeDatabaseAsync(migrationDbName);

            await using var context = PostgreSqlTestDatabase.CreateDbContext(migrationDbName);

            // 1. Verify all migrations are applied
            var appliedMigrations = (await context.Database.GetAppliedMigrationsAsync()).ToList();

            var expectedMigrations = new[]
            {
                "InitialDatabaseFoundation",
                "AddAgentWorkflowFoundation",
                "AddAgentMemory",
                "AddAgentSafetyActions",
                "AddAgentExecutionMetrics",
                "AddServiceRequestAndProblemAnalysis",
                "AddCategoryHintToServiceRequests",
                "AddServiceRequestClarifications",
                "AddServiceRequestLocationSource",
                "AddServiceRequestAttachmentsAndEvidenceRevision"
            };

            Assert.Equal(expectedMigrations.Length, appliedMigrations.Count);

            foreach (var expected in expectedMigrations)
            {
                Assert.Contains(appliedMigrations, m => m.Contains(expected));
            }

            // 2. Verify all tables exist in information_schema
            var expectedTables = new[]
            {
                "__EFMigrationsHistory",
                "Users",
                "ServiceRequests",
                "ProblemAnalyses",
                "ServiceRequestClarifications",
                "ServiceRequestAttachments",
                "AgentWorkflows",
                "AgentExecutions",
                "AgentExecutionMetrics",
                "AgentApprovals",
                "AgentAuditLogs",
                "AgentMemories",
                "AgentActions"
            };

            var conn = (NpgsqlConnection)context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            var actualTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var cmd = new NpgsqlCommand("SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'", conn))
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    actualTables.Add(reader.GetString(0));
                }
            }

            foreach (var table in expectedTables)
            {
                Assert.Contains(table, actualTables);
            }
        }
        finally
        {
            // Drop temporary migration test database
            await PostgreSqlTestDatabase.DropDatabaseAsync(migrationDbName);
        }
    }
}
