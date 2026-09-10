using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;

namespace AssistLK.IntegrationTests.PostgreSql;

[Collection("PostgreSqlDatabase")]
public abstract class PostgreSqlIntegrationTestBase : IAsyncLifetime
{
    protected readonly PostgreSqlTestFixture Fixture;

    protected PostgreSqlIntegrationTestBase(PostgreSqlTestFixture fixture)
    {
        Fixture = fixture;
    }

    public virtual async Task InitializeAsync()
    {
        Fixture.EnsureAvailable();
        await using var context = Fixture.CreateDbContext();
        await Fixture.ResetDataAsync(context);
    }

    public virtual Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    protected AssistLKDbContext CreateDbContext()
    {
        return Fixture.CreateDbContext();
    }

    protected async Task<User> CreateUserAsync(
        Guid? id = null,
        UserRole role = UserRole.Customer,
        string email = "user@assistlk.com",
        string fullName = "Test User")
    {
        await using var context = CreateDbContext();
        var user = new User
        {
            Id = id ?? Guid.NewGuid(),
            FullName = fullName,
            Email = email,
            PasswordHash = "dummy_hash",
            Role = role,
            PhoneNumber = "0771234567",
            IsActive = true
        };

        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();
        return user;
    }
}
