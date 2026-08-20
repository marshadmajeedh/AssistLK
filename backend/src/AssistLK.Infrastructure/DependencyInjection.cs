using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AssistLK.Application.Interfaces;
using AssistLK.Infrastructure.Repositories;

namespace AssistLK.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,

        string connectionString)
    {
        services.AddDbContext<AssistLKDbContext>(
            options =>
                options.UseNpgsql(connectionString)
        );
        services.AddScoped<IUserRepository, UserRepository>();
        return services;
    }
}