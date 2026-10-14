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
/// What this class tests:
///   Dependency Injection (DI) service registrations, scope validation, captive dependency detection, and AgentServicesOptions configuration validation for Component 1.
/// Why these tests are needed:
///   Prevents runtime lifetime mismatches (e.g. singletons capturing scoped dependencies), verifies that ExternalProblemUnderstandingAgentAdapter is registered under "ProblemUnderstandingAgent", and guarantees fail-fast validation on misconfigured agent URLs or timeouts.
/// What is real and what is mocked/faked:
///   - Real: Microsoft.Extensions.DependencyInjection container, ServiceCollection, ServiceProvider, and AgentServicesOptions.
///   - Mocked/Faked: None. No live network calls are made.
/// Prerequisites:
///   None. Runs in-memory.
/// Run from repository root:
///   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.ProblemUnderstandingRegistrationTests"
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

    /// <summary>
    /// Purpose: Checks that DI container builds with ValidateScopes = true and resolves AgentRegistry, AgentOrchestrator, and agent without captive dependencies.
    /// Why: Catches subtle multi-threading bugs and concurrency memory leaks where a singleton service accidentally holds onto a scoped service.
    /// Scenario: Builds ServiceProvider with ValidateScopes = true and resolves core agent services inside a scope.
    /// Pass condition: Container builds cleanly and all three services resolve to non-null instances.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ProblemUnderstandingRegistrationTests.DILifetimes_ValidateScopes_SucceedsWithoutSingletonCapturingScoped"
    /// </summary>
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

    /// <summary>
    /// Purpose: Checks that AgentRegistry.Get("ProblemUnderstandingAgent") resolves the concrete ExternalProblemUnderstandingAgentAdapter.
    /// Why: Ensures the orchestrator finds the HTTP adapter for Python FastAPI when dispatching problem understanding tasks.
    /// Scenario: Requests "ProblemUnderstandingAgent" from the scoped AgentRegistry.
    /// Pass condition: Resolved agent is non-null, is an instance of ExternalProblemUnderstandingAgentAdapter, and its Name equals "ProblemUnderstandingAgent".
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ProblemUnderstandingRegistrationTests.AgentRegistry_ResolvesExternalProblemUnderstandingAgentAdapter_AsProblemUnderstandingAgent"
    /// </summary>
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

    /// <summary>
    /// Purpose: Checks that ExternalProblemUnderstandingAgentAdapter implements the IAgent interface and exposes the canonical name "ProblemUnderstandingAgent".
    /// Why: Verifies compliance with the agent abstractions required by AgentOrchestrator.
    /// Scenario: Inspects the resolved agent as an IAgent.
    /// Pass condition: Agent implements IAgent and Name equals "ProblemUnderstandingAgent".
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ProblemUnderstandingRegistrationTests.ExternalProblemUnderstandingAgentAdapter_ImplementsIAgent_AndExposesLogicalName"
    /// </summary>
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

    /// <summary>
    /// Purpose: Checks that invalid, empty, or non-HTTP/HTTPS URLs throw InvalidOperationException during options validation.
    /// Why: Fails fast on application startup if the Python FastAPI agent service URL is misconfigured.
    /// Scenario: Validates AgentServicesOptions with empty, whitespace, malformed, or FTP URLs.
    /// Pass condition: Throws InvalidOperationException mentioning "ProblemUnderstandingUrl".
    /// Note: Parameterized test (Theory). The command below executes all 4 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.ProblemUnderstandingRegistrationTests.AgentServicesOptions_Validate_ThrowsOnInvalidUrl"
    /// </summary>
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

    /// <summary>
    /// Purpose: Checks that timeout values less than or equal to zero throw InvalidOperationException.
    /// Why: Prevents infinite hangs or immediate timeouts due to zero or negative timeout settings.
    /// Scenario: Validates AgentServicesOptions with timeout values of 0, -1, and -45 seconds.
    /// Pass condition: Throws InvalidOperationException mentioning "TimeoutSeconds".
    /// Note: Parameterized test (Theory). The command below executes all 3 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.ProblemUnderstandingRegistrationTests.AgentServicesOptions_Validate_ThrowsOnNonPositiveTimeout"
    /// </summary>
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
