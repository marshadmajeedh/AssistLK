using AssistLK.Agents.Abstractions;
using AssistLK.Agents.Adapters;
using AssistLK.Agents.Clients;
using AssistLK.Agents.Configuration;
using AssistLK.Agents.Core;
using AssistLK.Agents.Tools;
using AssistLK.Api.Middleware;
using AssistLK.Api.Seed;
using AssistLK.Infrastructure;
using System.Text;
using System.Text.Json.Serialization;
using AssistLK.Api.Authentication;
using AssistLK.Application.Interfaces;
using AssistLK.Application.Services;
using AssistLK.Application.Services.Auth;
using AssistLK.Domain.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using dotenv.net;

// Load local .env configuration into environment variables before builder initialization
Program.LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);


// -----------------------------
// Configuration
// -----------------------------

var connectionString =
    builder.Configuration.GetConnectionString(
        "DefaultConnection")
    ?? throw new InvalidOperationException(
        "Database connection string 'DefaultConnection' was not found.");

var jwtKey =
    builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "JWT key is not configured.");

var jwtIssuer =
    builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException(
        "JWT issuer is not configured.");

var jwtAudience =
    builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException(
        "JWT audience is not configured.");


// -----------------------------
// Application Services
// -----------------------------

builder.Services.AddInfrastructure(connectionString);
builder.Services.AddSingleton<AssistLK.Application.Attachments.IServiceRequestAttachmentStorage>(sp =>
{
    var options = new AssistLK.Infrastructure.Attachments.AttachmentStorageOptions();
    sp.GetRequiredService<IConfiguration>().GetSection("AttachmentStorage").Bind(options);
    var environment = sp.GetRequiredService<IWebHostEnvironment>();
    return new AssistLK.Infrastructure.Attachments.PrivateFileAttachmentStorage(
        options.Resolve(environment.ContentRootPath, environment.WebRootPath));
});

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(
                allowIntegerValues: false));
    });


builder.Services.AddScoped<
    IPasswordHasher<User>,
    PasswordHasher<User>>();

builder.Services.AddScoped<
    IAuthService,
    AuthService>();

builder.Services.AddScoped<
    IServiceRequestService,
    ServiceRequestService>();

builder.Services.AddScoped<
    IJwtTokenService,
    JwtTokenService>();


// -----------------------------
// Agent Infrastructure
// -----------------------------

builder.Services.AddScoped<AgentWorkflowService>();
builder.Services.AddScoped<AgentExecutionService>();
builder.Services.AddScoped<AgentMonitoringService>();
builder.Services.AddScoped<AgentMemoryService>();
builder.Services.AddScoped<AgentContextService>();

builder.Services.AddScoped<AgentSafetyService>();

builder.Services.AddSingleton<
    AgentSafetyPolicyEngine>();


// Agent Configuration & Services
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var options = new AgentServicesOptions();
    config.GetSection(AgentServicesOptions.SectionName).Bind(options);

    // Validate options
    options.Validate();
    return options;
});

// External Python Agent Client
builder.Services.AddHttpClient<IProblemUnderstandingClient, ProblemUnderstandingHttpClient>((sp, client) =>
{
    var options = sp.GetRequiredService<AgentServicesOptions>();
    var baseUrl = !string.IsNullOrWhiteSpace(options.ProblemUnderstandingUrl)
        ? options.ProblemUnderstandingUrl.TrimEnd('/')
        : "http://127.0.0.1:8001";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds > 0 ? options.TimeoutSeconds : 45);
});

// Provider Matching Microservice Client
builder.Services.AddHttpClient<AssistLK.Application.Services.Providers.IProviderMatchingService, AssistLK.Application.Services.Providers.ProviderMatchingService>(client =>
{
    client.BaseAddress = new Uri("http://127.0.0.1:8000");
    client.Timeout = TimeSpan.FromSeconds(90);
});
builder.Services.AddScoped<AssistLK.Application.Services.Providers.IProviderMatchingCoordinator, AssistLK.Application.Services.Providers.ProviderMatchingCoordinator>();
builder.Services.AddHostedService<AssistLK.Api.Features.Providers.ProviderMatchingBackgroundWorker>();


builder.Services.AddScoped<ExternalProblemUnderstandingAgentAdapter>();

// Agent Registry (Scoped, Lifetime-safe)
builder.Services.AddScoped<AgentRegistry>(sp =>
{
    var registry = new AgentRegistry();
    registry.Register(
        sp.GetRequiredService<ExternalProblemUnderstandingAgentAdapter>());
    return registry;
});


builder.Services.AddScoped<
    AgentOrchestrator>();


// -----------------------------
// HTTP Clients
// -----------------------------

builder.Services.AddHttpClient();

builder.Services.AddSingleton(sp =>
{
    var options = new AssistLK.Infrastructure.ExternalServices.LocationGeocodingOptions();
    sp.GetRequiredService<IConfiguration>().GetSection("LocationGeocoding").Bind(options);
    options.Validate();
    return options;
});
builder.Services.AddSingleton<AssistLK.Infrastructure.ExternalServices.NominatimRequestCoordinator>();
builder.Services.AddHttpClient<ILocationGeocodingService, AssistLK.Infrastructure.ExternalServices.NominatimReverseGeocodingService>((sp, client) =>
{
    client.Timeout = TimeSpan.FromSeconds(sp.GetRequiredService<AssistLK.Infrastructure.ExternalServices.LocationGeocodingOptions>().TimeoutSeconds);
})
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
    // Factory URL logging would disclose precise coordinates.
    .RemoveAllLoggers();


// -----------------------------
// Tools
// -----------------------------

builder.Services.AddScoped<ToolRegistry>(sp =>
{
    var registry = new ToolRegistry();

    registry.Register(
        sp.GetRequiredService<DemoProviderSearchTool>());

    return registry;
});


builder.Services.AddScoped<
    ToolExecutor>();

builder.Services.AddScoped<
    ProblemUnderstandingWorkflowService>();


builder.Services.AddScoped<
    DemoProviderSearchTool>();


// -----------------------------
// Authentication
// -----------------------------

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)

    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)),

                ClockSkew = TimeSpan.Zero
            };
    });


builder.Services.AddAuthorization();


// -----------------------------
// Swagger
// -----------------------------

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description =
                "Enter JWT token."
        });


    options.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference =
                    new OpenApiReference
                    {
                        Type =
                        ReferenceType.SecurityScheme,
                        Id="Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
});


// -----------------------------
// CORS
// -----------------------------

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "AssistLKClients",
        policy =>
        {
            policy
                .SetIsOriginAllowed(origin =>
                {
                    if (!Uri.TryCreate(
                        origin,
                        UriKind.Absolute,
                        out var uri))
                    {
                        return false;
                    }

                    return uri.Scheme ==
                           Uri.UriSchemeHttp
                           &&
                           uri.Host ==
                           "localhost";
                })
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});


builder.Services.AddHealthChecks();


var app = builder.Build();
// Test hosts substitute fake storage and must never create the developer's private directory.
if (!app.Environment.IsEnvironment("Testing"))
    _ = app.Services.GetRequiredService<AssistLK.Application.Attachments.IServiceRequestAttachmentStorage>();


// -----------------------------
// Middleware
// -----------------------------

app.UseMiddleware<ExceptionHandlingMiddleware>();


if(app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


var uploadsPath = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", "certificates");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseCors("AssistLKClients");

app.UseAuthentication();

app.UseAuthorization();


// -----------------------------
// Endpoints
// -----------------------------

app.MapControllers();

app.MapHealthChecks("/health");


await DevelopmentDataSeeder.SeedAsync(
    app.Services,
    app.Configuration,
    app.Environment);


app.Run();


public partial class Program
{
    /// <summary>
    /// Predictably locates and loads a physical .env file into process environment variables
    /// without overwriting existing operating-system environment variables.
    /// Does not throw if the file does not exist.
    /// </summary>
    public static void LoadDotEnv()
    {
        var envFilePath = ResolveEnvFilePath();
        if (envFilePath != null)
        {
            DotEnv.Load(new DotEnvOptions(
                envFilePaths: new[] { envFilePath },
                ignoreExceptions: true,
                overwriteExistingVars: false
            ));
        }
    }

    /// <summary>
    /// Searches for a .env file from explicit override (DOTENV_PATH),
    /// current working directory, and application base directory ascending up to repository root.
    /// </summary>
    public static string? ResolveEnvFilePath()
    {
        var customPath = Environment.GetEnvironmentVariable("DOTENV_PATH");
        if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
        {
            return customPath;
        }

        var candidateRoots = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var root in candidateRoots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            var dir = new DirectoryInfo(root);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, ".env");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                // Stop traversing upwards once we hit the git repository boundary
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                {
                    break;
                }

                dir = dir.Parent;
            }
        }

        return null;
    }
}
