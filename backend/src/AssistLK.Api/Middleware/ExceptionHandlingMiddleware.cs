using System.Text.Json;
using AssistLK.Api.DTOs;
using AssistLK.Application.Common.Exceptions;

namespace AssistLK.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "An unhandled exception occurred while processing the request."
            );

            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(
        HttpContext context,
        Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode =
            exception switch
            {
                ArgumentException =>
                    StatusCodes.Status400BadRequest,
                UnauthorizedAccessException =>
                    StatusCodes.Status401Unauthorized,
                KeyNotFoundException =>
                    StatusCodes.Status404NotFound,
                Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException =>
                    StatusCodes.Status409Conflict,
                ConflictException =>
                    StatusCodes.Status409Conflict,
                _ =>
                    StatusCodes.Status500InternalServerError
            };

        var response = new ErrorResponse
        {
            StatusCode = context.Response.StatusCode,
            Message =
                context.Response.StatusCode ==
                StatusCodes.Status500InternalServerError
                    ? "An unexpected error occurred."
                    : exception is Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException
                        ? "Request evidence or status changed. Refresh and retry." : exception.Message,
            Details = null
        };

        var json = JsonSerializer.Serialize(
            response,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

        await context.Response.WriteAsync(json);
    }
}