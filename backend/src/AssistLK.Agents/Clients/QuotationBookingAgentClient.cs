using System.Net.Http.Json;
using AssistLK.Agents.Abstractions;
using AssistLK.Agents.Configuration;
using AssistLK.Agents.DTOs;
using Microsoft.Extensions.Logging;

namespace AssistLK.Agents.Clients;

public sealed class QuotationBookingAgentClient : IQuotationBookingAgentClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<QuotationBookingAgentClient> _logger;

    public QuotationBookingAgentClient(
        HttpClient httpClient,
        AgentServicesOptions options,
        ILogger<QuotationBookingAgentClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _httpClient.BaseAddress ??= new Uri(options.QuotationBookingUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

        if (!string.IsNullOrWhiteSpace(options.InternalApiKey))
        {
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                "X-Internal-Api-Key", options.InternalApiKey);
        }
    }

    public Task<QuotationWorkflowStartResponse> StartWorkflowAsync(
        QuotationWorkflowStartRequest request,
        CancellationToken cancellationToken = default)
        => PostAsync<QuotationWorkflowStartRequest, QuotationWorkflowStartResponse>(
            "/workflows/start", request, cancellationToken);

    public Task<QuotationWorkflowResumeResponse> ResumeWorkflowAsync(
        QuotationWorkflowResumeRequest request,
        CancellationToken cancellationToken = default)
        => PostAsync<QuotationWorkflowResumeRequest, QuotationWorkflowResumeResponse>(
            "/workflows/resume", request, cancellationToken);

    private async Task<TResponse> PostAsync<TRequest, TResponse>(
        string path,
        TRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                path, request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Quotation agent returned HTTP {StatusCode} for {Path}.",
                    (int)response.StatusCode, path);
                throw new HttpRequestException(
                    $"Quotation agent returned HTTP {(int)response.StatusCode}.",
                    null,
                    response.StatusCode);
            }

            return await response.Content.ReadFromJsonAsync<TResponse>(
                       cancellationToken: cancellationToken)
                   ?? throw new InvalidOperationException(
                       "Quotation agent returned an empty response.");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Quotation agent timed out for {Path}.", path);
            throw new TimeoutException("Quotation agent timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Could not reach quotation agent at {Path}.", path);
            throw;
        }
    }
}