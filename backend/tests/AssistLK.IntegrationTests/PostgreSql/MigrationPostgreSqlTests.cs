using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AssistLK.IntegrationTests.PostgreSql;

// =========================================================================================
// Class: MigrationPostgreSqlTests
// What this class tests:
//   Complete EF Core migration execution chain on real PostgreSQL from initial foundation to latest schema.
// Why these tests are needed:
//   Proves that an empty, newly provisioned PostgreSQL database successfully applies every migration
//   in order (workflows, memory, service requests, clarifications, attachments, visual evidence)
//   without DDL syntax errors or migration ordering issues.
// What is real vs mocked/faked:
//   - REAL: Live PostgreSQL instance creating a temporary database (assistlk_mig_test_*), running
//     actual DDL migrations, querying information_schema.tables, and dropping the database upon completion.
//   - MOCKED: None.
// Prerequisites:
//   A running PostgreSQL instance with permissions to create and drop test databases, configured
//   via ASSISTLK_TEST_POSTGRESQL_CONNECTION, ConnectionStrings__TestConnection, or local User Secrets.
// Run from repository root:
//   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.PostgreSql.MigrationPostgreSqlTests"
// =========================================================================================
[Collection("PostgreSqlDatabase")]
public class MigrationPostgreSqlTests
{
    private readonly PostgreSqlTestFixture _fixture;

    public MigrationPostgreSqlTests(PostgreSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    // Purpose: Verifies that the entire EF Core migration chain applies cleanly to an empty PostgreSQL database and creates all 13 expected tables.
    // Why: Validates deployment readiness; confirms a brand-new database installation reaches the desired schema without failure.
    // Scenario: Creates a unique temporary database, applies all pending migrations, queries applied migrations, and inspects information_schema.tables.
    // Pass condition: All 11 expected migration names exist in __EFMigrationsHistory and all 13 expected table names exist in the public schema.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.MigrationPostgreSqlTests.RealPostgreSql_MigrationChain_AppliesAllMigrations_AndCreatesRequiredTables"
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
                "AddServiceRequestAttachmentsAndEvidenceRevision",
                "AddProblemAnalysisVisualEvidence"
            };

            foreach (var expected in expectedMigrations)
            {
                Assert.Contains(
                    appliedMigrations,
                    migration => migration.EndsWith(expected, StringComparison.Ordinal));
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
