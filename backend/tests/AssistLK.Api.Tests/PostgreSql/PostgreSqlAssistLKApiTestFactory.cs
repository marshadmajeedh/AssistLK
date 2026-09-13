using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using AssistLK.Agents.Clients;
using AssistLK.Api.Tests.TestDoubles;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace AssistLK.Api.Tests.PostgreSql;

public class PostgreSqlAssistLKApiTestFactory : WebApplicationFactory<Program>
{
    public const string TestJwtKey = "TestSecretSigningKeyForJwtBearerAuthentication1234567890!";
    public const string TestIssuer = "AssistLK.Api";
    public const string TestAudience = "AssistLK.Clients";

    public FakeProblemUnderstandingClient FakeAgentClient { get; } = new();

    private readonly string _connectionString;

    public PostgreSqlAssistLKApiTestFactory()
    {
        _connectionString = PostgreSqlApiTestDatabase.GetConnectionString();
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _connectionString);
        Environment.SetEnvironmentVariable("Jwt__Key", TestJwtKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", TestIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", TestAudience);
        Environment.SetEnvironmentVariable("Jwt__ExpirationMinutes", "60");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["Jwt:Key"] = TestJwtKey,
                ["Jwt:Issuer"] = TestIssuer,
                ["Jwt:Audience"] = TestAudience,
                ["Jwt:ExpirationMinutes"] = "60"
            });
        });

        builder.ConfigureServices(services =>
        {
            var dbContextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AssistLKDbContext>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            var contextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(AssistLKDbContext));
            if (contextDescriptor != null)
            {
                services.Remove(contextDescriptor);
            }

            services.AddDbContext<AssistLKDbContext>(options =>
            {
                options.UseNpgsql(_connectionString);
            });

            // Decouple from remote Python agent by providing local deterministic fake
            var clientDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IProblemUnderstandingClient));
            if (clientDescriptor != null)
            {
                services.Remove(clientDescriptor);
            }

            services.AddSingleton<IProblemUnderstandingClient>(FakeAgentClient);
            services.AddSingleton<AssistLK.Application.Attachments.IServiceRequestAttachmentStorage,
                AssistLK.Tests.Shared.FakeAttachmentStorage>();
        });
    }

    public async Task InitializeAsync()
    {
        await PostgreSqlApiTestDatabase.InitializeDatabaseAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AssistLKDbContext>();
        await PostgreSqlApiTestDatabase.ResetDataAsync(context);
    }

    public string GenerateJwtToken(
        Guid userId,
        UserRole role,
        string fullName = "Test Customer",
        string email = "customer@assistlk.com")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, fullName),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public HttpClient CreateAuthenticatedClient(Guid userId, UserRole role)
    {
        var client = CreateClient();
        var token = GenerateJwtToken(userId, role);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task<User> SeedUserAsync(
        Guid userId,
        UserRole role = UserRole.Customer,
        string email = "test@assistlk.com",
        string fullName = "Test User")
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AssistLKDbContext>();

        var user = new User
        {
            Id = userId,
            FullName = fullName,
            Email = email,
            PasswordHash = "hashed_pw",
            Role = role,
            PhoneNumber = "0771234567",
            IsActive = true
        };

        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();
        return user;
    }

    public AssistLKDbContext CreateDbContext()
    {
        return PostgreSqlApiTestDatabase.CreateDbContext();
    }
}
