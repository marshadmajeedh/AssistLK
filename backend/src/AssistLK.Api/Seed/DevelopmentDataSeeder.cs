using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.Api.Seed;

public static class DevelopmentDataSeeder
{
    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        // Never automatically seed development credentials
        // outside the Development environment.
        if (!environment.IsDevelopment())
        {
            return;
        }

        var email = configuration["AdminSeed:Email"];
        var password = configuration["AdminSeed:Password"];
        var fullName =
            configuration["AdminSeed:FullName"]
            ?? "AssistLK Administrator";

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        email = email.Trim().ToLowerInvariant();

        using var scope = services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<AssistLKDbContext>();

        var passwordHasher =
            scope.ServiceProvider
                .GetRequiredService<
                    IPasswordHasher<User>>();

        var adminExists =
            await dbContext.Users
                .AnyAsync(user =>
                    user.Email == email);

        if (adminExists)
        {
            return;
        }

        var admin = new User
        {
            FullName = fullName.Trim(),
            Email = email,
            Role = UserRole.Admin,
            IsActive = true
        };

        admin.PasswordHash =
            passwordHasher.HashPassword(
                admin,
                password);

        await dbContext.Users.AddAsync(admin);

        await dbContext.SaveChangesAsync();
    }
}
