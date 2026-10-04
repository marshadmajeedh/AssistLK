using AssistLK.Agents;
using AssistLK.Agents.Abstractions;
using AssistLK.Agents.Adapters;
using AssistLK.Agents.Clients;
using AssistLK.Agents.Configuration;
using AssistLK.Agents.Core;
using AssistLK.Agents.Tools;
using AssistLK.Api.Authentication;
using AssistLK.Api.Middleware;
using AssistLK.Api.Seed;
using AssistLK.Infrastructure;
using System.Text;
using System.Text.Json.Serialization;
using AssistLK.Application.Interfaces;
using AssistLK.Application.Auth;
using AssistLK.Application.ServiceRequests;
using AssistLK.Application.Services;
using AssistLK.Application.Services.Auth;
using AssistLK.Application.Quotations.DTOs;
using AssistLK.Api.Features.ServiceRequests;
using AssistLK.Domain.Entities;
using AssistLK.Infrastructure.Data;
using dotenv.net;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using AssistLK.Api.Hubs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using AssistLK.Infrastructure.Repositories;

// Load local .env configuration into environment variables before builder initialization
Program.LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();


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

var otpPepper =
    builder.Configuration["AuthOtp:OtpPepper"];

if (string.IsNullOrWhiteSpace(otpPepper))
{
    throw new InvalidOperationException(
        "AuthOtp:OtpPepper is not configured. An explicit pepper must be provided via dotnet user-secrets or environment variable 'AuthOtp__OtpPepper'.");
}

if (otpPepper.Trim().Length < 16)
{
    throw new InvalidOperationException(
        "AuthOtp:OtpPepper is too weak. The pepper must contain at least 16 characters.");
}


// -----------------------------
// Application Services
// -----------------------------

builder.Services.AddInfrastructure(connectionString);
builder.Services.AddSingleton(sp =>
{
    var options = new AssistLK.Infrastructure.Attachments.SupabaseStorageOptions();
    sp.GetRequiredService<IConfiguration>().GetSection("Supabase").Bind(options);
    return options;
});
builder.Services.AddHttpClient<
    AssistLK.Application.Attachments.IProofOfWorkStorage,
    AssistLK.Infrastructure.Attachments.SupabaseProofOfWorkStorage>();
builder.Services.AddSingleton<AssistLK.Application.Attachments.IServiceRequestAttachmentStorage>(sp =>
{
    var options = new AssistLK.Infrastructure.Attachments.AttachmentStorageOptions();
    sp.GetRequiredService<IConfiguration>().GetSection("AttachmentStorage").Bind(options);
    var environment = sp.GetRequiredService<IWebHostEnvironment>();
    return new AssistLK.Infrastructure.Attachments.PrivateFileAttachmentStorage(
        options.Resolve(environment.ContentRootPath, environment.WebRootPath));
});

// DbContext Registration (PostgreSQL / Npgsql)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null)));

builder.Services.AddScoped<IServiceJobsDbContext>(
    serviceProvider => serviceProvider.GetRequiredService<ApplicationDbContext>());
builder.Services.AddScoped<IProviderProfileDbContext>(
    serviceProvider => serviceProvider.GetRequiredService<AssistLKDbContext>());

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(
                allowIntegerValues: false));
    });

builder.Services.AddSignalR();
builder.Services
    .AddFluentValidationAutoValidation()
    .AddFluentValidationClientsideAdapters()
    .AddValidatorsFromAssemblyContaining<CreateQuotationDtoValidator>();


builder.Services.AddScoped<
    IPasswordHasher<User>,
    PasswordHasher<User>>();

builder.Services.AddScoped<
    IAuthService,
    AuthService>();

builder.Services.AddScoped<
    IServiceRequestService,
    ServiceRequestService>();

builder.Services.AddScoped<FeedbackApplicationService>();

builder.Services.AddScoped<
    IJwtTokenService,
    JwtTokenService>();

builder.Services.AddScoped<
    IOtpSecurityService,
    OtpSecurityService>();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<
        ISmsService,
        AssistLK.Infrastructure.ExternalServices.Sms.LocalDevSmsService>();
}
else
{
    builder.Services.AddScoped<
        ISmsService,
        AssistLK.Infrastructure.ExternalServices.Sms.FailingProductionSmsService>();
}


// -----------------------------
// Agent Infrastructure
// -----------------------------

// Agents Project Services Registration (Custom Assistant Agent DI)
builder.Services.AddAgentServices();

builder.Services.AddScoped<AgentWorkflowService>();
builder.Services.AddScoped<AgentExecutionService>();
builder.Services.AddScoped<AgentMonitoringService>();
builder.Services.AddScoped<AgentMemoryService>();
builder.Services.AddScoped<AgentContextService>();

builder.Services.AddScoped<AgentSafetyService>();

builder.Services.AddScoped<IQuotationRepository, QuotationRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<AssistLK.Application.Services.Quotations.IQuotationService,
    AssistLK.Application.Services.Quotations.QuotationService>();

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

// External Python Agent Client (Agent 1)
builder.Services.AddHttpClient<IProblemUnderstandingClient, ProblemUnderstandingHttpClient>((sp, client) =>
{
    var options = sp.GetRequiredService<AgentServicesOptions>();
    var baseUrl = !string.IsNullOrWhiteSpace(options.ProblemUnderstandingUrl)
        ? options.ProblemUnderstandingUrl.TrimEnd('/')
        : "http://127.0.0.1:8001";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds > 0 ? options.TimeoutSeconds : 45);
});

// 🆕 Agent 4 (Validation & Safety Agent) HttpClient Registration
builder.Services.AddHttpClient<ValidationSafetyAgent>((sp, client) =>
{
    var options = sp.GetRequiredService<AgentServicesOptions>();
    var baseUrl = !string.IsNullOrWhiteSpace(options.TrackingValidationUrl)
        ? options.TrackingValidationUrl.TrimEnd('/')
        : "http://127.0.0.1:8003";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds > 0 ? options.TimeoutSeconds : 45);
});

builder.Services.AddHttpClient<IQuotationBookingAgentClient, QuotationBookingAgentClient>();

// Provider Matching Microservice Client
builder.Services.AddHttpClient<AssistLK.Application.Services.Providers.IProviderMatchingService, AssistLK.Application.Services.Providers.ProviderMatchingService>((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var baseUrl = config["AgentServices:ProviderMatchingUrl"];
    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        baseUrl = "http://127.0.0.1:8000";
    }
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/'));
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

// C1 Stale Analysis Recovery
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var options = new C1RecoveryOptions();
    config.GetSection(C1RecoveryOptions.SectionName).Bind(options);
    options.Validate();
    return options;
});
builder.Services.AddScoped<IStaleAnalysisRecoveryService, StaleAnalysisRecoveryService>();
builder.Services.AddHostedService<StaleAnalysisRecoveryBackgroundService>();

// C1 Service Request Lifecycle Expiration
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var options = new AssistLK.Application.ServiceRequests.ServiceRequestLifecycleOptions();
    config.GetSection(AssistLK.Application.ServiceRequests.ServiceRequestLifecycleOptions.SectionName).Bind(options);
    options.Validate();
    return options;
});
builder.Services.AddScoped<IServiceRequestLifecycleExpirationService, ServiceRequestLifecycleExpirationService>();
builder.Services.AddHostedService<ServiceRequestLifecycleExpirationBackgroundService>();

// C1 Attachment Reconciliation
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var options = new AssistLK.Application.Attachments.AttachmentReconciliationOptions();
    config.GetSection(AssistLK.Application.Attachments.AttachmentReconciliationOptions.SectionName).Bind(options);
    options.Validate();
    return options;
});
builder.Services.AddHostedService<AttachmentReconciliationBackgroundService>();

// Challenge Retention Cleanup
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var options = new RegistrationChallengeRetentionOptions();
    config.GetSection(RegistrationChallengeRetentionOptions.SectionName).Bind(options);
    options.Validate();
    return options;
});
builder.Services.AddScoped<IRegistrationChallengeCleanupService, RegistrationChallengeCleanupService>();
builder.Services.AddHostedService<RegistrationChallengeCleanupBackgroundService>();

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

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var requestPath = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) &&
                    requestPath.StartsWithSegments("/hubs/tracking"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });


builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("OtpPolicy", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = builder.Environment.IsEnvironment("Testing") ? 10000 : 30;
        opt.QueueLimit = 0;
    });
});


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

                    if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                    {
                        return false;
                    }

                    // Local development (localhost, 127.0.0.1)
                    if (uri.Host == "localhost" || uri.Host == "127.0.0.1")
                    {
                        return true;
                    }

                    // Vercel deployment domains (*.vercel.app)
                    if (uri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    // Railway deployment domains (*.railway.app, *.up.railway.app)
                    if (uri.Host.EndsWith(".railway.app", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    // Optional custom configured origins via Cors:AllowedOrigins
                    var configuredOrigins = builder.Configuration["Cors:AllowedOrigins"];
                    if (!string.IsNullOrWhiteSpace(configuredOrigins))
                    {
                        var allowedList = configuredOrigins
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        return allowedList.Any(allowed => string.Equals(allowed.TrimEnd('/'), origin.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
                    }

                    return false;
                })
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
});


builder.Services.AddHealthChecks();

//component 3 - Service Request Lookup.
builder.Services.AddScoped<IServiceRequestLookup, ServiceRequestLookup>();


var app = builder.Build();
// Test hosts substitute fake storage and must never create the developer's private directory.
if (!app.Environment.IsEnvironment("Testing"))
    _ = app.Services.GetRequiredService<AssistLK.Application.Attachments.IServiceRequestAttachmentStorage>();


// -----------------------------
// Middleware
// -----------------------------

app.UseMiddleware<ExceptionHandlingMiddleware>();


var enableSwagger = app.Environment.IsDevelopment()
    || app.Configuration.GetValue<bool>("ENABLE_SWAGGER", true);

if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


var uploadsPath = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", "certificates");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

app.UseCors("AssistLKClients");

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseAuthentication();

app.UseAuthorization();

app.UseRateLimiter();


// -----------------------------
// Endpoints
// -----------------------------

app.MapControllers();
app.MapHub<TrackingHub>("/hubs/tracking");

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