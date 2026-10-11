using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.IntegrationTests.PostgreSql;

public class ProblemAnalysisPersistencePostgreSqlTests : PostgreSqlIntegrationTestBase
{
    public ProblemAnalysisPersistencePostgreSqlTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ProblemAnalysis_SupportsMultipleAnalysesPerRequest_AndMaintains1ToNRelation()
    {
        var customer = await CreateUserAsync();
        var requestId = Guid.NewGuid();

        await using (var context = CreateDbContext())
        {
            var request = new ServiceRequest
            {
                Id = requestId,
                CustomerId = customer.Id,
                Category = "Appliance Repair",
                Description = "Washing machine won't spin",
                LocationText = "Galle",
                Urgency = ServiceRequestUrgency.Medium,
                Status = ServiceRequestStatus.Analyzed
            };
            await context.ServiceRequests.AddAsync(request);
            await context.SaveChangesAsync();

            var analysis1 = new ProblemAnalysis
            {
                ServiceRequestId = requestId,
                DetectedProblem = "Initial analysis: probable belt failure",
                Confidence = 0.7500m,
                AgentName = "ProblemUnderstandingAgent"
            };
            await context.ProblemAnalyses.AddAsync(analysis1);
            await context.SaveChangesAsync();

            // Small delay to ensure timestamp progression
            await Task.Delay(50);

            var analysis2 = new ProblemAnalysis
            {
                ServiceRequestId = requestId,
                DetectedProblem = "Updated analysis: motor capacitor failure confirmed",
                Confidence = 0.9250m,
                AgentName = "ProblemUnderstandingAgent"
            };
            await context.ProblemAnalyses.AddAsync(analysis2);
            await context.SaveChangesAsync();
        }

        await using (var verifyContext = CreateDbContext())
        {
            var repo = new ProblemAnalysisRepository(verifyContext);

            var allAnalyses = await repo.GetByServiceRequestIdAsync(requestId);
            Assert.Equal(2, allAnalyses.Count);

            var latest = await repo.GetMostRecentByServiceRequestIdAsync(requestId);
            Assert.NotNull(latest);
            Assert.Equal("Updated analysis: motor capacitor failure confirmed", latest.DetectedProblem);
            Assert.Equal(0.9250m, latest.Confidence);
            Assert.Equal("ProblemUnderstandingAgent", latest.AgentName);

            // Also verify via ServiceRequest navigation property
            var requestRepo = new ServiceRequestRepository(verifyContext);
            var reqWithAnalyses = await requestRepo.GetByIdAsync(requestId, includeProblemAnalyses: true);
            Assert.NotNull(reqWithAnalyses);
            Assert.Equal(2, reqWithAnalyses.ProblemAnalyses.Count);
        }
    }

    [Fact]
    public async Task ProblemAnalysis_PreservesConfidencePrecision_InPostgreSqlNumericColumn()
    {
        var customer = await CreateUserAsync();
        var requestId = Guid.NewGuid();

        await using (var context = CreateDbContext())
        {
            var request = new ServiceRequest
            {
                Id = requestId,
                CustomerId = customer.Id,
                Category = "Electrical",
                Description = "Power outage in single circuit",
                LocationText = "Negombo",
                Urgency = ServiceRequestUrgency.High,
                Status = ServiceRequestStatus.Analyzed
            };
            await context.ServiceRequests.AddAsync(request);

            var analysis = new ProblemAnalysis
            {
                ServiceRequestId = requestId,
                DetectedProblem = "Tripped residual current device",
                Confidence = 0.8765m,
                AgentName = "ProblemUnderstandingAgent"
            };
            await context.ProblemAnalyses.AddAsync(analysis);
            await context.SaveChangesAsync();
        }

        await using (var verifyContext = CreateDbContext())
        {
            var analysis = await verifyContext.ProblemAnalyses
                .SingleAsync(a => a.ServiceRequestId == requestId);

            Assert.Equal(0.8765m, analysis.Confidence);
        }
    }
}
