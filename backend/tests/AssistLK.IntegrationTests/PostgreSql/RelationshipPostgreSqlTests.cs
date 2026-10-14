using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.IntegrationTests.PostgreSql;

// =========================================================================================
// Class: RelationshipPostgreSqlTests
// What this class tests:
//   Foreign key relationship behaviors (Restrict on User delete, Cascade on ServiceRequest delete,
//   and Eager Loading) in PostgreSQL.
// Why these tests are needed:
//   Validates relational database rules: deleting a customer with existing requests is blocked
//   (ON DELETE RESTRICT) to prevent orphaned data, deleting a service request cascades
//   (ON DELETE CASCADE) to clean up its problem analyses, and EF Core navigation joins operate properly.
// What is real vs mocked/faked:
//   - REAL: Live PostgreSQL instance, foreign key constraints, and EF Core SQL JOIN queries.
//   - MOCKED: None.
// Prerequisites:
//   A running PostgreSQL database instance configured via ASSISTLK_TEST_POSTGRESQL_CONNECTION,
//   ConnectionStrings__TestConnection, or local User Secrets.
// Run from repository root:
//   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.PostgreSql.RelationshipPostgreSqlTests"
// =========================================================================================
public class RelationshipPostgreSqlTests : PostgreSqlIntegrationTestBase
{
    public RelationshipPostgreSqlTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    // Purpose: Verifies that attempting to delete a User who owns existing ServiceRequest records is blocked by PostgreSQL ON DELETE RESTRICT.
    // Why: Prevents accidental customer deletion from corrupting or breaking foreign keys of active or historical service requests.
    // Scenario: Creates a user and service request, then attempts to remove the user from the database.
    // Pass condition: SaveChangesAsync throws DbUpdateException with an inner PostgreSQL foreign key violation.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.RelationshipPostgreSqlTests.UserDelete_WithExistingServiceRequest_ThrowsDbUpdateExceptionDueToRestrict"
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

    // Purpose: Verifies that deleting a ServiceRequest cascades in PostgreSQL to automatically delete its linked ProblemAnalysis rows.
    // Why: Ensures child analysis records do not become orphaned when their parent service request is deleted.
    // Scenario: Inserts a request and problem analysis, deletes the request, and queries for the analysis in a fresh context.
    // Pass condition: verifyContext.ProblemAnalyses.FindAsync(analysisId) returns null.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.RelationshipPostgreSqlTests.ServiceRequestDelete_CascadesAndDeletesAssociatedProblemAnalyses"
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

    // Purpose: Verifies that Include(r => r.Customer) and Include(r => r.ProblemAnalyses) eagerly load related entities via SQL JOINs.
    // Why: Confirms that navigation property mappings generate valid PostgreSQL JOIN queries and populate domain entity graphs.
    // Scenario: Queries a service request with eager loading for Customer and ProblemAnalyses navigation properties.
    // Pass condition: loaded.Customer is not null with correct customer full name, and loaded.ProblemAnalyses contains the linked problem analysis.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.RelationshipPostgreSqlTests.ServiceRequest_CanEagerlyLoadCustomerAndProblemAnalyses"
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
