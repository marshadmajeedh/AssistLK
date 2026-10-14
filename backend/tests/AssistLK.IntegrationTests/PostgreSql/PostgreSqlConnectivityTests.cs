using Microsoft.EntityFrameworkCore;

namespace AssistLK.IntegrationTests.PostgreSql;

// =========================================================================================
// Class: PostgreSqlConnectivityTests
// What this class tests:
//   PostgreSQL database connectivity and EF Core migration verification for the integration test suite.
// Why these tests are needed:
//   Ensures that the test suite can connect to a real PostgreSQL instance and that database
//   migrations (including initial schema and AddServiceRequestAndProblemAnalysis) are applied
//   before database tests execute.
// What is real vs mocked/faked:
//   - REAL: Live PostgreSQL connection, Npgsql socket communication, and EF Core __EFMigrationsHistory queries.
//   - MOCKED: None.
// Prerequisites:
//   A running PostgreSQL database instance with connection string configured in
//   ASSISTLK_TEST_POSTGRESQL_CONNECTION, ConnectionStrings__TestConnection, or local User Secrets.
// Run from repository root:
//   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.PostgreSql.PostgreSqlConnectivityTests"
// =========================================================================================
[Collection("PostgreSqlDatabase")]
public class PostgreSqlConnectivityTests
{
    private readonly PostgreSqlTestFixture _fixture;

    public PostgreSqlConnectivityTests(PostgreSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    // Purpose: Checks that EF Core connects to the PostgreSQL test database and confirms migrations are applied.
    // Why: Verifies database availability and schema setup; without applied migrations, downstream integration tests will fail.
    // Scenario: Connects to the test database and reads applied migrations.
    // Pass condition: CanConnectAsync returns true, and applied migrations list contains "AddServiceRequestAndProblemAnalysis".
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.PostgreSqlConnectivityTests.CanConnectToRealPostgreSqlDatabase_AndQueryMigrations"
    [Fact]
    public async Task CanConnectToRealPostgreSqlDatabase_AndQueryMigrations()
    {
        _fixture.EnsureAvailable();

        await using var context = _fixture.CreateDbContext();
        var canConnect = await context.Database.CanConnectAsync();
        Assert.True(canConnect, "Expected to successfully connect to real PostgreSQL test database.");

        var appliedMigrations = (await context.Database.GetAppliedMigrationsAsync()).ToList();
        Assert.NotEmpty(appliedMigrations);
        Assert.Contains(appliedMigrations, m => m.Contains("AddServiceRequestAndProblemAnalysis"));
    }
}
