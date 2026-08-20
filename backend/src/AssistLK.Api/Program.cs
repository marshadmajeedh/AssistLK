using AssistLK.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------
// Services
// -------------------------------------------------------

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHealthChecks();

// React development CORS policy.
// More production origins can be added later through configuration.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AssistLKClients", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// -------------------------------------------------------
// Middleware pipeline
// -------------------------------------------------------

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AssistLKClients");

app.UseAuthorization();

// -------------------------------------------------------
// Endpoints
// -------------------------------------------------------

app.MapControllers();

app.MapHealthChecks("/health");

app.Run();

// Required later for ASP.NET integration testing.
public partial class Program
{
}