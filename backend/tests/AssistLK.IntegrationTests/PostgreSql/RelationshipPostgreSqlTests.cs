using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.IntegrationTests.PostgreSql;

public class RelationshipPostgreSqlTests : PostgreSqlIntegrationTestBase
{
    public RelationshipPostgreSqlTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task UserDelete_WithExistingServiceRequest_ThrowsDbUpdateExceptionDueToRestrict()
    {
        var customer = await CreateUserAsync();

        await using (var context = CreateDbContext())
        {
            var request = new ServiceRequest
            {
                CustomerId = customer.Id,
                Category = "Plumbing",
                Description = "Leaking faucet",
                LocationText = "Colombo",
                Urgency = ServiceRequestUrgency.Low,
                Status = ServiceRequestStatus.Created
            };
            await context.ServiceRequests.AddAsync(request);
            await context.SaveChangesAsync();
        }

        // Attempt to delete user
        await using (var deleteContext = CreateDbContext())
        {
            var userToDelete = await deleteContext.Users.FindAsync(customer.Id);
            Assert.NotNull(userToDelete);

            deleteContext.Users.Remove(userToDelete);
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => deleteContext.SaveChangesAsync());
            Assert.NotNull(ex.InnerException);
        }
    }

    [Fact]
    public async Task ServiceRequestDelete_CascadesAndDeletesAssociatedProblemAnalyses()
    {
        var customer = await CreateUserAsync();
        var requestId = Guid.NewGuid();
        var analysisId = Guid.NewGuid();

        await using (var context = CreateDbContext())
        {
            var request = new ServiceRequest
            {
                Id = requestId,
                CustomerId = customer.Id,
                Category = "Electrical",
                Description = "Sparking switch",
                LocationText = "Kandy",
                Urgency = ServiceRequestUrgency.High,
                Status = ServiceRequestStatus.Analyzed
            };
            await context.ServiceRequests.AddAsync(request);

            var analysis = new ProblemAnalysis
            {
                Id = analysisId,
                ServiceRequestId = requestId,
                DetectedProblem = "Damaged wall switch contacts",
                Confidence = 0.9m,
                AgentName = "ProblemUnderstandingAgent"
            };
            await context.ProblemAnalyses.AddAsync(analysis);
            await context.SaveChangesAsync();
        }

        // Delete ServiceRequest
        await using (var deleteContext = CreateDbContext())
        {
            var req = await deleteContext.ServiceRequests.FindAsync(requestId);
            Assert.NotNull(req);

            deleteContext.ServiceRequests.Remove(req);
            await deleteContext.SaveChangesAsync();
        }

        // Verify ProblemAnalysis is cascade deleted
        await using (var verifyContext = CreateDbContext())
        {
            var analysis = await verifyContext.ProblemAnalyses.FindAsync(analysisId);
            Assert.Null(analysis);
        }
    }

    [Fact]
    public async Task ServiceRequest_CanEagerlyLoadCustomerAndProblemAnalyses()
    {
        var customer = await CreateUserAsync(fullName: "Praveen Silva");
        var requestId = Guid.NewGuid();

        await using (var context = CreateDbContext())
        {
            var request = new ServiceRequest
            {
                Id = requestId,
                CustomerId = customer.Id,
                Category = "Appliance Repair",
                Description = "Microwave doesn't heat",
                LocationText = "Dehiwala",
                Urgency = ServiceRequestUrgency.Low,
                Status = ServiceRequestStatus.Analyzed
            };
            await context.ServiceRequests.AddAsync(request);

            var analysis = new ProblemAnalysis
            {
                ServiceRequestId = requestId,
                DetectedProblem = "Defective magnetron",
                Confidence = 0.88m,
                AgentName = "ProblemUnderstandingAgent"
            };
            await context.ProblemAnalyses.AddAsync(analysis);
            await context.SaveChangesAsync();
        }

        await using (var queryContext = CreateDbContext())
        {
            var loaded = await queryContext.ServiceRequests
                .Include(r => r.Customer)
                .Include(r => r.ProblemAnalyses)
                .FirstOrDefaultAsync(r => r.Id == requestId);

            Assert.NotNull(loaded);
            Assert.NotNull(loaded.Customer);
            Assert.Equal("Praveen Silva", loaded.Customer.FullName);
            Assert.Single(loaded.ProblemAnalyses);
            Assert.Equal("Defective magnetron", loaded.ProblemAnalyses.First().DetectedProblem);
        }
    }
}
