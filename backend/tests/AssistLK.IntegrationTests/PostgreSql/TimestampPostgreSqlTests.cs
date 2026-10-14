using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.IntegrationTests.PostgreSql;

// =========================================================================================
// Class: TimestampPostgreSqlTests
// What this class tests:
//   Automated UTC timestamp management (CreatedAt and UpdatedAt) for ServiceRequest entities in PostgreSQL.
// Why these tests are needed:
//   Validates that EF Core and PostgreSQL timestamp triggers populate UTC timestamps on creation,
//   keep CreatedAt strictly immutable, and advance UpdatedAt whenever an entity is modified.
// What is real vs mocked/faked:
//   - REAL: Live PostgreSQL instance, EF Core ChangeTracker timestamp interceptors, and timestamp with time zone columns.
//   - MOCKED: None.
// Prerequisites:
//   A running PostgreSQL database instance configured via ASSISTLK_TEST_POSTGRESQL_CONNECTION,
//   ConnectionStrings__TestConnection, or local User Secrets.
// Run from repository root:
//   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.PostgreSql.TimestampPostgreSqlTests"
// =========================================================================================
public class TimestampPostgreSqlTests : PostgreSqlIntegrationTestBase
{
    public TimestampPostgreSqlTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    // Purpose: Verifies that newly created ServiceRequest records automatically populate both CreatedAt and UpdatedAt with the current UTC time.
    // Why: Provides reliable audit trails and sorting for customer requests without relying on manual timestamp assignments.
    // Scenario: Inserts a new request without explicit timestamps and reloads it in a fresh context.
    // Pass condition: CreatedAt and UpdatedAt fall within the test execution time window, equal each other, and have DateTimeKind.Utc.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.TimestampPostgreSqlTests.Timestamps_OnCreation_AreSetToUtcNow"
    [Fact]
    public async Task Timestamps_OnCreation_AreSetToUtcNow()
    {
        var customer = await CreateUserAsync();
        var before = DateTime.UtcNow.AddSeconds(-1);

        var requestId = Guid.NewGuid();
        await using (var context = CreateDbContext())
        {
            var request = new ServiceRequest
            {
                Id = requestId,
                CustomerId = customer.Id,
                Category = "Plumbing",
                Description = "Leaking pipe",
                LocationText = "Colombo",
                Urgency = ServiceRequestUrgency.Medium,
                Status = ServiceRequestStatus.Created
            };

            await context.ServiceRequests.AddAsync(request);
            await context.SaveChangesAsync();
        }

        var after = DateTime.UtcNow.AddSeconds(1);

        await using (var verifyContext = CreateDbContext())
        {
            var saved = await verifyContext.ServiceRequests.FindAsync(requestId);
            Assert.NotNull(saved);
            Assert.True(saved.CreatedAt >= before && saved.CreatedAt <= after);
            Assert.True(saved.UpdatedAt >= before && saved.UpdatedAt <= after);
            Assert.Equal(saved.CreatedAt, saved.UpdatedAt);
            Assert.Equal(DateTimeKind.Utc, saved.CreatedAt.Kind);
            Assert.Equal(DateTimeKind.Utc, saved.UpdatedAt.Kind);
        }
    }

    // Purpose: Verifies that modifying an existing ServiceRequest advances UpdatedAt while keeping CreatedAt strictly immutable.
    // Why: Audit compliance requires preserving the original creation date of records while accurately reflecting modification times.
    // Scenario: Updates description and status after a small delay, then asserts timestamps in a fresh context.
    // Pass condition: updated.CreatedAt equals initialCreatedAt, and updated.UpdatedAt is strictly greater than initialUpdatedAt.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.TimestampPostgreSqlTests.Timestamps_OnUpdate_UpdatesUpdatedAt_WhileCreatedAtRemainsImmutable"
    [Fact]
    public async Task Timestamps_OnUpdate_UpdatesUpdatedAt_WhileCreatedAtRemainsImmutable()
    {
        var customer = await CreateUserAsync();
        var requestId = Guid.NewGuid();

        await using (var context = CreateDbContext())
        {
            var request = new ServiceRequest
            {
                Id = requestId,
                CustomerId = customer.Id,
                Category = "Plumbing",
                Description = "Initial description",
                LocationText = "Colombo",
                Urgency = ServiceRequestUrgency.Medium,
                Status = ServiceRequestStatus.Created
            };

            await context.ServiceRequests.AddAsync(request);
            await context.SaveChangesAsync();
        }

        DateTime initialCreatedAt;
        DateTime initialUpdatedAt;

        await using (var readContext = CreateDbContext())
        {
            var initial = await readContext.ServiceRequests.FindAsync(requestId);
            Assert.NotNull(initial);
            initialCreatedAt = initial.CreatedAt;
            initialUpdatedAt = initial.UpdatedAt;
        }

        // Wait a short moment to ensure a distinct timestamp
        await Task.Delay(50);

        await using (var updateContext = CreateDbContext())
        {
            var req = await updateContext.ServiceRequests.FindAsync(requestId);
            Assert.NotNull(req);

            req.Description = "Updated description with more details";
            req.Status = ServiceRequestStatus.Analyzed;

            await updateContext.SaveChangesAsync();
        }

        await using (var verifyContext = CreateDbContext())
        {
            var updated = await verifyContext.ServiceRequests.FindAsync(requestId);
            Assert.NotNull(updated);

            // CreatedAt must remain identical
            Assert.Equal(initialCreatedAt, updated.CreatedAt);

            // UpdatedAt must be greater than initial UpdatedAt
            Assert.True(updated.UpdatedAt > initialUpdatedAt,
                $"Expected UpdatedAt ({updated.UpdatedAt}) to be strictly greater than initial UpdatedAt ({initialUpdatedAt}).");
        }
    }
}
