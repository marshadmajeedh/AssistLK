using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace AssistLK.Api.Tests.PostgreSql;

public static class PostgreSqlApiTestDatabase
{
    public const string ApiTestDatabaseName = "assistlk_test_api";

    private static readonly object Lock = new();
    private static bool _isInitialized;

    public static string? ResolveBaseConnectionString()
    {
        var envTest = Environment.GetEnvironmentVariable("ASSISTLK_TEST_POSTGRESQL_CONNECTION");
        if (!string.IsNullOrWhiteSpace(envTest))
        {
            return envTest;
        }

        var envConn = Environment.GetEnvironmentVariable("ConnectionStrings__TestConnection");
        if (!string.IsNullOrWhiteSpace(envConn))
        {
            return envConn;
        }

        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddUserSecrets("15fcbe56-7908-4746-bfbe-0692dc0c8045")
            .Build();

        var configTest = config.GetConnectionString("TestConnection");
        if (!string.IsNullOrWhiteSpace(configTest))
        {
            return configTest;
        }

        var configDefault = config.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(configDefault))
        {
            return configDefault;
        }

        return null;
    }

    public static string GetConnectionString(string databaseName = ApiTestDatabaseName)
    {
        var baseConnectionString = ResolveBaseConnectionString();
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            throw new InvalidOperationException(
                "PostgreSQL test connection string could not be resolved for API tests. " +
                "Configure 'ASSISTLK_TEST_POSTGRESQL_CONNECTION', 'ConnectionStrings__TestConnection', " +
                "or local User Secrets for UserSecretsId '15fcbe56-7908-4746-bfbe-0692dc0c8045'.");
        }

        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Database = databaseName
        };

        return builder.ConnectionString;
    }

    public static void EnsureSafeTestDatabase(string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException(
                "Destructive operations are forbidden: database name cannot be null or whitespace.");
        }

        var lower = databaseName.Trim().ToLowerInvariant();

        if (lower == "assistlk_db" || lower == "postgres" || lower == "template0" || lower == "template1")
        {
            throw new InvalidOperationException(
                $"Destructive operations are strictly forbidden against production or system database '{databaseName}'.");
        }

        bool isAllowed = lower == "assistlk_test_api"
                      || lower == "assistlk_test_integration"
                      || lower.StartsWith("assistlk_mig_test_")
                      || lower.StartsWith("assistlk_test_");

        if (!isAllowed)
        {
            throw new InvalidOperationException(
                $"Refusing destructive operation against unapproved database '{databaseName}'. " +
                "Database name must match approved test names ('assistlk_test_api', 'assistlk_test_integration', 'assistlk_mig_test_*').");
        }
    }

    public static async Task InitializeDatabaseAsync(string databaseName = ApiTestDatabaseName)
    {
        EnsureSafeTestDatabase(databaseName);

        lock (Lock)
        {
            if (_isInitialized)
            {
                return;
            }
        }

        var baseConnectionString = ResolveBaseConnectionString();
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            throw new InvalidOperationException(
                "Cannot initialize PostgreSQL API test database because no valid connection string was found.");
        }

        // Connect to postgres admin database to check/create target test database
        var adminBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Database = "postgres"
        };

        await using (var adminConnection = new NpgsqlConnection(adminBuilder.ConnectionString))
        {
            await adminConnection.OpenAsync();

            var exists = false;
            await using (var checkCmd = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name", adminConnection))
            {
                checkCmd.Parameters.AddWithValue("name", databaseName);
                var result = await checkCmd.ExecuteScalarAsync();
                exists = result != null && result != DBNull.Value;
            }

            if (!exists)
            {
                await using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", adminConnection);
                await createCmd.ExecuteNonQueryAsync();
            }
        }

        // Apply EF Core migrations to ensure schema is fully up to date
        var targetConnectionString = GetConnectionString(databaseName);
        var options = new DbContextOptionsBuilder<AssistLKDbContext>()
            .UseNpgsql(targetConnectionString)
            .Options;

        await using (var context = new AssistLKDbContext(options))
        {
            await context.Database.MigrateAsync();
        }

        lock (Lock)
        {
            _isInitialized = true;
        }
    }

    public static async Task ResetDataAsync(AssistLKDbContext context)
    {
        var databaseName = context.Database.GetDbConnection().Database;
        EnsureSafeTestDatabase(databaseName);

        // Never truncate __EFMigrationsHistory
        const string truncateSql = """
            TRUNCATE TABLE
                "ProblemAnalyses",
                "ServiceRequests",
                "AgentMemories",
                "AgentExecutionMetrics",
                "AgentActions",
                "AgentApprovals",
                "AgentAuditLogs",
                "AgentExecutions",
                "AgentWorkflows",
                "Users"
            RESTART IDENTITY CASCADE;
        """;

        await context.Database.ExecuteSqlRawAsync(truncateSql);
    }

    public static AssistLKDbContext CreateDbContext(string databaseName = ApiTestDatabaseName)
    {
        var connectionString = GetConnectionString(databaseName);
        var options = new DbContextOptionsBuilder<AssistLKDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AssistLKDbContext(options);
    }
}
