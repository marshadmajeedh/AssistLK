using Microsoft.EntityFrameworkCore;

namespace AssistLK.IntegrationTests.PostgreSql;

[Collection("PostgreSqlDatabase")]
public class PostgreSqlConnectivityTests
{
    private readonly PostgreSqlTestFixture _fixture;

    public PostgreSqlConnectivityTests(PostgreSqlTestFixture fixture)
    {
        _fixture = fixture;
    }

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
