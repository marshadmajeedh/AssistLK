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
        Assert.Equal("Unclassified", response.Category);
        Assert.Equal(ServiceRequestUrgency.Unknown, response.Urgency);
        Assert.Equal(ServiceRequestStatus.Created, response.Status);
        Assert.NotEqual(Guid.Empty, response.ServiceRequestId);
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
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
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
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
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

        var matching = await repositories.Service.GetReadyForMatchingAsync(response.ServiceRequestId);

        Assert.NotNull(matching);
        Assert.Equal("Engine issue", matching!.ProblemSummary);
        Assert.Equal(ServiceRequestStatus.ReadyForMatching, matching.Status);
        Assert.DoesNotContain("CustomerId", matching.GetType().GetProperties().Select(x => x.Name));
    }

    private static (ServiceRequestService Service, List<ServiceRequest> Requests, List<ProblemAnalysis> Analyses) CreateService()
    {
        var requests = new List<ServiceRequest>();
        var analyses = new List<ProblemAnalysis>();
        return (
            new ServiceRequestService(
                new InMemoryServiceRequestRepository(requests, analyses),
                new InMemoryProblemAnalysisRepository(analyses)),
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

        public Task<ServiceRequest?> GetByIdAndCustomerIdAsync(Guid id, Guid customerId, bool includeProblemAnalyses = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(_requests.SingleOrDefault(x => x.Id == id && x.CustomerId == customerId));

        public Task<IReadOnlyList<ServiceRequest>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ServiceRequest>>(_requests.Where(x => x.CustomerId == customerId).ToArray());

        public Task<IReadOnlyList<ServiceRequest>> GetByStatusAsync(ServiceRequestStatus status, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ServiceRequest>>(_requests.Where(x => x.Status == status).ToArray());

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
