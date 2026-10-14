namespace AssistLK.IntegrationTests.PostgreSql;

// =========================================================================================
// Class: DestructiveSafetyGuardTests
// What this class tests:
//   Safety guard validation in PostgreSqlTestDatabase.EnsureSafeTestDatabase(dbName).
// Why these tests are needed:
//   Critical safety protection: Ensures automated test database resets and schema drops
//   can NEVER execute against production, development, or default system databases.
// What is real vs mocked/faked:
//   - REAL: String-matching whitelist validation logic in PostgreSqlTestDatabase.
//   - MOCKED: None (in-memory logic check; requires no database connection).
// Prerequisites:
//   None. Runs entirely in-memory.
// Run from repository root:
//   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.PostgreSql.DestructiveSafetyGuardTests"
// =========================================================================================
public class DestructiveSafetyGuardTests
{
    // Purpose: Verifies that unapproved database names (production, postgres, templates, blank) throw InvalidOperationException.
    // Why: Guarantees that destructive operations (reset/truncate) cannot run against production or system databases.
    // Scenario: Passes unapproved names like "assistlk_db", "production_db", "postgres", empty strings.
    // Pass condition: Throws InvalidOperationException with a non-empty message for each unapproved database name.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.PostgreSql.DestructiveSafetyGuardTests.EnsureSafeTestDatabase_ThrowsInvalidOperationException_ForUnapprovedDatabases"
    // Note: Parameterized theory test; this command executes all 9 inline data cases.
    [Theory]
    [InlineData("assistlk_db")]
    [InlineData("postgres")]
    [InlineData("template0")]
    [InlineData("template1")]
    [InlineData("production_db")]
    [InlineData("my_custom_db")]
    [InlineData("assistlk_production")]
    [InlineData("")]
    [InlineData("   ")]
    public void EnsureSafeTestDatabase_ThrowsInvalidOperationException_ForUnapprovedDatabases(string dbName)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PostgreSqlTestDatabase.EnsureSafeTestDatabase(dbName));

        Assert.NotEmpty(ex.Message);
    }

    // Purpose: Verifies that approved test database names (e.g., assistlk_test_*, assistlk_mig_test_*) are permitted.
    // Why: Ensures legitimate test database executions and resets proceed without false alarms.
    // Scenario: Passes approved test database names like "assistlk_test_integration" and "assistlk_mig_test_12345".
    // Pass condition: Method executes without throwing any exception.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.PostgreSql.DestructiveSafetyGuardTests.EnsureSafeTestDatabase_AllowsApprovedTestDatabases"
    // Note: Parameterized theory test; this command executes all 4 inline data cases.
    [Theory]
    [InlineData("assistlk_test_integration")]
    [InlineData("assistlk_test_api")]
    [InlineData("assistlk_mig_test_12345")]
    [InlineData("assistlk_test_custom_runner")]
    public void EnsureSafeTestDatabase_AllowsApprovedTestDatabases(string dbName)
    {
        // Should not throw
        PostgreSqlTestDatabase.EnsureSafeTestDatabase(dbName);
    }
}
