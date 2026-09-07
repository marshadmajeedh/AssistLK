using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace AssistLK.Api.Tests;

/// <summary>
/// WebApplicationFactory fixture for Phase 7E API, authorization, routing, and response-contract tests.
/// Uses EF Core InMemory provider and exercises the real JWT Bearer authentication pipeline.
///
/// Note: These tests verify HTTP API routing, authorization policies, validation gates, and serialization contracts.
/// They do NOT verify PostgreSQL database constraints, triggers, or PostgreSQL-specific persistence behaviour,
/// which are verified in dedicated PostgreSQL suites (PostgreSqlApiTestFactory and AssistLK.IntegrationTests).
/// </summary>
public class AssistLKApiTestFactory : WebApplicationFactory<Program>
{
    public const string TestJwtKey = "TestSecretSigningKeyForJwtBearerAuthentication1234567890!";
    public const string TestIssuer = "AssistLK.Api";
    public const string TestAudience = "AssistLK.Clients";

    static AssistLKApiTestFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Host=localhost;Database=test;Username=postgres;Password=postgres");
        Environment.SetEnvironmentVariable("Jwt__Key", TestJwtKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", TestIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", TestAudience);
        Environment.SetEnvironmentVariable("Jwt__ExpirationMinutes", "60");
    }

    private readonly string _databaseName = Guid.NewGuid().ToString();


    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=postgres;Password=postgres",
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
                options.UseInMemoryDatabase(_databaseName);
            });
        });
    }

    public string GenerateJwtToken(
        Guid userId,
        UserRole role,
        string fullName = "Test User",
        string email = "test@assistlk.com")
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

    public async Task SeedAsync(Func<AssistLKDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AssistLKDbContext>();
        await action(context);
        await context.SaveChangesAsync();
    }
}
