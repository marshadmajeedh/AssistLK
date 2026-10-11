using AssistLK.Infrastructure.Data;

namespace AssistLK.IntegrationTests.PostgreSql;

public class PostgreSqlTestFixture : IAsyncLifetime
{
    public bool IsAvailable { get; private set; }
    public string? UnavailableReason { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            var baseConn = PostgreSqlTestDatabase.ResolveBaseConnectionString();
            if (string.IsNullOrWhiteSpace(baseConn))
            {
                IsAvailable = false;
                UnavailableReason = "PostgreSQL test connection string could not be resolved. " +
                    "Set ASSISTLK_TEST_POSTGRESQL_CONNECTION, ConnectionStrings__TestConnection, or local User Secrets.";
                return;
            }

            await PostgreSqlTestDatabase.InitializeDatabaseAsync();
            IsAvailable = true;
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            UnavailableReason = $"Failed to initialize PostgreSQL test database: {ex.Message}";
        }
    }

    public void EnsureAvailable()
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException(UnavailableReason ?? "PostgreSQL test database is unavailable.");
        }
    }

    public AssistLKDbContext CreateDbContext()
    {
        EnsureAvailable();
        return PostgreSqlTestDatabase.CreateDbContext();
    }

    public async Task ResetDataAsync(AssistLKDbContext context)
    {
        EnsureAvailable();
        await PostgreSqlTestDatabase.ResetDataAsync(context);
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
