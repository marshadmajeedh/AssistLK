using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.IntegrationTests.PostgreSql;

// =========================================================================================
// Class: ProblemAnalysisPersistencePostgreSqlTests
// What this class tests:
//   PostgreSQL persistence and repository queries for the ProblemAnalysis entity.
// Why these tests are needed:
//   Verifies that multiple analysis records per service request maintain a 1-to-N relationship,
//   repository methods accurately query the most recent analysis, and numeric confidence
//   precision is preserved in PostgreSQL columns without rounding.
// What is real vs mocked/faked:
//   - REAL: Live PostgreSQL instance, EF Core foreign key relationships, and ProblemAnalysisRepository.
//   - MOCKED: None.
// Prerequisites:
//   A running PostgreSQL database instance configured in ASSISTLK_TEST_POSTGRESQL_CONNECTION,
//   ConnectionStrings__TestConnection, or local User Secrets.
// Run from repository root:
//   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.PostgreSql.ProblemAnalysisPersistencePostgreSqlTests"
// =========================================================================================
public class ProblemAnalysisPersistencePostgreSqlTests : PostgreSqlIntegrationTestBase
{
    public ProblemAnalysisPersistencePostgreSqlTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    // Purpose: Verifies that multiple analysis records can be linked to a single ServiceRequest and that repository queries retrieve both the history and latest record.
    // Why: Clarification workflows produce multiple analysis records across rounds; each must be preserved for history and auditability.
    // Scenario: Inserts initial analysis (belt failure, 0.75 confidence) followed by updated analysis (capacitor failure, 0.925 confidence).
    // Pass condition: Repo returns 2 analyses for request, GetMostRecentByServiceRequestIdAsync returns the updated analysis, and navigation property contains both.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.ProblemAnalysisPersistencePostgreSqlTests.ProblemAnalysis_SupportsMultipleAnalysesPerRequest_AndMaintains1ToNRelation"
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

    // Purpose: Checks that PostgreSQL numeric columns preserve exact 4-decimal place precision for AI confidence values (e.g., 0.8765m).
    // Why: Floating-point inaccuracy can corrupt scoring and downstream provider matching filters.
    // Scenario: Persists an analysis with Confidence = 0.8765m and reads it back from PostgreSQL.
    // Pass condition: Loaded confidence equals exactly 0.8765m.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.ProblemAnalysisPersistencePostgreSqlTests.ProblemAnalysis_PreservesConfidencePrecision_InPostgreSqlNumericColumn"
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
