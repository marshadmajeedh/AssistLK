using AssistLK.Application.Common.Exceptions;
using AssistLK.Application.Interfaces;
using AssistLK.Application.ServiceRequests.DTOs;
using AssistLK.Application.Services;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;

namespace AssistLK.IntegrationTests;

public class ServiceRequestServiceTests
{
    [Fact]
    public async Task CreateAsync_UsesInitialValuesAndDoesNotAcceptServerFields()
    {
        var repositories = CreateService();
        var customerId = Guid.NewGuid();

        var response = await repositories.Service.CreateAsync(
            customerId,
            new CreateServiceRequestRequest
            {
                Description = "Vehicle will not start",
                LocationText = "Colombo"
            });

        var request = repositories.Requests.Single();
        Assert.Equal(customerId, request.CustomerId);
        Assert.Null(request.CategoryHint);
        Assert.Null(response.CategoryHint);
        Assert.Equal("Unclassified", response.Category);
        Assert.Equal(ServiceRequestUrgency.Unknown, response.Urgency);
        Assert.Equal(ServiceRequestStatus.Created, response.Status);
        Assert.NotEqual(Guid.Empty, response.ServiceRequestId);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("Plumbing", "Plumbing")]
    [InlineData("Electrical", "Electrical")]
    [InlineData("Vehicle Repair", "Vehicle Repair")]
    [InlineData("Appliance Repair", "Appliance Repair")]
    public async Task CreateAsync_WithValidCategoryHints_PersistsHint_AndLeavesCategoryUnclassified(
        string? inputHint,
        string? expectedHint)
    {
        var repositories = CreateService();
        var customerId = Guid.NewGuid();

        var response = await repositories.Service.CreateAsync(
            customerId,
            new CreateServiceRequestRequest
            {
                Description = "Water leaking under sink",
                LocationText = "Colombo",
                CategoryHint = inputHint
            });

        var persisted = repositories.Requests.Single();
        Assert.Equal(expectedHint, persisted.CategoryHint);
        Assert.Equal(expectedHint, response.CategoryHint);
        Assert.Equal("Unclassified", persisted.Category);
        Assert.Equal("Unclassified", response.Category);
    }

    [Theory]
    [InlineData("Vehicle Assistance")]
    [InlineData("Cleaning")]
    [InlineData("AC Service")]
    [InlineData("Carpentry")]
    [InlineData("Gardening")]
    [InlineData("Work")]
    [InlineData("Emergency")]
    [InlineData("vehicle")]
    [InlineData("random text")]
    public async Task CreateAsync_WithInvalidCategoryHints_ThrowsArgumentException(string invalidHint)
    {
        var repositories = CreateService();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            repositories.Service.CreateAsync(
                Guid.NewGuid(),
                new CreateServiceRequestRequest
                {
                    Description = "Valid problem description",
                    LocationText = "Colombo",
                    CategoryHint = invalidHint
                }));

        Assert.Contains("invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateAsync_CanUpdateCategoryHint_InEditableStatuses_WithoutChangingCategory()
    {
        var repositories = CreateService();
        var customerId = Guid.NewGuid();
        var created = await repositories.Service.CreateAsync(
            customerId,
            new CreateServiceRequestRequest
            {
                Description = "Initial description",
                LocationText = "Colombo",
                CategoryHint = "Plumbing"
            });

        Assert.Equal("Plumbing", created.CategoryHint);
        Assert.Equal("Unclassified", created.Category);

        // Update in Created status to Electrical
        var updatedInCreated = await repositories.Service.UpdateAsync(
            customerId,
            created.ServiceRequestId,
            new UpdateServiceRequestRequest
            {
                Description = "Updated description",
                LocationText = "Colombo",
                CategoryHint = "Electrical"
            });

        Assert.Equal("Electrical", updatedInCreated.CategoryHint);
        Assert.Equal("Unclassified", updatedInCreated.Category);

        // Transition to AwaitingInformation
        var entity = repositories.Requests.Single();
        entity.Status = ServiceRequestStatus.AwaitingInformation;
        entity.Category = "Electrical"; // Simulate prior analysis result

        // Update in AwaitingInformation status to Appliance Repair
        var updatedInAwaiting = await repositories.Service.UpdateAsync(
            customerId,
            created.ServiceRequestId,
            new UpdateServiceRequestRequest
            {
                Description = "Further updated description",
                LocationText = "Colombo",
                CategoryHint = "Appliance Repair"
            });

        Assert.Equal("Appliance Repair", updatedInAwaiting.CategoryHint);
        Assert.Equal("Electrical", updatedInAwaiting.Category); // Category untouched by hint edit
    }

    [Theory]
    [InlineData("Vehicle Assistance")]
    [InlineData("Cleaning")]
    [InlineData("invalid")]
    public async Task UpdateAsync_WithInvalidCategoryHint_ThrowsArgumentException(string invalidHint)
    {
        var repositories = CreateService();
        var customerId = Guid.NewGuid();
        var created = await repositories.Service.CreateAsync(
            customerId,
            new CreateServiceRequestRequest
            {
                Description = "Initial description",
                LocationText = "Colombo"
            });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repositories.Service.UpdateAsync(
                customerId,
                created.ServiceRequestId,
                new UpdateServiceRequestRequest
                {
                    Description = "Valid update",
                    LocationText = "Colombo",
                    CategoryHint = invalidHint
                }));
    }

    [Fact]
    public async Task CreateAsync_RejectsInvalidFieldsAndPartialCoordinates()
    {
        var repositories = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repositories.Service.CreateAsync(
                Guid.NewGuid(),
                new CreateServiceRequestRequest
                {
                    Description = "",
                    LocationText = "Colombo",
                    Latitude = 6.9m
                }));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repositories.Service.CreateAsync(
                Guid.NewGuid(),
                new CreateServiceRequestRequest
                {
                    Description = "Valid description",
                    LocationText = "Colombo",
                    Latitude = 91m,
                    Longitude = 80m
                }));
    }

    [Fact]
    public async Task CustomerQueriesFilterByCustomerAndOwnershipIsRequiredForUpdates()
    {
        var repositories = CreateService();
        var owner = Guid.NewGuid();
        var otherCustomer = Guid.NewGuid();
        var owned = await CreateRequest(repositories.Service, owner, "Owned");
        await CreateRequest(repositories.Service, otherCustomer, "Other");

        var requests = await repositories.Service.GetCurrentCustomerRequestsAsync(owner);

        Assert.Single(requests);
        Assert.Equal(owned.ServiceRequestId, requests[0].ServiceRequestId);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            repositories.Service.UpdateAsync(
                otherCustomer,
                owned.ServiceRequestId,
                ValidUpdate()));
    }

    [Fact]
    public async Task UpdateAsync_AllowsOnlyEditableStatuses()
    {
        var repositories = CreateService();
        var customerId = Guid.NewGuid();
        var response = await CreateRequest(repositories.Service, customerId, "Before");

        var updated = await repositories.Service.UpdateAsync(
            customerId,
            response.ServiceRequestId,
            new UpdateServiceRequestRequest
            {
                Description = "After",
                LocationText = "Kandy",
                Latitude = 7.29m,
                Longitude = 80.63m
            });

        Assert.Equal("After", updated.Description);
        Assert.Equal("Kandy", updated.LocationText);

        repositories.Requests.Single().Status = ServiceRequestStatus.Analyzing;
        await Assert.ThrowsAsync<ConflictException>(() =>
            repositories.Service.UpdateAsync(customerId, response.ServiceRequestId, ValidUpdate()));

        repositories.Requests.Single().Status = ServiceRequestStatus.AwaitingInformation;
        var awaitingUpdate = await repositories.Service.UpdateAsync(
            customerId,
            response.ServiceRequestId,
            ValidUpdate());
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, awaitingUpdate.Status);
    }

    [Fact]
    public async Task CancelAsync_AllowsOnlySpecifiedStatusesAndPreservesRecord()
    {
        var repositories = CreateService();
        var customerId = Guid.NewGuid();
        var response = await CreateRequest(repositories.Service, customerId, "Cancel me");

        var cancelled = await repositories.Service.CancelAsync(
            customerId,
            response.ServiceRequestId);

        Assert.Equal(ServiceRequestStatus.Cancelled, cancelled.Status);
        Assert.Single(repositories.Requests);

        var ready = await CreateRequest(repositories.Service, customerId, "Ready");
        repositories.Requests.Single(x => x.Id == ready.ServiceRequestId).Status =
            ServiceRequestStatus.ReadyForMatching;
        await Assert.ThrowsAsync<ConflictException>(() =>
            repositories.Service.CancelAsync(customerId, ready.ServiceRequestId));
    }

    [Fact]
    public async Task ApplyProblemAnalysisResult_PersistsResultAndUpdatesCategoryUrgencyAndStatus()
    {
        var repositories = CreateService();
        var response = await CreateRequest(repositories.Service, Guid.NewGuid(), "Analyze me");

        var analysis = await repositories.Service.ApplyProblemAnalysisResultAsync(
            new ApplyProblemAnalysisResult
            {
                ServiceRequestId = response.ServiceRequestId,
                DetectedProblem = "Possible battery failure",
                Category = "Vehicle Repair",
                Urgency = ServiceRequestUrgency.High,
                Confidence = 0.85m,
                AgentName = "ProblemUnderstandingAgent"
            });

        var request = repositories.Requests.Single();
        Assert.Single(repositories.Analyses);
        Assert.Equal("Possible battery failure", analysis.DetectedProblem);
        Assert.Equal("Vehicle Repair", request.Category);
        Assert.Equal(ServiceRequestUrgency.High, request.Urgency);
        Assert.Equal(ServiceRequestStatus.Analyzed, request.Status);
    }

    [Fact]
    public async Task ApplyProblemAnalysisResult_RejectsInvalidConfidenceAndSupportsAwaitingInformation()
    {
        var repositories = CreateService();
        var response = await CreateRequest(repositories.Service, Guid.NewGuid(), "Need details");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repositories.Service.ApplyProblemAnalysisResultAsync(
                new ApplyProblemAnalysisResult
                {
                    ServiceRequestId = response.ServiceRequestId,
                    DetectedProblem = "Problem",
                    Category = "Home Repair",
                    Confidence = 1.1m,
                    AgentName = "Agent"
                }));

        await repositories.Service.ApplyProblemAnalysisResultAsync(
            new ApplyProblemAnalysisResult
            {
                ServiceRequestId = response.ServiceRequestId,
                DetectedProblem = "Need location detail",
                Category = "Home Repair",
                Urgency = ServiceRequestUrgency.Medium,
                Confidence = 0.5m,
                AgentName = "Agent",
                NeedsMoreInformation = true
            });

        Assert.Equal(
            ServiceRequestStatus.AwaitingInformation,
            repositories.Requests.Single().Status);
    }

    [Fact]
    public async Task GetReadyForMatchingAsync_ReturnsContractOnlyWhenReady()
    {
        var repositories = CreateService();
        var response = await CreateRequest(repositories.Service, Guid.NewGuid(), "Match me");
        repositories.Requests.Single().Status = ServiceRequestStatus.Analyzed;

        Assert.Null(await repositories.Service.GetReadyForMatchingAsync(response.ServiceRequestId));

        var analysis = new ProblemAnalysis
        {
            ServiceRequestId = response.ServiceRequestId,
            DetectedProblem = "Engine issue",
            Confidence = 0.92m,
            AgentName = "Agent",
            CreatedAt = DateTime.UtcNow
        };
        repositories.Analyses.Add(analysis);
        repositories.Requests.Single().Category = "Vehicle Repair";
        repositories.Requests.Single().Urgency = ServiceRequestUrgency.High;
        repositories.Requests.Single().Status = ServiceRequestStatus.ReadyForMatching;
        repositories.Requests.Single().LocationSource = LocationSource.OpenStreetMap;
        repositories.Requests.Single().Latitude = 6.905m;
        repositories.Requests.Single().Longitude = 79.9195m;

        var matching = await repositories.Service.GetReadyForMatchingAsync(response.ServiceRequestId);

        Assert.NotNull(matching);
        Assert.Equal("Colombo", matching.LocationText);
        Assert.Equal(LocationSource.OpenStreetMap, matching.LocationSource);
        Assert.Equal(6.905m, matching.Latitude);
        Assert.Equal(79.9195m, matching.Longitude);
        Assert.Equal("Engine issue", matching!.ProblemSummary);
        Assert.Equal(ServiceRequestStatus.ReadyForMatching, matching.Status);
        Assert.DoesNotContain("CustomerId", matching.GetType().GetProperties().Select(x => x.Name));
    }

    [Fact]
    public async Task GetAllForAdminAsync_ReturnsAllRequestsAcrossCustomers()
    {
        var repositories = CreateService();
        var customer1 = Guid.NewGuid();
        var customer2 = Guid.NewGuid();

        await CreateRequest(repositories.Service, customer1, "Issue 1");
        await CreateRequest(repositories.Service, customer2, "Issue 2");

        var results = await repositories.Service.GetAllForAdminAsync();

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task GetAllForAdminAsync_FiltersCorrectly()
    {
        var repositories = CreateService();
        var c1 = Guid.NewGuid();
        var req1 = await CreateRequest(repositories.Service, c1, "Plumbing request");
        var req2 = await CreateRequest(repositories.Service, c1, "Electrical request");

        repositories.Requests.Single(x => x.Id == req1.ServiceRequestId).Category = "Plumbing";
        repositories.Requests.Single(x => x.Id == req1.ServiceRequestId).Status = ServiceRequestStatus.Analyzed;
        repositories.Requests.Single(x => x.Id == req1.ServiceRequestId).Urgency = ServiceRequestUrgency.High;

        repositories.Requests.Single(x => x.Id == req2.ServiceRequestId).Category = "Electrical";
        repositories.Requests.Single(x => x.Id == req2.ServiceRequestId).Status = ServiceRequestStatus.Created;
        repositories.Requests.Single(x => x.Id == req2.ServiceRequestId).Urgency = ServiceRequestUrgency.Low;

        var plumbingOnly = await repositories.Service.GetAllForAdminAsync(category: "Plumbing");
        Assert.Single(plumbingOnly);
        Assert.Equal(req1.ServiceRequestId, plumbingOnly[0].ServiceRequestId);

        var analyzedOnly = await repositories.Service.GetAllForAdminAsync(status: "Analyzed");
        Assert.Single(analyzedOnly);
        Assert.Equal(req1.ServiceRequestId, analyzedOnly[0].ServiceRequestId);

        var highOnly = await repositories.Service.GetAllForAdminAsync(urgency: "High");
        Assert.Single(highOnly);
        Assert.Equal(req1.ServiceRequestId, highOnly[0].ServiceRequestId);

        var combined = await repositories.Service.GetAllForAdminAsync(status: "Analyzed", category: "Plumbing", urgency: "High");
        Assert.Single(combined);

        var nonMatching = await repositories.Service.GetAllForAdminAsync(status: "Cancelled");
        Assert.Empty(nonMatching);
    }

    [Theory]
    [InlineData("InvalidStatus", null, null)]
    [InlineData(null, "InvalidCategory", null)]
    [InlineData(null, null, "InvalidUrgency")]
    public async Task GetAllForAdminAsync_InvalidFilters_ThrowArgumentException(string? status, string? category, string? urgency)
    {
        var repositories = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repositories.Service.GetAllForAdminAsync(status, category, urgency));
    }

    [Fact]
    public async Task GetByIdForAdminAsync_ReturnsDetail_RegardlessOfCustomer()
    {
        var repositories = CreateService();
        var customerId = Guid.NewGuid();
        var created = await CreateRequest(repositories.Service, customerId, "Test request");

        var result = await repositories.Service.GetByIdForAdminAsync(created.ServiceRequestId);

        Assert.NotNull(result);
        Assert.Equal(created.ServiceRequestId, result.ServiceRequestId);
        Assert.Equal(customerId, result.CustomerId);
    }

    [Fact]
    public async Task GetByIdForAdminAsync_NotFound_ThrowsKeyNotFoundException()
    {
        var repositories = CreateService();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            repositories.Service.GetByIdForAdminAsync(Guid.NewGuid()));
    }

    private static (ServiceRequestService Service, List<ServiceRequest> Requests, List<ProblemAnalysis> Analyses) CreateService()
    {
        var requests = new List<ServiceRequest>();
        var analyses = new List<ProblemAnalysis>();
        return (
            new ServiceRequestService(
                new InMemoryServiceRequestRepository(requests, analyses),
                new InMemoryProblemAnalysisRepository(analyses),
                new TestDoubles.InMemoryServiceJobRepository()),
            requests,
            analyses);
    }

    private static async Task<ServiceRequestResponse> CreateRequest(
        ServiceRequestService service,
        Guid customerId,
        string description)
    {
        return await service.CreateAsync(
            customerId,
            new CreateServiceRequestRequest
            {
                Description = description,
                LocationText = "Colombo"
            });
    }

    private static UpdateServiceRequestRequest ValidUpdate() => new()
    {
        Description = "Updated description",
        LocationText = "Colombo"
    };

    private sealed class InMemoryServiceRequestRepository : IServiceRequestRepository
    {
        private readonly List<ServiceRequest> _requests;
        private readonly List<ProblemAnalysis> _analyses;

        public InMemoryServiceRequestRepository(
            List<ServiceRequest> requests,
            List<ProblemAnalysis> analyses)
        {
            _requests = requests;
            _analyses = analyses;
        }

        public Task<ServiceRequest?> GetByIdAsync(
            Guid id,
            bool includeProblemAnalyses = false,
            CancellationToken cancellationToken = default)
        {
            var request = _requests.SingleOrDefault(x => x.Id == id);
            if (includeProblemAnalyses && request is not null)
            {
                request.ProblemAnalyses = _analyses
                    .Where(x => x.ServiceRequestId == id)
                    .ToList();
            }

            return Task.FromResult(request);
        }

        public Task<ServiceRequest?> GetByIdAndCustomerIdAsync(Guid id, Guid customerId, bool includeProblemAnalyses = false, CancellationToken cancellationToken = default)
        {
            var request = _requests.SingleOrDefault(x => x.Id == id && x.CustomerId == customerId);
            if (includeProblemAnalyses && request is not null)
            {
                request.ProblemAnalyses = _analyses
                    .Where(x => x.ServiceRequestId == id)
                    .ToList();
            }

            return Task.FromResult(request);
        }

        public Task<IReadOnlyList<ServiceRequest>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ServiceRequest>>(_requests.Where(x => x.CustomerId == customerId).ToArray());

        public Task<IReadOnlyList<ServiceRequest>> GetByStatusAsync(ServiceRequestStatus status, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ServiceRequest>>(_requests.Where(x => x.Status == status).ToArray());

        public Task<IReadOnlyList<ServiceRequest>> GetAllForAdminAsync(
            ServiceRequestStatus? status = null,
            string? category = null,
            ServiceRequestUrgency? urgency = null,
            CancellationToken cancellationToken = default)
        {
            var query = _requests.AsEnumerable();

            if (status.HasValue)
            {
                query = query.Where(x => x.Status == status.Value);
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(x => string.Equals(x.Category, category, StringComparison.OrdinalIgnoreCase));
            }

            if (urgency.HasValue)
            {
                query = query.Where(x => x.Urgency == urgency.Value);
            }

            foreach (var req in query)
            {
                req.ProblemAnalyses = _analyses.Where(a => a.ServiceRequestId == req.Id).ToList();
            }

            return Task.FromResult<IReadOnlyList<ServiceRequest>>(
                query.OrderByDescending(x => x.CreatedAt).ToArray());
        }

        public Task AddAsync(ServiceRequest serviceRequest, CancellationToken cancellationToken = default)
        {
            _requests.Add(serviceRequest);
            return Task.CompletedTask;
        }

        public void Update(ServiceRequest serviceRequest)
        {
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryProblemAnalysisRepository : IProblemAnalysisRepository
    {
        private readonly List<ProblemAnalysis> _analyses;

        public InMemoryProblemAnalysisRepository(List<ProblemAnalysis> analyses)
        {
            _analyses = analyses;
        }

        public Task<IReadOnlyList<ProblemAnalysis>> GetByServiceRequestIdAsync(Guid serviceRequestId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProblemAnalysis>>(_analyses.Where(x => x.ServiceRequestId == serviceRequestId).ToArray());

        public Task<ProblemAnalysis?> GetMostRecentByServiceRequestIdAsync(Guid serviceRequestId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_analyses.Where(x => x.ServiceRequestId == serviceRequestId).OrderByDescending(x => x.CreatedAt).FirstOrDefault());

        public Task AddAsync(ProblemAnalysis problemAnalysis, CancellationToken cancellationToken = default)
        {
            _analyses.Add(problemAnalysis);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
