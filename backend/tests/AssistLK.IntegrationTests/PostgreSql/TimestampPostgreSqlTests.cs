using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.IntegrationTests.PostgreSql;

public class TimestampPostgreSqlTests : PostgreSqlIntegrationTestBase
{
    public TimestampPostgreSqlTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

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
