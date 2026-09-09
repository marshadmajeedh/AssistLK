using AssistLK.Agents.Abstractions;
using AssistLK.Agents.Agents;
using AssistLK.Agents.Core;
using AssistLK.Agents.Services;
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


// Agent Registry
builder.Services.AddScoped<AgentRegistry>(sp =>
{
    var registry = new AgentRegistry();

    registry.Register(
        sp.GetRequiredService<ProblemUnderstandingAgent>());

    return registry;
});


builder.Services.AddScoped<
    AgentOrchestrator>();


// -----------------------------
// Gemini
// -----------------------------

builder.Services.AddHttpClient();

builder.Services.AddScoped<
    IGeminiService,
    GeminiService>();

builder.Services.AddScoped<
    GeminiService>();


builder.Services.AddScoped<
    ProblemUnderstandingAgent>();


// -----------------------------
// Tools
// -----------------------------

builder.Services.AddScoped<ToolRegistry>(sp =>
{
    var registry = new ToolRegistry();

    registry.Register(
        sp.GetRequiredService<DemoProviderSearchTool>());

    registry.Register(
        sp.GetRequiredService<ProblemClassificationTool>());

    registry.Register(
        sp.GetRequiredService<LocationExtractionTool>());

    registry.Register(
        sp.GetRequiredService<ServiceKnowledgeTool>());

    return registry;
});


builder.Services.AddScoped<
    ToolExecutor>();

builder.Services.AddScoped<
    ProblemUnderstandingWorkflowService>();


builder.Services.AddScoped<
    DemoProviderSearchTool>();

builder.Services.AddScoped<
    ProblemClassificationTool>();

builder.Services.AddScoped<
    LocationExtractionTool>();

builder.Services.AddScoped<
    ServiceKnowledgeTool>();


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


// -----------------------------
// Middleware
// -----------------------------

app.UseMiddleware<ExceptionHandlingMiddleware>();


if(app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseHttpsRedirection();

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

}