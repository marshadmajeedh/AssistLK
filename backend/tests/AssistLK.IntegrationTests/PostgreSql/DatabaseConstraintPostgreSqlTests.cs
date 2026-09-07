using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.IntegrationTests.PostgreSql;

public class DatabaseConstraintPostgreSqlTests : PostgreSqlIntegrationTestBase
{
    public DatabaseConstraintPostgreSqlTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

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
}
