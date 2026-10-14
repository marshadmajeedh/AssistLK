using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AssistLK.IntegrationTests.PostgreSql;

// =========================================================================================
// Class: ServiceRequestPersistencePostgreSqlTests
// What this class tests:
//   PostgreSQL persistence and repository queries for the ServiceRequest domain entity.
// Why these tests are needed:
//   Verifies that ServiceRequest entities round-trip cleanly in a real PostgreSQL database,
//   enums (Urgency, Status) persist as varchar strings rather than integer ordinals,
//   and ServiceRequestRepository filtering operates correctly.
// What is real vs mocked/faked:
//   - REAL: Live PostgreSQL database, Npgsql connection, EF Core migrations, and ServiceRequestRepository.
//   - MOCKED: None.
// Prerequisites:
//   A running PostgreSQL instance with connection string configured in
//   ASSISTLK_TEST_POSTGRESQL_CONNECTION, ConnectionStrings__TestConnection, or local User Secrets.
// Run from repository root:
//   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.PostgreSql.ServiceRequestPersistencePostgreSqlTests"
// =========================================================================================
public class ServiceRequestPersistencePostgreSqlTests : PostgreSqlIntegrationTestBase
{
    public ServiceRequestPersistencePostgreSqlTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    // Purpose: Verifies that creating and saving a ServiceRequest in PostgreSQL preserves all properties and timestamps accurately.
    // Why: Ensures database column types (UUIDs, decimals, text, timestamps) match domain entity properties without data loss or truncation.
    // Scenario: Inserts a plumbing request with full GPS and high urgency, then loads it in a fresh DbContext.
    // Pass condition: Loaded entity matches all inserted properties and auto-generates CreatedAt and UpdatedAt timestamps.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.ServiceRequestPersistencePostgreSqlTests.ServiceRequest_FullRoundtrip_SavesAndLoadsAllPropertiesAccurately"
    [Fact]
    public async Task ServiceRequest_FullRoundtrip_SavesAndLoadsAllPropertiesAccurately()
    {
        var customer = await CreateUserAsync();

        var requestId = Guid.NewGuid();
        var category = "Plumbing";
        var description = "Leaking bathroom pipe under sink";
        var locationText = "Colombo 03";
        var latitude = 6.903456m;
        var longitude = 79.854321m;
        var urgency = ServiceRequestUrgency.High;
        var status = ServiceRequestStatus.Created;

        await using (var context = CreateDbContext())
        {
            var request = new ServiceRequest
            {
                Id = requestId,
                CustomerId = customer.Id,
                Category = category,
                Description = description,
                LocationText = locationText,
                Latitude = latitude,
                Longitude = longitude,
                Urgency = urgency,
                Status = status
            };

            await context.ServiceRequests.AddAsync(request);
            await context.SaveChangesAsync();
        }

        await using (var verifyContext = CreateDbContext())
        {
            var saved = await verifyContext.ServiceRequests.FindAsync(requestId);
            Assert.NotNull(saved);
            Assert.Equal(requestId, saved.Id);
            Assert.Equal(customer.Id, saved.CustomerId);
            Assert.Equal(category, saved.Category);
            Assert.Equal(description, saved.Description);
            Assert.Equal(locationText, saved.LocationText);
            Assert.Equal(latitude, saved.Latitude);
            Assert.Equal(longitude, saved.Longitude);
            Assert.Equal(urgency, saved.Urgency);
            Assert.Equal(status, saved.Status);
            Assert.True(saved.CreatedAt > DateTime.MinValue);
            Assert.True(saved.UpdatedAt > DateTime.MinValue);
        }
    }

    // Purpose: Verifies via raw SQL that enum values (Urgency and Status) are stored in PostgreSQL columns as human-readable varchar strings.
    // Why: Storing enums as strings prevents data corruption when enum values are reordered or added in future releases.
    // Scenario: Inserts a request with Urgency = Unknown and Status = ReadyForMatching, then reads the columns using a raw NpgsqlCommand.
    // Pass condition: Raw SQL reader returns "Unknown" and "ReadyForMatching" strings.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.ServiceRequestPersistencePostgreSqlTests.ServiceRequest_StoresEnumsAsVarcharStrings_InPostgreSqlColumns"
    [Fact]
    public async Task ServiceRequest_StoresEnumsAsVarcharStrings_InPostgreSqlColumns()
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
                Description = "Tripping circuit breaker",
                LocationText = "Kandy City",
                Urgency = ServiceRequestUrgency.Unknown,
                Status = ServiceRequestStatus.ReadyForMatching
            };

            await context.ServiceRequests.AddAsync(request);
            await context.SaveChangesAsync();

            // Direct raw SQL query against PostgreSQL to inspect stored string values
            var conn = (NpgsqlConnection)context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            await using var cmd = new NpgsqlCommand(
                "SELECT \"Urgency\", \"Status\" FROM \"ServiceRequests\" WHERE \"Id\" = @id", conn);
            cmd.Parameters.AddWithValue("id", requestId);

            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());

            var rawUrgency = reader.GetString(0);
            var rawStatus = reader.GetString(1);

            Assert.Equal("Unknown", rawUrgency);
            Assert.Equal("ReadyForMatching", rawStatus);
        }
    }

    // Purpose: Verifies that ServiceRequestRepository.GetByCustomerIdAsync and GetByStatusAsync return accurately filtered collections from PostgreSQL.
    // Why: Service request listings and matching queries rely heavily on customer ID and status filters.
    // Scenario: Inserts 3 requests across two customers and different statuses, then queries using the repository.
    // Pass condition: Customer 1 query returns 2 requests; status queries return exactly 2 Created requests and 1 Analyzed request.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.ServiceRequestPersistencePostgreSqlTests.ServiceRequestRepository_GetByCustomerIdAndStatus_ReturnsFilteredResults"
    [Fact]
    public async Task ServiceRequestRepository_GetByCustomerIdAndStatus_ReturnsFilteredResults()
    {
        var customer1 = await CreateUserAsync(email: "c1@test.com");
        var customer2 = await CreateUserAsync(email: "c2@test.com");

        await using (var context = CreateDbContext())
        {
            var repo = new ServiceRequestRepository(context);

            await repo.AddAsync(new ServiceRequest
            {
                CustomerId = customer1.Id,
                Category = "Plumbing",
                Description = "Pipe issue 1",
                LocationText = "Location 1",
                Urgency = ServiceRequestUrgency.Medium,
                Status = ServiceRequestStatus.Created
            });

            await repo.AddAsync(new ServiceRequest
            {
                CustomerId = customer1.Id,
                Category = "Electrical",
                Description = "Wiring issue 2",
                LocationText = "Location 2",
                Urgency = ServiceRequestUrgency.High,
                Status = ServiceRequestStatus.Analyzed
            });

            await repo.AddAsync(new ServiceRequest
            {
                CustomerId = customer2.Id,
                Category = "Appliance Repair",
                Description = "Fridge repair",
                LocationText = "Location 3",
                Urgency = ServiceRequestUrgency.Low,
                Status = ServiceRequestStatus.Created
            });

            await repo.SaveChangesAsync();
        }

        await using (var verifyContext = CreateDbContext())
        {
            var repo = new ServiceRequestRepository(verifyContext);

            var customer1Requests = await repo.GetByCustomerIdAsync(customer1.Id);
            Assert.Equal(2, customer1Requests.Count);

            var createdRequests = await repo.GetByStatusAsync(ServiceRequestStatus.Created);
            Assert.Equal(2, createdRequests.Count);

            var analyzedRequests = await repo.GetByStatusAsync(ServiceRequestStatus.Analyzed);
            Assert.Single(analyzedRequests);
            Assert.Equal("Electrical", analyzedRequests[0].Category);
        }
    }
}
