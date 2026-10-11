namespace AssistLK.IntegrationTests.PostgreSql;

public class DestructiveSafetyGuardTests
{
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
