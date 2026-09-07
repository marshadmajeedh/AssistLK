using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Repositories;

namespace AssistLK.IntegrationTests.PostgreSql;

public class OwnershipPostgreSqlTests : PostgreSqlIntegrationTestBase
{
    public OwnershipPostgreSqlTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

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
