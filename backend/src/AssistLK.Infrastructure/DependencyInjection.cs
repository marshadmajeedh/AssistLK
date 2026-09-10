using AssistLK.Application.Interfaces;
using AssistLK.Infrastructure.Data;
using AssistLK.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddScoped<IAgentWorkflowDbContext>(
            provider => provider.GetRequiredService<AssistLKDbContext>()
        );
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IServiceRequestRepository, ServiceRequestRepository>();
        services.AddScoped<IProblemAnalysisRepository, ProblemAnalysisRepository>();
        return services;
    }
}