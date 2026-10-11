using System.Net;
using System.Text.Json;
using System;
using AssistLK.Agents.Abstractions;
using AssistLK.Agents.Clients;
using AssistLK.Agents.Configuration;
using AssistLK.Agents.DTOs;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssistLK.IntegrationTests;

public class QuotationBookingAgentClientTests
{
    [Fact]
    public async Task StartAndResume_UsePythonContractAndInternalKey()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        var options = new AgentServicesOptions
        {
            QuotationBookingUrl = "http://localhost:8002",
            InternalApiKey = "test-key",
        };
        var serviceRequestId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        IQuotationBookingAgentClient client = new QuotationBookingAgentClient(
            httpClient, options, NullLogger<QuotationBookingAgentClient>.Instance);
        Assert.Equal(TimeSpan.FromSeconds(60), httpClient.Timeout);

        var started = await client.StartWorkflowAsync(new QuotationWorkflowStartRequest
        {
            QuotationId = 42,
            ServiceRequestId = serviceRequestId,
            ProviderId = providerId,
            Items = [new QuotationWorkflowItem
            {
                Description = "Repair",
                Amount = 25m,
                Quantity = 2,
            }],
        });

        var resumed = await client.ResumeWorkflowAsync(new QuotationWorkflowResumeRequest
        {
            ThreadId = started.ThreadId,
            Decision = "approve",
            Remarks = "Approved",
        });

        Assert.Equal("thread-42", started.ThreadId);
        Assert.Equal(42, started.ApprovalRequest?.QuotationId);
        Assert.Equal(
            Guid.Parse("00000000-0000-0000-0000-000000000009"),
            started.ApprovalRequest?.ServiceRequestId);
        Assert.Equal(
            Guid.Parse("00000000-0000-0000-0000-000000000017"),
            started.ApprovalRequest?.ProviderId);
        Assert.Equal("completed", resumed.Status);
        Assert.Equal(42, resumed.FinalState?.QuotationId);
        Assert.Equal("approve", resumed.FinalState?.CustomerDecision);
        Assert.Collection(
            handler.Requests,
            start =>
            {
                Assert.Equal("/workflows/start", start.Path);
                Assert.Equal("test-key", start.InternalApiKey);
                using var payload = JsonDocument.Parse(start.Body!);
                Assert.Equal(42, payload.RootElement.GetProperty("quotation_id").GetInt32());
                Assert.Equal(
                    serviceRequestId.ToString(),
                    payload.RootElement.GetProperty("service_request_id").GetString());
                Assert.Equal(
                    providerId.ToString(),
                    payload.RootElement.GetProperty("provider_id").GetString());
                Assert.Equal(25m, payload.RootElement.GetProperty("items")[0].GetProperty("amount").GetDecimal());
            },
            resume =>
            {
                Assert.Equal("/workflows/resume", resume.Path);
                Assert.Equal("test-key", resume.InternalApiKey);
                using var payload = JsonDocument.Parse(resume.Body!);
                Assert.Equal("thread-42", payload.RootElement.GetProperty("thread_id").GetString());
                Assert.Equal("approve", payload.RootElement.GetProperty("decision").GetString());
            });
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.RequestUri!.AbsolutePath,
                await request.Content!.ReadAsStringAsync(cancellationToken),
                request.Headers.GetValues("X-Internal-Api-Key").Single()));

            var responseBody = request.RequestUri.AbsolutePath == "/workflows/start"
                ? """
                  {"thread_id":"thread-42","status":"waiting_for_approval","approval_request":{"type":"quotation_approval","quotation_id":42,"service_request_id":"00000000-0000-0000-0000-000000000009","provider_id":"00000000-0000-0000-0000-000000000017","total_amount":50,"allowed_actions":["approve","reject"],"message":"Approve?"}}
                  """
                : """
                  {"thread_id":"thread-42","status":"completed","final_state":{"quotation_id":42,"customer_decision":"approve","customer_remarks":"Approved","final_message":"Customer approved the quotation."}}
                  """;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody),
            };
        }
    }

    private sealed record RecordedRequest(string Path, string Body, string InternalApiKey);
}