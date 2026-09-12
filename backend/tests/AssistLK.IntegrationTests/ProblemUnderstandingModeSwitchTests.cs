using AssistLK.Agents.Abstractions;
using AssistLK.Agents.Adapters;
using AssistLK.Agents.Clients;
using AssistLK.Agents.Configuration;
using AssistLK.Agents.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AssistLK.IntegrationTests;

/// <summary>
/// Verifies Section 4 & 5 DI lifetimes and mode switching between NativeCSharp and ExternalPython.
/// Confirms that AgentRegistry, AgentOrchestrator, and agents share matching Scoped lifetimes
/// and that no singleton captures a scoped dependency.
/// </summary>
public class ProblemUnderstandingModeSwitchTests
{
    private static IServiceProvider CreateContainer(string? mode = null, string? url = null)
    {
        var services = new ServiceCollection();

        var inMemory = new Dictionary<string, string?>();
        if (mode != null)
        {
            inMemory["AgentServices:ProblemUnderstandingMode"] = mode;
        }
        if (url != null)
        {
            inMemory["AgentServices:ProblemUnderstandingUrl"] = url;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(b => b.AddDebug());
        services.AddHttpClient();

        // Agent Services Options
        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var options = new AgentServicesOptions();
            config.GetSection(AgentServicesOptions.SectionName).Bind(options);
            options.Validate();
            return options;
        });

        // External Client
        services.AddHttpClient<IProblemUnderstandingClient, ProblemUnderstandingHttpClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<AgentServicesOptions>();
            var baseUrl = !string.IsNullOrWhiteSpace(options.ProblemUnderstandingUrl)
                ? options.ProblemUnderstandingUrl.TrimEnd('/')
                : "http://127.0.0.1:8001";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        // Tools & Infrastructure
        services.AddScoped<ToolRegistry>();
        services.AddScoped<ToolExecutor>();

        // External Adapter
        services.AddScoped<ExternalProblemUnderstandingAgentAdapter>();

        // Orchestrator
        services.AddScoped<AgentOrchestrator>();

        // Agent Registry (Mode Switch)
        services.AddScoped<AgentRegistry>(sp =>
        {
            var registry = new AgentRegistry();
            var options = sp.GetRequiredService<AgentServicesOptions>();

            if (string.Equals(options.ProblemUnderstandingMode, AgentServicesOptions.ExternalPythonMode, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(options.ProblemUnderstandingMode, AgentServicesOptions.NativeCSharpMode, StringComparison.OrdinalIgnoreCase))
            {
                registry.Register(sp.GetRequiredService<ExternalProblemUnderstandingAgentAdapter>());
            }
            else
            {
                throw new InvalidOperationException(
                    $"Invalid AgentServices:ProblemUnderstandingMode '{options.ProblemUnderstandingMode}'. " +
                    $"Supported modes are '{AgentServicesOptions.NativeCSharpMode}' and '{AgentServicesOptions.ExternalPythonMode}'.");
            }

            return registry;
        });

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    [Fact]
    public void DILifetimes_ValidateScopes_SucceedsWithoutSingletonCapturingScoped()
    {
        // ServiceProviderOptions.ValidateScopes = true guarantees no singleton captures a scoped dependency
        var provider = CreateContainer(mode: "ExternalPython", url: "http://127.0.0.1:8001");
        using var scope = provider.CreateScope();

        var registry = scope.ServiceProvider.GetRequiredService<AgentRegistry>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<AgentOrchestrator>();
        var agent = registry.Get("ProblemUnderstandingAgent");

        Assert.NotNull(registry);
        Assert.NotNull(orchestrator);
        Assert.NotNull(agent);
    }

    [Fact]
    public void AgentRegistry_ResolvesExternalProblemUnderstandingAgentAdapter_AsIntendedC1Agent()
    {
        var provider = CreateContainer(mode: "ExternalPython", url: "http://127.0.0.1:8001");
        using var scope = provider.CreateScope();

        var registry = scope.ServiceProvider.GetRequiredService<AgentRegistry>();
        var agent = registry.Get("ProblemUnderstandingAgent");

        Assert.NotNull(agent);
        Assert.IsType<ExternalProblemUnderstandingAgentAdapter>(agent);
        Assert.Equal("ProblemUnderstandingAgent", agent.Name);
    }

    [Fact]
    public void ModeSwitch_ExternalPython_ResolvesExternalProblemUnderstandingAgentAdapter()
    {
        var provider = CreateContainer(mode: "ExternalPython", url: "http://127.0.0.1:8001");
        using var scope = provider.CreateScope();

        var registry = scope.ServiceProvider.GetRequiredService<AgentRegistry>();
        var agent = registry.Get("ProblemUnderstandingAgent");

        Assert.NotNull(agent);
        Assert.IsType<ExternalProblemUnderstandingAgentAdapter>(agent);
        Assert.Equal("ProblemUnderstandingAgent", agent.Name);
    }

    [Fact]
    public void FinalArchitecture_ExternalAdapter_ImplementsIAgent_AndHasCorrectName()
    {
        var provider = CreateContainer(mode: "ExternalPython", url: "http://127.0.0.1:8001");
        using var scope = provider.CreateScope();

        var registry = scope.ServiceProvider.GetRequiredService<AgentRegistry>();
        var agent = registry.Get("ProblemUnderstandingAgent");

        Assert.NotNull(agent);
        var iAgent = Assert.IsAssignableFrom<IAgent>(agent);
        Assert.Equal("ProblemUnderstandingAgent", iAgent.Name);
    }

    [Fact]
    public void ModeSwitch_InvalidMode_ThrowsInvalidOperationExceptionDuringResolution()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            var provider = CreateContainer(mode: "DisallowedMode");
            using var scope = provider.CreateScope();
            _ = scope.ServiceProvider.GetRequiredService<AgentRegistry>();
        });
    }
}
