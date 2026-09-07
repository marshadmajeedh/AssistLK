using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AssistLK.Api.DTOs.ServiceRequests;
using AssistLK.Application.ServiceRequests.DTOs;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;

using System.Text.Json.Serialization;

namespace AssistLK.Api.Tests;

public class ServiceRequestsControllerSecurityTests : IClassFixture<AssistLKApiTestFactory>
{
    private readonly AssistLKApiTestFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ServiceRequestsControllerSecurityTests(AssistLKApiTestFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("POST", "/api/service-requests")]
    [InlineData("GET", "/api/service-requests/my")]
    [InlineData("GET", "/api/service-requests/00000000-0000-0000-0000-000000000001")]
    [InlineData("PUT", "/api/service-requests/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/service-requests/00000000-0000-0000-0000-000000000001/cancel")]
    [InlineData("POST", "/api/service-requests/00000000-0000-0000-0000-000000000001/analyze")]
    [InlineData("POST", "/api/service-requests/00000000-0000-0000-0000-000000000001/ready-for-matching")]
    public async Task Requirement01_UnauthenticatedCalls_AreRejectedWith401(string method, string url)
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), url);

        if (method is "POST" or "PUT")
        {
            request.Content = JsonContent.Create(new { description = "test", locationText = "Colombo" });
        }

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Requirement02_CustomerCanCreateRequest_Returns201CreatedWithLocationHeader()
    {
        var customerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);

        var payload = new CreateServiceRequestRequest
        {
            Description = "Car engine overheated and smoking on the roadside",
            LocationText = "Kandy Road, Kadawatha",
            Latitude = 7.0012m,
            Longitude = 79.9543m
        };

        var response = await client.PostAsJsonAsync("/api/service-requests", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.ServiceRequestId);
        Assert.Equal(customerId, body.CustomerId);
        Assert.Equal(ServiceRequestStatus.Created, body.Status);
        Assert.Equal("Unclassified", body.Category);
        Assert.Equal(ServiceRequestUrgency.Unknown, body.Urgency);
    }

    [Fact]
    public async Task Requirement03_ServiceProviderCannotAccessCustomerEndpoints_Returns403Forbidden()
    {
        var providerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(providerId, UserRole.Provider);

        var payload = new CreateServiceRequestRequest

        {
            Description = "Need mechanic assistance",
            LocationText = "Colombo"
        };

        var response = await client.PostAsJsonAsync("/api/service-requests", payload);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Requirement04_And_05_CustomerIdIsDerivedFromJwt_AndCannotBeOverriddenViaJson()
    {
        var authenticatedCustomerId = Guid.NewGuid();
        var spoofedCustomerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(authenticatedCustomerId, UserRole.Customer);

        // Attempt to pass spoofed customerId in request body
        var payloadWithSpoofedId = new
        {
            customerId = spoofedCustomerId,
            description = "Pipe burst in bathroom floor",
            locationText = "Galle Road, Colombo 03"
        };

        var response = await client.PostAsJsonAsync("/api/service-requests", payloadWithSpoofedId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(body);
        // CustomerId MUST be the authenticated user's ID, ignoring the body
        Assert.Equal(authenticatedCustomerId, body.CustomerId);
        Assert.NotEqual(spoofedCustomerId, body.CustomerId);
    }

    [Fact]
    public async Task Requirement06_CustomerCanRetrieveOwnRequest_Returns200Ok()
    {
        var customerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = requestId,
                CustomerId = customerId,
                Description = "AC stopped cooling",
                LocationText = "Nugegoda",
                Category = "Unclassified",
                Urgency = ServiceRequestUrgency.Unknown,
                Status = ServiceRequestStatus.Created
            });
        });

        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);
        var response = await client.GetAsync($"/api/service-requests/{requestId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(body);
        Assert.Equal(requestId, body.ServiceRequestId);
        Assert.Equal("AC stopped cooling", body.Description);
    }

    [Fact]
    public async Task Requirement07_CustomerCannotRetrieveAnotherCustomersRequest_Returns404NotFound()
    {
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = requestId,
                CustomerId = customerA,
                Description = "Roof leak during heavy rain",
                LocationText = "Negombo",
                Status = ServiceRequestStatus.Created
            });
        });

        var clientB = _factory.CreateAuthenticatedClient(customerB, UserRole.Customer);
        var response = await clientB.GetAsync($"/api/service-requests/{requestId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Requirement08_GetMyRequests_ReturnsOnlyCurrentCustomersRequests()
    {
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddRangeAsync(
                new ServiceRequest
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customerA,
                    Description = "Customer A request 1",
                    LocationText = "Colombo",
                    Status = ServiceRequestStatus.Created
                },
                new ServiceRequest
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customerA,
                    Description = "Customer A request 2",
                    LocationText = "Colombo",
                    Status = ServiceRequestStatus.Created
                },
                new ServiceRequest
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customerB,
                    Description = "Customer B request",
                    LocationText = "Kandy",
                    Status = ServiceRequestStatus.Created
                }
            );
        });

        var clientA = _factory.CreateAuthenticatedClient(customerA, UserRole.Customer);
        var response = await clientA.GetAsync("/api/service-requests/my");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var requests = await response.Content.ReadFromJsonAsync<List<ServiceRequestResponse>>(_jsonOptions);
        Assert.NotNull(requests);
        Assert.All(requests, r => Assert.Equal(customerA, r.CustomerId));
        Assert.Contains(requests, r => r.Description == "Customer A request 1");
        Assert.Contains(requests, r => r.Description == "Customer A request 2");
        Assert.DoesNotContain(requests, r => r.Description == "Customer B request");
    }

    [Fact]
    public async Task Requirement09_CustomerCanUpdatePermittedRequest_Returns200Ok()
    {
        var customerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = requestId,
                CustomerId = customerId,
                Description = "Initial description",
                LocationText = "Colombo 03",
                Status = ServiceRequestStatus.Created
            });
        });

        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);
        var updatePayload = new UpdateServiceRequestRequest
        {
            Description = "Updated description with more details",
            LocationText = "Colombo 07",
            Latitude = 6.9010m,
            Longitude = 79.8600m
        };

        var response = await client.PutAsJsonAsync($"/api/service-requests/{requestId}", updatePayload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Updated description with more details", updated.Description);
        Assert.Equal("Colombo 07", updated.LocationText);
    }

    [Theory]
    [InlineData(ServiceRequestStatus.Analyzing)]
    [InlineData(ServiceRequestStatus.Analyzed)]
    [InlineData(ServiceRequestStatus.ReadyForMatching)]
    public async Task Requirement10_CustomerCannotUpdateProtectedLifecycleState_Returns409Conflict(ServiceRequestStatus protectedStatus)
    {
        var customerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = requestId,
                CustomerId = customerId,
                Description = "Protected request",
                LocationText = "Colombo",
                Status = protectedStatus
            });
        });

        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);
        var updatePayload = new UpdateServiceRequestRequest
        {
            Description = "Illegal modification attempt",
            LocationText = "Colombo"
        };

        var response = await client.PutAsJsonAsync($"/api/service-requests/{requestId}", updatePayload);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData(ServiceRequestStatus.Created)]
    [InlineData(ServiceRequestStatus.Analyzing)]
    [InlineData(ServiceRequestStatus.AwaitingInformation)]
    [InlineData(ServiceRequestStatus.Analyzed)]
    public async Task Requirement11_CustomerCanCancelAllowedRequest_Returns200Ok(ServiceRequestStatus cancellableStatus)
    {
        var customerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = requestId,
                CustomerId = customerId,
                Description = "Request to be cancelled",
                LocationText = "Colombo",
                Status = cancellableStatus
            });
        });

        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);
        var response = await client.PostAsync($"/api/service-requests/{requestId}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cancelled = await response.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(cancelled);
        Assert.Equal(ServiceRequestStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public async Task Requirement12_CustomerCannotCancelAfterReadyForMatching_Returns409Conflict()
    {
        var customerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = requestId,
                CustomerId = customerId,
                Description = "Locked matching request",
                LocationText = "Colombo",
                Status = ServiceRequestStatus.ReadyForMatching
            });
        });

        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);
        var response = await client.PostAsync($"/api/service-requests/{requestId}/cancel", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Requirement13_CustomerCanAnalyzeTheirRequest_Returns200WithStructuredResponse()
    {
        var customerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = requestId,
                CustomerId = customerId,
                Description = "Car engine will not start and makes clicking sound when turning key",
                LocationText = "Kandy, Central Province",
                Status = ServiceRequestStatus.Created
            });
        });

        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);
        var response = await client.PostAsync($"/api/service-requests/{requestId}/analyze", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProblemUnderstandingResponseDto>(_jsonOptions);
        Assert.NotNull(result);
        Assert.Equal(requestId, result.ServiceRequestId);
        Assert.Equal(ServiceRequestStatus.Analyzed, result.Status);
        Assert.Equal("Vehicle Repair", result.Category);
        Assert.False(string.IsNullOrWhiteSpace(result.ProblemSummary));
        Assert.True(result.Confidence > 0m);
        Assert.False(result.NeedsMoreInformation);
    }

    [Fact]
    public async Task Requirement14_CustomerCannotAnalyzeAnotherCustomersRequest_Returns404NotFound()
    {
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = requestId,
                CustomerId = customerA,
                Description = "Water leaking from ceiling",
                LocationText = "Colombo",
                Status = ServiceRequestStatus.Created
            });
        });

        var clientB = _factory.CreateAuthenticatedClient(customerB, UserRole.Customer);
        var response = await clientB.PostAsync($"/api/service-requests/{requestId}/analyze", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Requirement16_AwaitingInformationResponse_ContainsSafeFollowUpQuestions()
    {
        var customerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = requestId,
                CustomerId = customerId,
                Description = "Machine broken", // Ambiguous input triggers AwaitingInformation
                LocationText = "Colombo",
                Status = ServiceRequestStatus.Created
            });
        });

        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);
        var response = await client.PostAsync($"/api/service-requests/{requestId}/analyze", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProblemUnderstandingResponseDto>(_jsonOptions);
        Assert.NotNull(result);
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, result.Status);
        Assert.True(result.NeedsMoreInformation);
        Assert.NotEmpty(result.FollowUpQuestions);
    }

    [Fact]
    public async Task Requirement17_SufficientAnalysisResponse_ContainsStructuredSafeOutput()
    {
        var customerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = requestId,
                CustomerId = customerId,
                Description = "Water pipe has burst in the main bathroom and water is flooding",
                LocationText = "Galle, Southern Province",
                Status = ServiceRequestStatus.Created
            });
        });

        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);
        var response = await client.PostAsync($"/api/service-requests/{requestId}/analyze", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProblemUnderstandingResponseDto>(_jsonOptions);
        Assert.NotNull(result);
        Assert.Equal(ServiceRequestStatus.Analyzed, result.Status);
        Assert.Equal("Plumbing", result.Category);
        Assert.Equal(ServiceRequestUrgency.High, result.Urgency);
        Assert.True(result.Confidence >= 0.7m);
    }

    [Fact]
    public async Task Requirement18_ApiNeverExposesChainOfThoughtOrInternalMemory()
    {
        var customerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = requestId,
                CustomerId = customerId,
                Description = "Air conditioner does not cool and compressor makes loud rattling noise",
                LocationText = "Colombo",
                Status = ServiceRequestStatus.Created
            });
        });

        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);
        var response = await client.PostAsync($"/api/service-requests/{requestId}/analyze", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();

        // Verify string response contains NO internal memory keys, prompts, reasoning or tool logs
        Assert.DoesNotContain("chain_of_thought", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reasoning", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("system_prompt", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("prompt", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ToolExecutor", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AgentMemory", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("problem.category", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("problem.summary", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Requirement19_ReadyForMatchingEndpoint_EnforcesApplicationValidationGate()
    {
        var customerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);

        // Subcase A: AwaitingInformation request is rejected with 409
        var awaitingId = Guid.NewGuid();
        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = awaitingId,
                CustomerId = customerId,
                Description = "Ambiguous issue",
                LocationText = "Colombo",
                Status = ServiceRequestStatus.AwaitingInformation
            });
        });

        var awaitingResponse = await client.PostAsync($"/api/service-requests/{awaitingId}/ready-for-matching", null);
        Assert.Equal(HttpStatusCode.Conflict, awaitingResponse.StatusCode);

        // Subcase B: Created request without analysis is rejected with 409
        var createdId = Guid.NewGuid();
        await _factory.SeedAsync(async db =>
        {
            await db.ServiceRequests.AddAsync(new ServiceRequest
            {
                Id = createdId,
                CustomerId = customerId,
                Description = "Unanalyzed issue",
                LocationText = "Colombo",
                Status = ServiceRequestStatus.Created
            });
        });

        var createdResponse = await client.PostAsync($"/api/service-requests/{createdId}/ready-for-matching", null);
        Assert.Equal(HttpStatusCode.Conflict, createdResponse.StatusCode);

        // Subcase C: Analyzed request with complete structured fields succeeds with 200 OK
        var analyzedId = Guid.NewGuid();
        await _factory.SeedAsync(async db =>
        {
            var req = new ServiceRequest
            {
                Id = analyzedId,
                CustomerId = customerId,
                Description = "Water pipe burst in kitchen",
                LocationText = "Colombo 05",
                Category = "Plumbing",
                Urgency = ServiceRequestUrgency.High,
                Status = ServiceRequestStatus.Analyzed
            };
            var analysis = new ProblemAnalysis
            {
                Id = Guid.NewGuid(),
                ServiceRequestId = analyzedId,
                DetectedProblem = "Burst pipe flooding kitchen",
                Confidence = 0.9m,
                AgentName = "ProblemUnderstandingAgent"
            };
            await db.ServiceRequests.AddAsync(req);
            await db.ProblemAnalyses.AddAsync(analysis);
        });

        var successResponse = await client.PostAsync($"/api/service-requests/{analyzedId}/ready-for-matching", null);
        Assert.Equal(HttpStatusCode.OK, successResponse.StatusCode);

        var matchingReady = await successResponse.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(matchingReady);
        Assert.Equal(ServiceRequestStatus.ReadyForMatching, matchingReady.Status);
    }

    [Fact]
    public void Requirement15_Controller_DoesNotDirectlyReferenceDbContextOrAgent_AndUsesWorkflowService()
    {
        var controllerType = typeof(AssistLK.Api.Controllers.ServiceRequestsController);
        var constructors = controllerType.GetConstructors();
        Assert.Single(constructors);

        var paramTypes = constructors[0].GetParameters().Select(p => p.ParameterType).ToList();
        Assert.Contains(typeof(AssistLK.Application.Interfaces.IServiceRequestService), paramTypes);
        Assert.Contains(typeof(AssistLK.Application.Services.ProblemUnderstandingWorkflowService), paramTypes);

        Assert.DoesNotContain(paramTypes, t => t.Name.Contains("DbContext"));
        Assert.DoesNotContain(paramTypes, t => t.Name.Contains("ProblemUnderstandingAgent"));
    }

    [Fact]
    public async Task ScenarioA_DetailedRequest_FullLifecycle_Created_Analyzing_Analyzed_ReadyForMatching()
    {
        var customerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);

        // 1. Create: Created
        var createPayload = new CreateServiceRequestRequest
        {
            Description = "Car engine overheats quickly and steam comes from the radiator while driving",
            LocationText = "Kandy Road, Kiribathgoda",
            Latitude = 6.9785m,
            Longitude = 79.9287m
        };

        var createResponse = await client.PostAsJsonAsync("/api/service-requests", createPayload);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(created);
        Assert.Equal(ServiceRequestStatus.Created, created.Status);
        var requestId = created.ServiceRequestId;

        // 2. Analyze: Created -> Analyzing -> Analyzed
        var analyzeResponse = await client.PostAsync($"/api/service-requests/{requestId}/analyze", null);
        Assert.Equal(HttpStatusCode.OK, analyzeResponse.StatusCode);
        var analyzed = await analyzeResponse.Content.ReadFromJsonAsync<ProblemUnderstandingResponseDto>(_jsonOptions);
        Assert.NotNull(analyzed);
        Assert.Equal(ServiceRequestStatus.Analyzed, analyzed.Status);
        Assert.Equal("Vehicle Repair", analyzed.Category);
        Assert.False(analyzed.NeedsMoreInformation);
        Assert.True(analyzed.Confidence > 0m);

        // 3. Verify status: Analyzed
        var getResponse = await client.GetAsync($"/api/service-requests/{requestId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(fetched);
        Assert.Equal(ServiceRequestStatus.Analyzed, fetched.Status);

        // 4. Ready for matching: Analyzed -> ReadyForMatching
        var readyResponse = await client.PostAsync($"/api/service-requests/{requestId}/ready-for-matching", null);
        Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);
        var ready = await readyResponse.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(ready);
        Assert.Equal(ServiceRequestStatus.ReadyForMatching, ready.Status);

        // 5. Verify status after handoff: ReadyForMatching
        var finalGet = await client.GetAsync($"/api/service-requests/{requestId}");
        Assert.Equal(HttpStatusCode.OK, finalGet.StatusCode);
        var finalFetched = await finalGet.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(finalFetched);
        Assert.Equal(ServiceRequestStatus.ReadyForMatching, finalFetched.Status);

        // 6. Verify subsequent mutations are locked: Update -> 409, Cancel -> 409
        var updateAttempt = await client.PutAsJsonAsync($"/api/service-requests/{requestId}", new UpdateServiceRequestRequest
        {
            Description = "Changed my mind",
            LocationText = "Colombo"
        });
        Assert.Equal(HttpStatusCode.Conflict, updateAttempt.StatusCode);

        var cancelAttempt = await client.PostAsync($"/api/service-requests/{requestId}/cancel", null);
        Assert.Equal(HttpStatusCode.Conflict, cancelAttempt.StatusCode);
    }

    [Fact]
    public async Task ScenarioB_AmbiguousRequest_FullLifecycle_Created_Analyzing_AwaitingInformation_ReadyForMatchingFails()
    {
        var customerId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(customerId, UserRole.Customer);

        // 1. Create ambiguous request: Created
        var createPayload = new CreateServiceRequestRequest
        {
            Description = "Something broken please help",
            LocationText = "Colombo"
        };

        var createResponse = await client.PostAsJsonAsync("/api/service-requests", createPayload);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(created);
        Assert.Equal(ServiceRequestStatus.Created, created.Status);
        var requestId = created.ServiceRequestId;

        // 2. Analyze: Created -> Analyzing -> AwaitingInformation
        var analyzeResponse = await client.PostAsync($"/api/service-requests/{requestId}/analyze", null);
        Assert.Equal(HttpStatusCode.OK, analyzeResponse.StatusCode);
        var analyzed = await analyzeResponse.Content.ReadFromJsonAsync<ProblemUnderstandingResponseDto>(_jsonOptions);
        Assert.NotNull(analyzed);
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, analyzed.Status);
        Assert.True(analyzed.NeedsMoreInformation);
        Assert.NotEmpty(analyzed.FollowUpQuestions);

        // 3. Verify status: AwaitingInformation
        var getResponse = await client.GetAsync($"/api/service-requests/{requestId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(fetched);
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, fetched.Status);

        // 4. Ready for matching must FAIL from AwaitingInformation (409 Conflict)
        var readyResponse = await client.PostAsync($"/api/service-requests/{requestId}/ready-for-matching", null);
        Assert.Equal(HttpStatusCode.Conflict, readyResponse.StatusCode);

        // 5. Verify status remains AwaitingInformation
        var verifyGet = await client.GetAsync($"/api/service-requests/{requestId}");
        Assert.Equal(HttpStatusCode.OK, verifyGet.StatusCode);
        var verifyFetched = await verifyGet.Content.ReadFromJsonAsync<ServiceRequestResponse>(_jsonOptions);
        Assert.NotNull(verifyFetched);
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, verifyFetched.Status);
    }
}

