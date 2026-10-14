using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.IntegrationTests.PostgreSql;

// =========================================================================================
// Class: DatabaseConstraintPostgreSqlTests
// What this class tests:
//   PostgreSQL database check constraints, unique constraints, and foreign key enforcement
//   for ServiceRequest, ProblemAnalysis, and User entities.
// Why these tests are needed:
//   Proves that PostgreSQL itself rejects invalid data (latitude outside [-90, 90], longitude
//   outside [-180, 180], confidence outside [0, 1], invalid request statuses, duplicate emails,
//   and missing customer foreign keys) as a final line of defense against data corruption.
// What is real vs mocked/faked:
//   - REAL: Live PostgreSQL instance, database CHECK constraints (CK_ServiceRequests_Latitude_Range,
//     CK_ServiceRequests_Longitude_Range, CK_ProblemAnalyses_Confidence_Range, CK_ServiceRequests_Status_Valid),
//     UNIQUE indexes, and FOREIGN KEY constraints.
//   - MOCKED: None.
// Prerequisites:
//   A running PostgreSQL database instance configured via ASSISTLK_TEST_POSTGRESQL_CONNECTION,
//   ConnectionStrings__TestConnection, or local User Secrets.
// Run from repository root:
//   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.PostgreSql.DatabaseConstraintPostgreSqlTests"
// =========================================================================================
public class DatabaseConstraintPostgreSqlTests : PostgreSqlIntegrationTestBase
{
    public DatabaseConstraintPostgreSqlTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    // Purpose: Verifies that latitude values within [-90.0, 90.0] degrees (and null) satisfy the database check constraint and save successfully.
    // Why: Validates acceptable geographic latitude bounds in the database.
    // Scenario: Parameterized test inserting -90, -45.5, 0.0, 45.5, 90.0, and null.
    // Pass condition: All valid values save without throwing any database constraint violation.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.PostgreSql.DatabaseConstraintPostgreSqlTests.Latitude_ValidValuesWithinRangeOrNull_Succeeds"
    // Note: Parameterized theory test; this command executes all 6 inline data cases.
    [Theory]
    [InlineData(-90.0)]
    [InlineData(-45.5)]
    [InlineData(0.0)]
    [InlineData(45.5)]
    [InlineData(90.0)]
    [InlineData(null)]
    public async Task Latitude_ValidValuesWithinRangeOrNull_Succeeds(double? latitudeValue)
    {
        var customer = await CreateUserAsync();

        await using var context = CreateDbContext();
        var request = new ServiceRequest
        {
            CustomerId = customer.Id,
            Category = "Plumbing",
            Description = "Pipe issue",
            LocationText = "Colombo",
            Latitude = latitudeValue.HasValue ? (decimal)latitudeValue.Value : null,
            Longitude = 79.85m,
            Urgency = ServiceRequestUrgency.Medium,
            Status = ServiceRequestStatus.Created
        };

        await context.ServiceRequests.AddAsync(request);
        await context.SaveChangesAsync();

        Assert.NotEqual(Guid.Empty, request.Id);
    }

    // Purpose: Verifies that latitude values outside [-90.0, 90.0] violate PostgreSQL check constraint CK_ServiceRequests_Latitude_Range.
    // Why: Prevents impossible geographic coordinates from entering the database.
    // Scenario: Parameterized test inserting -90.00001, -100.0, 90.00001, 120.0.
    // Pass condition: SaveChangesAsync throws DbUpdateException containing a PostgreSQL check constraint violation.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.PostgreSql.DatabaseConstraintPostgreSqlTests.Latitude_InvalidValuesOutsideRange_ThrowsDbUpdateException"
    // Note: Parameterized theory test; this command executes all 4 inline data cases.
    [Theory]
    [InlineData(-90.00001)]
    [InlineData(-100.0)]
    [InlineData(90.00001)]
    [InlineData(120.0)]
    public async Task Latitude_InvalidValuesOutsideRange_ThrowsDbUpdateException(double invalidLatitude)
    {
        var customer = await CreateUserAsync();

        await using var context = CreateDbContext();
        var request = new ServiceRequest
        {
            CustomerId = customer.Id,
            Category = "Plumbing",
            Description = "Pipe issue",
            LocationText = "Colombo",
            Latitude = (decimal)invalidLatitude,
            Longitude = 79.85m,
            Urgency = ServiceRequestUrgency.Medium,
            Status = ServiceRequestStatus.Created
        };

        await context.ServiceRequests.AddAsync(request);
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.NotNull(ex.InnerException);
    }

    // Purpose: Verifies that longitude values within [-180.0, 180.0] degrees (and null) satisfy database check constraints.
    // Why: Validates acceptable geographic longitude bounds.
    // Scenario: Parameterized test inserting -180, -90.5, 0.0, 90.5, 180.0, and null.
    // Pass condition: All valid longitude values save without throwing any database constraint violation.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.PostgreSql.DatabaseConstraintPostgreSqlTests.Longitude_ValidValuesWithinRangeOrNull_Succeeds"
    // Note: Parameterized theory test; this command executes all 6 inline data cases.
    [Theory]
    [InlineData(-180.0)]
    [InlineData(-90.5)]
    [InlineData(0.0)]
    [InlineData(90.5)]
    [InlineData(180.0)]
    [InlineData(null)]
    public async Task Longitude_ValidValuesWithinRangeOrNull_Succeeds(double? longitudeValue)
    {
        var customer = await CreateUserAsync();

        await using var context = CreateDbContext();
        var request = new ServiceRequest
        {
            CustomerId = customer.Id,
            Category = "Plumbing",
            Description = "Pipe issue",
            LocationText = "Colombo",
            Latitude = 6.9m,
            Longitude = longitudeValue.HasValue ? (decimal)longitudeValue.Value : null,
            Urgency = ServiceRequestUrgency.Medium,
            Status = ServiceRequestStatus.Created
        };

        await context.ServiceRequests.AddAsync(request);
        await context.SaveChangesAsync();

        Assert.NotEqual(Guid.Empty, request.Id);
    }

    // Purpose: Verifies that longitude values outside [-180.0, 180.0] violate PostgreSQL check constraint CK_ServiceRequests_Longitude_Range.
    // Why: Prevents invalid geographic coordinates from being persisted.
    // Scenario: Parameterized test inserting -180.00001, -200.0, 180.00001, 250.0.
    // Pass condition: Throws DbUpdateException containing a PostgreSQL check constraint violation.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.PostgreSql.DatabaseConstraintPostgreSqlTests.Longitude_InvalidValuesOutsideRange_ThrowsDbUpdateException"
    // Note: Parameterized theory test; this command executes all 4 inline data cases.
    [Theory]
    [InlineData(-180.00001)]
    [InlineData(-200.0)]
    [InlineData(180.00001)]
    [InlineData(250.0)]
    public async Task Longitude_InvalidValuesOutsideRange_ThrowsDbUpdateException(double invalidLongitude)
    {
        var customer = await CreateUserAsync();

        await using var context = CreateDbContext();
        var request = new ServiceRequest
        {
            CustomerId = customer.Id,
            Category = "Plumbing",
            Description = "Pipe issue",
            LocationText = "Colombo",
            Latitude = 6.9m,
            Longitude = (decimal)invalidLongitude,
            Urgency = ServiceRequestUrgency.Medium,
            Status = ServiceRequestStatus.Created
        };

        await context.ServiceRequests.AddAsync(request);
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.NotNull(ex.InnerException);
    }

    // Purpose: Verifies that confidence values between 0.0 and 1.0 (inclusive) pass the PostgreSQL check constraint on ProblemAnalysis.
    // Why: Confidence must be a valid probability score.
    // Scenario: Parameterized test inserting 0.0, 0.5, and 1.0.
    // Pass condition: ProblemAnalysis entity saves successfully with a generated ID.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.PostgreSql.DatabaseConstraintPostgreSqlTests.Confidence_ValidValuesBetweenZeroAndOne_Succeeds"
    // Note: Parameterized theory test; this command executes all 3 inline data cases.
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public async Task Confidence_ValidValuesBetweenZeroAndOne_Succeeds(double confidence)
    {
        var customer = await CreateUserAsync();

        await using var context = CreateDbContext();
        var request = new ServiceRequest
        {
            CustomerId = customer.Id,
            Category = "Appliance Repair",
            Description = "Fridge warming",
            LocationText = "Colombo",
            Urgency = ServiceRequestUrgency.Medium,
            Status = ServiceRequestStatus.Analyzed
        };
        await context.ServiceRequests.AddAsync(request);
        await context.SaveChangesAsync();

        var analysis = new ProblemAnalysis
        {
            ServiceRequestId = request.Id,
            DetectedProblem = "Compressor issue",
            Confidence = (decimal)confidence,
            AgentName = "ProblemUnderstandingAgent"
        };

        await context.ProblemAnalyses.AddAsync(analysis);
        await context.SaveChangesAsync();

        Assert.NotEqual(Guid.Empty, analysis.Id);
    }

    // Purpose: Verifies that confidence values outside [0.0, 1.0] violate PostgreSQL check constraint CK_ProblemAnalyses_Confidence_Range.
    // Why: Guarantees corrupted or out-of-range AI confidence numbers cannot be stored.
    // Scenario: Parameterized test inserting -0.0001, -1.0, 1.0001, and 2.5.
    // Pass condition: Throws DbUpdateException containing inner PostgreSQL check constraint exception.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.PostgreSql.DatabaseConstraintPostgreSqlTests.Confidence_InvalidValuesOutsideZeroAndOne_ThrowsDbUpdateException"
    // Note: Parameterized theory test; this command executes all 4 inline data cases.
    [Theory]
    [InlineData(-0.0001)]
    [InlineData(-1.0)]
    [InlineData(1.0001)]
    [InlineData(2.5)]
    public async Task Confidence_InvalidValuesOutsideZeroAndOne_ThrowsDbUpdateException(double invalidConfidence)
    {
        var customer = await CreateUserAsync();

        await using var context = CreateDbContext();
        var request = new ServiceRequest
        {
            CustomerId = customer.Id,
            Category = "Appliance Repair",
            Description = "Fridge warming",
            LocationText = "Colombo",
            Urgency = ServiceRequestUrgency.Medium,
            Status = ServiceRequestStatus.Analyzed
        };
        await context.ServiceRequests.AddAsync(request);
        await context.SaveChangesAsync();

        var analysis = new ProblemAnalysis
        {
            ServiceRequestId = request.Id,
            DetectedProblem = "Compressor issue",
            Confidence = (decimal)invalidConfidence,
            AgentName = "ProblemUnderstandingAgent"
        };

        await context.ProblemAnalyses.AddAsync(analysis);
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.NotNull(ex.InnerException);
    }

    // Purpose: Verifies that attempting to insert a ServiceRequest with a non-existent CustomerId violates foreign key constraint FK_ServiceRequests_Users_CustomerId.
    // Why: Relational integrity prevents orphan requests without a valid owning customer.
    // Scenario: Inserts a service request with a random CustomerId Guid that does not exist in Users.
    // Pass condition: SaveChangesAsync throws DbUpdateException.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.DatabaseConstraintPostgreSqlTests.ServiceRequest_ForeignKeyToUser_FailsWhenUserDoesNotExist"
    [Fact]
    public async Task ServiceRequest_ForeignKeyToUser_FailsWhenUserDoesNotExist()
    {
        var nonExistentCustomerId = Guid.NewGuid();

        await using var context = CreateDbContext();
        var request = new ServiceRequest
        {
            CustomerId = nonExistentCustomerId,
            Category = "Plumbing",
            Description = "Water leaking",
            LocationText = "Colombo",
            Urgency = ServiceRequestUrgency.Medium,
            Status = ServiceRequestStatus.Created
        };

        await context.ServiceRequests.AddAsync(request);
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.NotNull(ex.InnerException);
    }

    // Purpose: Verifies that inserting two users with the same email violates PostgreSQL unique index IX_Users_Email.
    // Why: Enforces unique email credentials per user account across the system.
    // Scenario: Creates a user, then attempts to insert a second user with the identical email address.
    // Pass condition: Throws DbUpdateException from PostgreSQL unique constraint violation.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.PostgreSql.DatabaseConstraintPostgreSqlTests.User_UniqueEmailConstraint_ThrowsDbUpdateExceptionOnDuplicate"
    [Fact]
    public async Task User_UniqueEmailConstraint_ThrowsDbUpdateExceptionOnDuplicate()
    {
        var email = "duplicate@assistlk.com";
        await CreateUserAsync(email: email);

        await using var context = CreateDbContext();
        var duplicateUser = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Second User",
            Email = email,
            PasswordHash = "dummy_hash",
            Role = UserRole.Customer,
            PhoneNumber = "0779998888",
            IsActive = true
        };

        await context.Users.AddAsync(duplicateUser);
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.NotNull(ex.InnerException);
    }

    // Purpose: Verifies that all 6 valid ServiceRequestStatus values (Created, Analyzing, AwaitingInformation, Analyzed, ReadyForMatching, Cancelled) satisfy check constraint CK_ServiceRequests_Status_Valid.
    // Why: Validates that all defined lifecycle statuses for Component 1 are allowed by the database.
    // Scenario: Parameterized test inserting a request for each of the 6 enum values.
    // Pass condition: Each status persists successfully and reads back with matching status value.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.PostgreSql.DatabaseConstraintPostgreSqlTests.ServiceRequest_Status_ValidEnumStatuses_PersistSuccessfully"
    // Note: Parameterized theory test; this command executes all 6 inline data cases.
    [Theory]
    [InlineData(ServiceRequestStatus.Created)]
    [InlineData(ServiceRequestStatus.Analyzing)]
    [InlineData(ServiceRequestStatus.AwaitingInformation)]
    [InlineData(ServiceRequestStatus.Analyzed)]
    [InlineData(ServiceRequestStatus.ReadyForMatching)]
    [InlineData(ServiceRequestStatus.Cancelled)]
    public async Task ServiceRequest_Status_ValidEnumStatuses_PersistSuccessfully(ServiceRequestStatus status)
    {
        var customer = await CreateUserAsync();

        await using var context = CreateDbContext();
        var request = new ServiceRequest
        {
            CustomerId = customer.Id,
            Category = "Plumbing",
            Description = $"Status test for {status}",
            LocationText = "Colombo",
            Urgency = ServiceRequestUrgency.Medium,
            Status = status
        };

        await context.ServiceRequests.AddAsync(request);
        await context.SaveChangesAsync();

        var retrieved = await context.ServiceRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == request.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(status, retrieved.Status);
    }

    // Purpose: Verifies that legacy or downstream statuses ("Assigned", "Completed", "Booked", "InProgress") violate PostgreSQL check constraint CK_ServiceRequests_Status_Valid.
    // Why: Architectural decoupling guarantee: ServiceRequest status is owned strictly by Component 1 lifecycle; downstream provider matching (C2) and booking (C3) maintain their own separate entity statuses and must never set these on ServiceRequest.
    // Scenario: Executes raw SQL INSERT attempting to set status to "Assigned", "Completed", "Booked", or "InProgress".
    // Pass condition: Throws exception with SQLState "23514" (check constraint violation) or matching constraint name CK_ServiceRequests_Status_Valid.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.PostgreSql.DatabaseConstraintPostgreSqlTests.ServiceRequest_Status_InvalidStatus_ThrowsCheckConstraintViolation"
    // Note: Parameterized theory test; this command executes all 4 inline data cases.
    [Theory]
    [InlineData("Assigned")]
    [InlineData("Completed")]
    [InlineData("Booked")]
    [InlineData("InProgress")]
    public async Task ServiceRequest_Status_InvalidStatus_ThrowsCheckConstraintViolation(string invalidStatus)
    {
        var customer = await CreateUserAsync();
        await using var context = CreateDbContext();

        var requestId = Guid.NewGuid();
        var sql = $@"
            INSERT INTO ""ServiceRequests"" (
                ""Id"", ""CustomerId"", ""Category"", ""Description"", ""LocationText"", ""Urgency"", ""Status"", ""CreatedAt"", ""UpdatedAt"", ""EvidenceRevision""
            ) VALUES (
                '{requestId}', '{customer.Id}', 'Plumbing', 'Invalid status test', 'Colombo', 'Medium', '{invalidStatus}', NOW(), NOW(), 1
            )";

        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            context.Database.ExecuteSqlRawAsync(sql));

        var isConstraintViolation = ex.ToString().Contains("CK_ServiceRequests_Status_Valid") ||
            (ex is Npgsql.PostgresException pgEx && pgEx.SqlState == "23514");

        Assert.True(isConstraintViolation, $"Expected check constraint violation for status '{invalidStatus}', but got: {ex.Message}");
    }
}
