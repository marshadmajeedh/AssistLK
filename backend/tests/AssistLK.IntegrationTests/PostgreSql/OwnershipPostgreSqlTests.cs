using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Repositories;

namespace AssistLK.IntegrationTests.PostgreSql;

// =========================================================================================
// Class: OwnershipPostgreSqlTests
// What this class tests:
//   Multi-tenant customer data isolation at the PostgreSQL repository layer.
// Why these tests are needed:
//   Prevents horizontal privilege escalation and data leakage by ensuring queries scoped
//   to Customer B can never return service requests belonging to Customer A.
// What is real vs mocked/faked:
//   - REAL: Live PostgreSQL database, EF Core LINQ queries, and ServiceRequestRepository.
//   - MOCKED: None.
// Prerequisites:
//   A running PostgreSQL database instance configured via ASSISTLK_TEST_POSTGRESQL_CONNECTION,
//   ConnectionStrings__TestConnection, or local User Secrets.
// Run from repository root:
//   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.PostgreSql.OwnershipPostgreSqlTests"
// =========================================================================================
public class OwnershipPostgreSqlTests : PostgreSqlIntegrationTestBase
{
    public OwnershipPostgreSqlTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    // Purpose: Checks that another customer cannot read or list a service request owned by a different customer.
    // Why: Protects sensitive customer location and service problem data from horizontal data breaches.
    // Scenario: Customer A creates a service request; Customer B attempts to retrieve it by ID and queries their own request list.
    // Pass condition: GetByIdAndCustomerIdAsync returns the record for Customer A, returns null for Customer B, and Customer B's request list is empty.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.OwnershipPostgreSqlTests.CustomerIsolation_CustomerBCannotRetrieveCustomerARequest"
    [Fact]
    public async Task CustomerIsolation_CustomerBCannotRetrieveCustomerARequest()
    {
        var customerA = await CreateUserAsync(email: "customerA@assistlk.com", fullName: "Customer A");
        var customerB = await CreateUserAsync(email: "customerB@assistlk.com", fullName: "Customer B");

        var requestId = Guid.NewGuid();

        await using (var context = CreateDbContext())
        {
            var repo = new ServiceRequestRepository(context);
            var request = new ServiceRequest
            {
                Id = requestId,
                CustomerId = customerA.Id,
                Category = "Plumbing",
                Description = "Pipe burst in basement",
                LocationText = "Colombo 07",
                Urgency = ServiceRequestUrgency.High,
                Status = ServiceRequestStatus.Created
            };

            await repo.AddAsync(request);
            await repo.SaveChangesAsync();
        }

        await using (var queryContext = CreateDbContext())
        {
            var repo = new ServiceRequestRepository(queryContext);

            // Customer A can retrieve their own request
            var foundForA = await repo.GetByIdAndCustomerIdAsync(requestId, customerA.Id);
            Assert.NotNull(foundForA);
            Assert.Equal(requestId, foundForA.Id);

            // Customer B query for Customer A's request returns null
            var foundForB = await repo.GetByIdAndCustomerIdAsync(requestId, customerB.Id);
            Assert.Null(foundForB);

            // Customer B's list does not contain Customer A's request
            var customerBRequests = await repo.GetByCustomerIdAsync(customerB.Id);
            Assert.Empty(customerBRequests);
        }
    }
}
