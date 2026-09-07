using AssistLK.Application.Common.Exceptions;
using AssistLK.Application.Interfaces;
using AssistLK.Application.ServiceRequests.DTOs;
using AssistLK.Application.Services;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;

namespace AssistLK.IntegrationTests;

public class Component1LifecycleTests
{
    private (ServiceRequestService Service, List<ServiceRequest> Requests, List<ProblemAnalysis> Analyses) CreateTestContext()
    {
        var requests = new List<ServiceRequest>();
        var analyses = new List<ProblemAnalysis>();

        var reqRepo = new InMemoryServiceRequestRepository(requests, analyses);
        var anaRepo = new InMemoryProblemAnalysisRepository(analyses);

        var service = new ServiceRequestService(reqRepo, anaRepo);
        return (service, requests, analyses);
    }

    [Fact]
    public async Task BeginAnalysisAsync_TransitionsFromCreatedToAnalyzing()
    {
        var (service, requests, _) = CreateTestContext();
        var id = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = Guid.NewGuid(),
            Description = "Leaking pipe",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Created
        });

        var response = await service.BeginAnalysisAsync(id);

        Assert.Equal(ServiceRequestStatus.Analyzing, response.Status);
        Assert.Equal(ServiceRequestStatus.Analyzing, requests.Single().Status);
    }

    [Fact]
    public async Task BeginAnalysisAsync_TransitionsFromAwaitingInformationToAnalyzing()
    {
        var (service, requests, _) = CreateTestContext();
        var id = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = Guid.NewGuid(),
            Description = "Something broken",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.AwaitingInformation
        });

        var response = await service.BeginAnalysisAsync(id);

        Assert.Equal(ServiceRequestStatus.Analyzing, response.Status);
        Assert.Equal(ServiceRequestStatus.Analyzing, requests.Single().Status);
    }

    [Theory]
    [InlineData(ServiceRequestStatus.Analyzing)]
    [InlineData(ServiceRequestStatus.Analyzed)]
    [InlineData(ServiceRequestStatus.ReadyForMatching)]
    [InlineData(ServiceRequestStatus.Cancelled)]
    public async Task BeginAnalysisAsync_ThrowsFromInvalidStatuses(ServiceRequestStatus invalidStatus)
    {
        var (service, requests, _) = CreateTestContext();
        var id = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = Guid.NewGuid(),
            Description = "Test description",
            LocationText = "Colombo",
            Status = invalidStatus
        });

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.BeginAnalysisAsync(id));
    }

    [Fact]
    public async Task ApplyProblemAnalysisResult_TransitionsAnalyzingToAnalyzedWhenComplete()
    {
        var (service, requests, analyses) = CreateTestContext();
        var id = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = Guid.NewGuid(),
            Description = "Kitchen pipe burst and flooded floor",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Analyzing
        });

        var result = await service.ApplyProblemAnalysisResultAsync(new ApplyProblemAnalysisResult
        {
            ServiceRequestId = id,
            Category = "Plumbing",
            DetectedProblem = "Burst pipe flooding kitchen",
            Confidence = 0.9m,
            Urgency = ServiceRequestUrgency.High,
            NeedsMoreInformation = false,
            AgentName = "ProblemUnderstandingAgent"
        });

        Assert.Equal("Plumbing", requests.Single().Category);
        Assert.Equal(ServiceRequestStatus.Analyzed, requests.Single().Status);
        Assert.Equal(ServiceRequestUrgency.High, requests.Single().Urgency);
        Assert.Single(analyses);
    }

    [Fact]
    public async Task ApplyProblemAnalysisResult_TransitionsAnalyzingToAwaitingInformationWhenIncomplete()
    {
        var (service, requests, analyses) = CreateTestContext();
        var id = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = Guid.NewGuid(),
            Description = "Machine broken",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Analyzing
        });

        await service.ApplyProblemAnalysisResultAsync(new ApplyProblemAnalysisResult
        {
            ServiceRequestId = id,
            Category = "Unclassified",
            DetectedProblem = "Insufficient details",
            Confidence = 0.3m,
            Urgency = ServiceRequestUrgency.Unknown,
            NeedsMoreInformation = true,
            AgentName = "ProblemUnderstandingAgent"
        });

        Assert.Equal(ServiceRequestStatus.AwaitingInformation, requests.Single().Status);
        Assert.Single(analyses);
    }

    [Fact]
    public async Task MarkReadyForMatchingAsync_TransitionsAnalyzedToReadyForMatchingWhenValid()
    {
        var (service, requests, analyses) = CreateTestContext();
        var id = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = customerId,
            Description = "Car engine won't start",
            LocationText = "Colombo",
            Category = "Vehicle Repair",
            Urgency = ServiceRequestUrgency.High,
            Status = ServiceRequestStatus.Analyzed
        });

        analyses.Add(new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = id,
            DetectedProblem = "Starter motor or battery fault",
            Confidence = 0.85m,
            AgentName = "ProblemUnderstandingAgent"
        });

        var response = await service.MarkReadyForMatchingAsync(id, customerId);

        Assert.Equal(ServiceRequestStatus.ReadyForMatching, response.Status);
        Assert.Equal(ServiceRequestStatus.ReadyForMatching, requests.Single().Status);
    }

    [Fact]
    public async Task MarkReadyForMatchingAsync_ThrowsKeyNotFound_WhenNotOwned()
    {
        var (service, requests, analyses) = CreateTestContext();
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var otherCustomerId = Guid.NewGuid();

        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = ownerId,
            Description = "Car engine won't start",
            LocationText = "Colombo",
            Category = "Vehicle Repair",
            Urgency = ServiceRequestUrgency.High,
            Status = ServiceRequestStatus.Analyzed
        });

        analyses.Add(new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = id,
            DetectedProblem = "Starter motor or battery fault",
            Confidence = 0.85m,
            AgentName = "ProblemUnderstandingAgent"
        });

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.MarkReadyForMatchingAsync(id, otherCustomerId));
    }

    [Fact]
    public async Task MarkReadyForMatchingAsync_ThrowsWhenCategoryIsUnclassified()
    {
        var (service, requests, analyses) = CreateTestContext();
        var id = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = customerId,
            Description = "Vague description",
            LocationText = "Colombo",
            Category = "Unclassified",
            Urgency = ServiceRequestUrgency.Medium,
            Status = ServiceRequestStatus.Analyzed
        });

        analyses.Add(new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = id,
            DetectedProblem = "Some problem",
            Confidence = 0.7m,
            AgentName = "ProblemUnderstandingAgent"
        });

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.MarkReadyForMatchingAsync(id, customerId));
    }

    [Fact]
    public async Task MarkReadyForMatchingAsync_ThrowsWhenUrgencyIsUnknown()
    {
        var (service, requests, analyses) = CreateTestContext();
        var id = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = customerId,
            Description = "Plumbing issue",
            LocationText = "Colombo",
            Category = "Plumbing",
            Urgency = ServiceRequestUrgency.Unknown,
            Status = ServiceRequestStatus.Analyzed
        });

        analyses.Add(new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = id,
            DetectedProblem = "Plumbing fault",
            Confidence = 0.8m,
            AgentName = "ProblemUnderstandingAgent"
        });

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.MarkReadyForMatchingAsync(id, customerId));
    }

    [Fact]
    public async Task MarkReadyForMatchingAsync_ThrowsWhenNoAnalysisExists()
    {
        var (service, requests, _) = CreateTestContext();
        var id = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = customerId,
            Description = "Electrical issue",
            LocationText = "Colombo",
            Category = "Electrical",
            Urgency = ServiceRequestUrgency.High,
            Status = ServiceRequestStatus.Analyzed
        });

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.MarkReadyForMatchingAsync(id, customerId));
    }

    [Fact]
    public async Task MarkReadyForMatchingAsync_AllowsOptionalCoordinates_WhenLocationTextIsPresent()
    {
        var (service, requests, analyses) = CreateTestContext();
        var id = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = customerId,
            Description = "Car engine won't start",
            LocationText = "Kandy, Central Province",
            Latitude = null,  // Coordinates are optional
            Longitude = null, // Coordinates are optional
            Category = "Vehicle Repair",
            Urgency = ServiceRequestUrgency.High,
            Status = ServiceRequestStatus.Analyzed
        });

        analyses.Add(new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = id,
            DetectedProblem = "Battery discharged",
            Confidence = 0.8m,
            AgentName = "ProblemUnderstandingAgent"
        });

        var response = await service.MarkReadyForMatchingAsync(id, customerId);

        Assert.Equal(ServiceRequestStatus.ReadyForMatching, response.Status);
        Assert.Null(response.Latitude);
        Assert.Null(response.Longitude);
        Assert.Equal("Kandy, Central Province", response.LocationText);
    }

    [Fact]
    public async Task MarkReadyForMatchingAsync_ThrowsWhenLocationTextIsMissing()
    {
        var (service, requests, analyses) = CreateTestContext();
        var id = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = customerId,
            Description = "Car engine won't start",
            LocationText = "", // Missing!
            Category = "Vehicle Repair",
            Urgency = ServiceRequestUrgency.High,
            Status = ServiceRequestStatus.Analyzed
        });

        analyses.Add(new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = id,
            DetectedProblem = "Battery discharged",
            Confidence = 0.8m,
            AgentName = "ProblemUnderstandingAgent"
        });

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.MarkReadyForMatchingAsync(id, customerId));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(1.2)]
    public async Task MarkReadyForMatchingAsync_ThrowsWhenLatestProblemAnalysisHasInvalidConfidence(decimal invalidConfidence)
    {
        var (service, requests, analyses) = CreateTestContext();
        var id = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        requests.Add(new ServiceRequest
        {
            Id = id,
            CustomerId = customerId,
            Description = "Car engine won't start",
            LocationText = "Colombo",
            Category = "Vehicle Repair",
            Urgency = ServiceRequestUrgency.High,
            Status = ServiceRequestStatus.Analyzed
        });

        analyses.Add(new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = id,
            DetectedProblem = "Battery discharged",
            Confidence = invalidConfidence,
            AgentName = "ProblemUnderstandingAgent"
        });

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.MarkReadyForMatchingAsync(id, customerId));
    }

    // In-memory test doubles
    private sealed class InMemoryServiceRequestRepository : IServiceRequestRepository
    {
        private readonly List<ServiceRequest> _requests;
        private readonly List<ProblemAnalysis> _analyses;

        public InMemoryServiceRequestRepository(List<ServiceRequest> requests, List<ProblemAnalysis> analyses)
        {
            _requests = requests;
            _analyses = analyses;
        }

        public Task<ServiceRequest?> GetByIdAsync(Guid id, bool includeProblemAnalyses = false, CancellationToken cancellationToken = default)
        {
            var req = _requests.SingleOrDefault(x => x.Id == id);
            if (req != null && includeProblemAnalyses)
            {
                req.ProblemAnalyses = _analyses.Where(a => a.ServiceRequestId == id).ToList();
            }
            return Task.FromResult(req);
        }

        public Task<ServiceRequest?> GetByIdAndCustomerIdAsync(Guid id, Guid customerId, bool includeProblemAnalyses = false, CancellationToken cancellationToken = default)
        {
            var req = _requests.SingleOrDefault(x => x.Id == id && x.CustomerId == customerId);
            if (req != null && includeProblemAnalyses)
            {
                req.ProblemAnalyses = _analyses.Where(a => a.ServiceRequestId == id).ToList();
            }
            return Task.FromResult(req);
        }

        public Task<IReadOnlyList<ServiceRequest>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ServiceRequest>>(_requests.Where(x => x.CustomerId == customerId).ToArray());

        public Task<IReadOnlyList<ServiceRequest>> GetByStatusAsync(ServiceRequestStatus status, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ServiceRequest>>(_requests.Where(x => x.Status == status).ToArray());

        public Task AddAsync(ServiceRequest serviceRequest, CancellationToken cancellationToken = default)
        {
            _requests.Add(serviceRequest);
            return Task.CompletedTask;
        }

        public void Update(ServiceRequest serviceRequest) { }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryProblemAnalysisRepository : IProblemAnalysisRepository
    {
        private readonly List<ProblemAnalysis> _analyses;

        public InMemoryProblemAnalysisRepository(List<ProblemAnalysis> analyses)
        {
            _analyses = analyses;
        }

        public Task<IReadOnlyList<ProblemAnalysis>> GetByServiceRequestIdAsync(Guid serviceRequestId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProblemAnalysis>>(_analyses.Where(a => a.ServiceRequestId == serviceRequestId).ToArray());

        public Task<ProblemAnalysis?> GetMostRecentByServiceRequestIdAsync(Guid serviceRequestId, CancellationToken cancellationToken = default)
            => Task.FromResult(_analyses.Where(a => a.ServiceRequestId == serviceRequestId).OrderByDescending(a => a.CreatedAt).FirstOrDefault());

        public Task AddAsync(ProblemAnalysis problemAnalysis, CancellationToken cancellationToken = default)
        {
            _analyses.Add(problemAnalysis);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
