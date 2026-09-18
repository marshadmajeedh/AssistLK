using AssistLK.Agents.Abstractions;
using AssistLK.Agents.Core;
using AssistLK.Agents.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace AssistLK.Agents;

public static class DependencyInjection
{
    public static IServiceCollection AddAgentServices(this IServiceCollection services)
    {
        services.AddScoped<DemoProviderSearchTool>();
        services.AddScoped<IAssistantAgent, AssistantAgent>();

        return services;
    }
}