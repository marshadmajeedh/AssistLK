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
/// Verifies DI lifetimes and agent registration for Component 1's production architecture:
/// ASP.NET Core -> ExternalProblemUnderstandingAgentAdapter -> ProblemUnderstandingHttpClient -> Python FastAPI.
/// Confirms that AgentRegistry, AgentOrchestrator, and ExternalProblemUnderstandingAgentAdapter share matching Scoped lifetimes
/// and that no singleton captures a scoped dependency.
/// </summary>
public class ProblemUnderstandingRegistrationTests
{
    private static IServiceProvider CreateContainer(string? url = null, int? timeoutSeconds = null)
    {
        var services = new ServiceCollection();

        var inMemory = new Dictionary<string, string?>();
        if (url != null)
        {
            inMemory["AgentServices:ProblemUnderstandingUrl"] = url;
        }
        if (timeoutSeconds != null)
        {
            inMemory["AgentServices:TimeoutSeconds"] = timeoutSeconds.Value.ToString();
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

        // Agent Registry
        services.AddScoped<AgentRegistry>(sp =>
        {
            var registry = new AgentRegistry();
            registry.Register(sp.GetRequiredService<ExternalProblemUnderstandingAgentAdapter>());
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
        var provider = CreateContainer(url: "http://127.0.0.1:8001");
        using var scope = provider.CreateScope();

        var registry = scope.ServiceProvider.GetRequiredService<AgentRegistry>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<AgentOrchestrator>();
        var agent = registry.Get("ProblemUnderstandingAgent");

        Assert.NotNull(registry);
        Assert.NotNull(orchestrator);
        Assert.NotNull(agent);
    }

    [Fact]
    public void AgentRegistry_ResolvesExternalProblemUnderstandingAgentAdapter_AsProblemUnderstandingAgent()
    {
        var provider = CreateContainer(url: "http://127.0.0.1:8001");
        using var scope = provider.CreateScope();

        var registry = scope.ServiceProvider.GetRequiredService<AgentRegistry>();
        var agent = registry.Get("ProblemUnderstandingAgent");

        Assert.NotNull(agent);
        Assert.IsType<ExternalProblemUnderstandingAgentAdapter>(agent);
        Assert.Equal("ProblemUnderstandingAgent", agent.Name);
    }

    [Fact]
    public void ExternalProblemUnderstandingAgentAdapter_ImplementsIAgent_AndExposesLogicalName()
    {
        var provider = CreateContainer(url: "http://127.0.0.1:8001");
        using var scope = provider.CreateScope();

        var registry = scope.ServiceProvider.GetRequiredService<AgentRegistry>();
        var agent = registry.Get("ProblemUnderstandingAgent");

        Assert.NotNull(agent);
        var iAgent = Assert.IsAssignableFrom<IAgent>(agent);
        Assert.Equal("ProblemUnderstandingAgent", iAgent.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    [InlineData("ftp://127.0.0.1:8001")]
    public void AgentServicesOptions_Validate_ThrowsOnInvalidUrl(string invalidUrl)
    {
        var options = new AgentServicesOptions
        {
            ProblemUnderstandingUrl = invalidUrl
        };

        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("ProblemUnderstandingUrl", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-45)]
    public void AgentServicesOptions_Validate_ThrowsOnNonPositiveTimeout(int invalidTimeout)
    {
        var options = new AgentServicesOptions
        {
            TimeoutSeconds = invalidTimeout
        };

        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("TimeoutSeconds", ex.Message);
    }
}
