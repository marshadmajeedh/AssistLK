using AssistLK.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.IntegrationTests.PostgreSql;

public class ServiceJobPersistencePostgreSqlTests : PostgreSqlIntegrationTestBase
{
    public ServiceJobPersistencePostgreSqlTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ServiceJob_WithNullBookingId_PersistsWithAssignedStatus()
    {
        var jobId = Guid.NewGuid();

        await using (var context = CreateDbContext())
        {
            context.ServiceJobs.Add(new ServiceJob
            {
                Id = jobId,
                ServiceRequestId = Guid.NewGuid(),
                ProviderId = Guid.NewGuid(),
                BookingId = null
            });
            await context.SaveChangesAsync();
        }

        await using var readContext = CreateDbContext();
        var saved = await readContext.ServiceJobs.AsNoTracking().SingleAsync(j => j.Id == jobId);

        Assert.Null(saved.BookingId);
        Assert.Equal(ServiceJobStatus.Assigned, saved.Status);
    }

    [Fact]
    public async Task ServiceJob_WithIntegerBookingId_PersistsAndRoundTrips()
    {
        var jobId = Guid.NewGuid();

        await using (var context = CreateDbContext())
        {
            context.ServiceJobs.Add(new ServiceJob { Id = jobId, BookingId = 987654 });
            await context.SaveChangesAsync();
        }

        await using var readContext = CreateDbContext();
        var saved = await readContext.ServiceJobs.AsNoTracking().SingleAsync(j => j.Id == jobId);

        Assert.Equal(987654, saved.BookingId);
    }
}