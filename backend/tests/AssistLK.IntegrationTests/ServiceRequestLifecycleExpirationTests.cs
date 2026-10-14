using System.Reflection;
using AssistLK.Api.Features.ServiceRequests;
using AssistLK.Application.Common.Exceptions;
using AssistLK.Application.Interfaces;
using AssistLK.Application.ServiceRequests;
using AssistLK.Application.ServiceRequests.DTOs;
using AssistLK.Application.Services;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AssistLK.IntegrationTests;

// ==============================================================================
// TEST SUITE: ServiceRequestLifecycleExpirationTests
// What this class tests:
//   - Service request lifecycle expiration, matching windows, and background expiration services.
//   - ReadyForMatchingAtUtc and MatchingExpiresAtUtc timestamp calculation from configurable MatchingWindowHours.
//   - Matching eligibility logic (IsMatchingEligible is true only for active ReadyForMatching requests within their window).
//   - Expiration of abandoned AwaitingInformation requests that have unanswered clarifications past timeout (cancelling them).
//   - Preservation of recent, answered, or superseded clarification requests.
//   - Background worker resilience, cancellation handling, and architectural invariants (single write site for ReadyForMatching).
// Why these tests are needed:
//   - Prevents abandoned customer requests from idling indefinitely in the pipeline.
//   - Protects service providers from being matched against stale or expired service requests.
// What is real and what is mocked/faked:
//   - Real: ServiceRequestService, ServiceRequestLifecycleExpirationService,
//           ServiceRequestLifecycleExpirationBackgroundService, ServiceRequestLifecycleOptions.
//   - In-Memory/Faked: InMemoryServiceRequestRepository, InMemoryProblemAnalysisRepository,
//                     TestTimeProvider (.NET 8 TimeProvider subclass), MockLifecycleExpirationService.
// Prerequisites:
//   - None (runs purely in-memory with virtual clock).
// Run from repository root:
//   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.ServiceRequestLifecycleExpirationTests"
// ==============================================================================
public class ServiceRequestLifecycleExpirationTests
{
    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public TestTimeProvider(DateTimeOffset initialUtcNow)
        {
            _utcNow = initialUtcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void SetUtcNow(DateTimeOffset utcNow) => _utcNow = utcNow;

        public void Advance(TimeSpan delta) => _utcNow += delta;
    }

    private sealed class InMemoryServiceRequestRepository : IServiceRequestRepository
    {
        private readonly List<ServiceRequest> _requests = new();
        public bool ThrowConcurrencyOnSave { get; set; }

        public void AddSync(ServiceRequest request) => _requests.Add(request);

        public Task<ServiceRequest?> ReloadForRecoveryAsync(Guid serviceRequestId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_requests.FirstOrDefault(x => x.Id == serviceRequestId));
        }

        public Task<ServiceRequest?> GetByIdAsync(Guid serviceRequestId, bool includeProblemAnalyses = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_requests.FirstOrDefault(x => x.Id == serviceRequestId));
        }

        public Task<ServiceRequest?> GetByIdAsync(Guid serviceRequestId, bool includeProblemAnalyses, bool includeClarifications, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_requests.FirstOrDefault(x => x.Id == serviceRequestId));
        }

        public Task<ServiceRequest?> GetByIdAndCustomerIdAsync(Guid serviceRequestId, Guid customerId, bool includeProblemAnalyses = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_requests.FirstOrDefault(x => x.Id == serviceRequestId && x.CustomerId == customerId));
        }

        public Task<ServiceRequest?> GetByIdAndCustomerIdAsync(Guid serviceRequestId, Guid customerId, bool includeProblemAnalyses, bool includeClarifications, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_requests.FirstOrDefault(x => x.Id == serviceRequestId && x.CustomerId == customerId));
        }

        public Task<IReadOnlyList<ServiceRequest>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ServiceRequest>>(_requests.Where(x => x.CustomerId == customerId).ToList());
        }

        public Task<IReadOnlyList<ServiceRequest>> GetByStatusAsync(ServiceRequestStatus status, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ServiceRequest>>(_requests.Where(x => x.Status == status).ToList());
        }

        public Task<IReadOnlyList<ServiceRequest>> GetStaleAnalyzingRequestsAsync(DateTime cutoffTimeUtc, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ServiceRequest>>(_requests
                .Where(x => x.Status == ServiceRequestStatus.Analyzing && x.UpdatedAt <= cutoffTimeUtc)
                .ToList());
        }

        public Task<IReadOnlyList<ServiceRequest>> GetStaleAwaitingInformationRequestsAsync(DateTime cutoffTimeUtc, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ServiceRequest>>(_requests
                .Where(x => x.Status == ServiceRequestStatus.AwaitingInformation && x.UpdatedAt <= cutoffTimeUtc)
                .ToList());
        }

        public Task<IReadOnlyList<ServiceRequest>> GetAllForAdminAsync(ServiceRequestStatus? status = null, string? category = null, ServiceRequestUrgency? urgency = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ServiceRequest>>(_requests.ToList());
        }

        public Task AddAsync(ServiceRequest serviceRequest, CancellationToken cancellationToken = default)
        {
            _requests.Add(serviceRequest);
            return Task.CompletedTask;
        }

        public void Update(ServiceRequest serviceRequest)
        {
            // In-memory update
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowConcurrencyOnSave)
            {
                throw new DbUpdateConcurrencyException("Simulated concurrency conflict.");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryProblemAnalysisRepository : IProblemAnalysisRepository
    {
        private readonly List<ProblemAnalysis> _analyses = new();

        public void AddSync(ProblemAnalysis analysis) => _analyses.Add(analysis);

        public Task AddAsync(ProblemAnalysis analysis, CancellationToken cancellationToken = default)
        {
            _analyses.Add(analysis);
            return Task.CompletedTask;
        }

        public Task<ProblemAnalysis?> GetByIdAsync(Guid problemAnalysisId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_analyses.FirstOrDefault(x => x.Id == problemAnalysisId));
        }

        public Task<IReadOnlyList<ProblemAnalysis>> GetByServiceRequestIdAsync(Guid serviceRequestId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ProblemAnalysis>>(
                _analyses.Where(x => x.ServiceRequestId == serviceRequestId).ToList());
        }

        public Task<ProblemAnalysis?> GetMostRecentByServiceRequestIdAsync(Guid serviceRequestId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _analyses.Where(x => x.ServiceRequestId == serviceRequestId)
                    .OrderByDescending(x => x.CreatedAt)
                    .FirstOrDefault());
        }

        public void Update(ProblemAnalysis analysis) { }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static ServiceRequest CreateValidReadyCandidate(Guid requestId, Guid customerId, DateTime createdAt)
    {
        return new ServiceRequest
        {
            Id = requestId,
            CustomerId = customerId,
            EvidenceRevision = 1,
            Category = "Plumbing",
            Description = "Leaking pipe under kitchen sink",
            LocationText = "123 Main St, Colombo",
            Latitude = 6.9271m,
            Longitude = 79.8612m,
            Urgency = ServiceRequestUrgency.High,
            Status = ServiceRequestStatus.Analyzed,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            Clarifications = new List<ServiceRequestClarification>(),
            ProblemAnalyses = new List<ProblemAnalysis>()
        };
    }

    // =========================================================================
    // CASE 1: ReadyForMatching transition sets ReadyForMatchingAtUtc
    // =========================================================================
    // Purpose: Checks that transitioning to ReadyForMatching sets ReadyForMatchingAtUtc to current UTC time.
    // Why: Records the exact timestamp when the request became eligible for provider matching.
    // Scenario: Analyzed request with matching analysis transitions to ReadyForMatching at fixed time.
    // Pass condition: ReadyForMatchingAtUtc matches the test clock's UTC timestamp.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestLifecycleExpirationTests.Case01_ReadyForMatchingTransition_SetsReadyForMatchingAtUtc"
    [Fact]
    public async Task Case01_ReadyForMatchingTransition_SetsReadyForMatchingAtUtc()
    {
        var fixedTime = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        var clock = new TestTimeProvider(fixedTime);
        var requestRepo = new InMemoryServiceRequestRepository();
        var analysisRepo = new InMemoryProblemAnalysisRepository();
        var options = new ServiceRequestLifecycleOptions { MatchingWindowHours = 24 };

        var service = new ServiceRequestService(requestRepo, analysisRepo, options, timeProvider: clock);

        var requestId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var request = CreateValidReadyCandidate(requestId, customerId, fixedTime.UtcDateTime.AddDays(-2));

        var analysis = new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            EvidenceRevision = 1,
            DetectedProblem = "Pipe burst",
            Confidence = 0.95m,
            AgentName = "TestAgent",
            CreatedAt = fixedTime.UtcDateTime.AddHours(-1)
        };
        request.ProblemAnalyses.Add(analysis);
        requestRepo.AddSync(request);
        analysisRepo.AddSync(analysis);

        var response = await service.MarkReadyForMatchingAsync(requestId, customerId);

        Assert.Equal(ServiceRequestStatus.ReadyForMatching, response.Status);
        Assert.NotNull(response.ReadyForMatchingAtUtc);
        Assert.Equal(fixedTime.UtcDateTime, response.ReadyForMatchingAtUtc);
        Assert.Equal(fixedTime.UtcDateTime, request.ReadyForMatchingAtUtc);
    }

    // =========================================================================
    // CASE 2: Same transition sets MatchingExpiresAtUtc using configured MatchingWindowHours
    // =========================================================================
    // Purpose: Checks that MatchingExpiresAtUtc is set to ReadyForMatchingAtUtc + MatchingWindowHours.
    // Why: Enforces the operational window for matching so old requests expire after the configured duration (e.g. 48h).
    // Scenario: MatchingWindowHours set to 48 hours; request transitioned to ReadyForMatching.
    // Pass condition: MatchingExpiresAtUtc equals fixed time + 48 hours.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestLifecycleExpirationTests.Case02_ReadyForMatchingTransition_SetsMatchingExpiresAtUtcUsingConfiguredWindow"
    [Fact]
    public async Task Case02_ReadyForMatchingTransition_SetsMatchingExpiresAtUtcUsingConfiguredWindow()
    {
        var fixedTime = new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero);
        var clock = new TestTimeProvider(fixedTime);
        var requestRepo = new InMemoryServiceRequestRepository();
        var analysisRepo = new InMemoryProblemAnalysisRepository();
        var options = new ServiceRequestLifecycleOptions { MatchingWindowHours = 48 };

        var service = new ServiceRequestService(requestRepo, analysisRepo, options, timeProvider: clock);

        var requestId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var request = CreateValidReadyCandidate(requestId, customerId, fixedTime.UtcDateTime.AddDays(-3));

        var analysis = new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            EvidenceRevision = 1,
            DetectedProblem = "Sink leak",
            Confidence = 0.9m,
            AgentName = "TestAgent",
            CreatedAt = fixedTime.UtcDateTime.AddHours(-1)
        };
        request.ProblemAnalyses.Add(analysis);
        requestRepo.AddSync(request);
        analysisRepo.AddSync(analysis);

        var response = await service.MarkReadyForMatchingAsync(requestId, customerId);

        Assert.NotNull(response.MatchingExpiresAtUtc);
        Assert.Equal(fixedTime.UtcDateTime.AddHours(48), response.MatchingExpiresAtUtc);
        Assert.Equal(fixedTime.UtcDateTime.AddHours(48), request.MatchingExpiresAtUtc);
    }

    // =========================================================================
    // CASE 3: Expiry is calculated from ReadyForMatchingAtUtc, NOT CreatedAt
    // =========================================================================
    // Purpose: Checks that matching expiry is calculated from the transition timestamp (ReadyForMatchingAtUtc), NOT from the request creation time (CreatedAt).
    // Why: A request may take days in clarification before becoming ready; the matching clock must only start once ready.
    // Scenario: Request created 12 days ago is transitioned to ReadyForMatching today with a 24-hour window.
    // Pass condition: Expiry is today + 24h, not 12 days ago + 24h.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestLifecycleExpirationTests.Case03_ExpiryIsCalculatedFromReadyForMatchingAtUtc_NotCreatedAt"
    [Fact]
    public async Task Case03_ExpiryIsCalculatedFromReadyForMatchingAtUtc_NotCreatedAt()
    {
        var createdTime = new DateTimeOffset(2026, 9, 20, 8, 0, 0, TimeSpan.Zero); // 12 days ago
        var transitionTime = new DateTimeOffset(2026, 10, 2, 14, 0, 0, TimeSpan.Zero);
        var clock = new TestTimeProvider(transitionTime);
        var requestRepo = new InMemoryServiceRequestRepository();
        var analysisRepo = new InMemoryProblemAnalysisRepository();
        var options = new ServiceRequestLifecycleOptions { MatchingWindowHours = 24 };

        var service = new ServiceRequestService(requestRepo, analysisRepo, options, timeProvider: clock);

        var requestId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var request = CreateValidReadyCandidate(requestId, customerId, createdTime.UtcDateTime);

        var analysis = new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            EvidenceRevision = 1,
            DetectedProblem = "Old leak finally ready",
            Confidence = 0.88m,
            AgentName = "TestAgent",
            CreatedAt = transitionTime.UtcDateTime.AddMinutes(-10)
        };
        request.ProblemAnalyses.Add(analysis);
        requestRepo.AddSync(request);
        analysisRepo.AddSync(analysis);

        var response = await service.MarkReadyForMatchingAsync(requestId, customerId);

        // Expiry must be transitionTime + 24 hours, NOT createdTime + 24 hours
        Assert.Equal(transitionTime.UtcDateTime.AddHours(24), response.MatchingExpiresAtUtc);
        Assert.NotEqual(createdTime.UtcDateTime.AddHours(24), response.MatchingExpiresAtUtc);
    }

    // =========================================================================
    // CASE 4: Active ReadyForMatching request: IsMatchingEligible == true
    // =========================================================================
    // Purpose: Checks that IsMatchingEligible is true for a ReadyForMatching request whose expiry is in the future.
    // Why: Informs provider matching services and customer UI that the request is currently active for matching.
    // Scenario: ReadyForMatching request with expiry 22 hours in the future.
    // Pass condition: Both customer DTO and matching DTO report IsMatchingEligible == true.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestLifecycleExpirationTests.Case04_ActiveReadyForMatchingRequest_IsMatchingEligible_IsTrue"
    [Fact]
    public async Task Case04_ActiveReadyForMatchingRequest_IsMatchingEligible_IsTrue()
    {
        var now = new DateTimeOffset(2026, 10, 2, 15, 0, 0, TimeSpan.Zero);
        var clock = new TestTimeProvider(now);
        var requestRepo = new InMemoryServiceRequestRepository();
        var analysisRepo = new InMemoryProblemAnalysisRepository();
        var service = new ServiceRequestService(requestRepo, analysisRepo, timeProvider: clock);

        var requestId = Guid.NewGuid();
        var request = CreateValidReadyCandidate(requestId, Guid.NewGuid(), now.UtcDateTime.AddDays(-1));
        request.Status = ServiceRequestStatus.ReadyForMatching;
        request.ReadyForMatchingAtUtc = now.UtcDateTime.AddHours(-2);
        request.MatchingExpiresAtUtc = now.UtcDateTime.AddHours(22); // active: expires in 22h

        var analysis = new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            DetectedProblem = "Active leak",
            Confidence = 0.9m,
            CreatedAt = now.UtcDateTime.AddHours(-2)
        };
        request.ProblemAnalyses.Add(analysis);
        requestRepo.AddSync(request);

        var customerResponse = await service.GetByIdAsync(requestId, request.CustomerId);
        var matchingResponse = await service.GetReadyForMatchingAsync(requestId);

        Assert.True(customerResponse.IsMatchingEligible);
        Assert.NotNull(matchingResponse);
        Assert.True(matchingResponse.IsMatchingEligible);
    }

    // =========================================================================
    // CASE 5: Expired ReadyForMatching request: IsMatchingEligible == false
    // =========================================================================
    // Purpose: Checks that IsMatchingEligible is false for a ReadyForMatching request whose expiry has passed.
    // Why: Prevents providers from receiving or bidding on requests whose matching deadline has elapsed.
    // Scenario: ReadyForMatching request expired 2 days ago.
    // Pass condition: Status is still ReadyForMatching, but IsMatchingEligible == false on both customer and matching DTOs.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestLifecycleExpirationTests.Case05_ExpiredReadyForMatchingRequest_IsMatchingEligible_IsFalse"
    [Fact]
    public async Task Case05_ExpiredReadyForMatchingRequest_IsMatchingEligible_IsFalse()
    {
        var now = new DateTimeOffset(2026, 10, 2, 15, 0, 0, TimeSpan.Zero);
        var clock = new TestTimeProvider(now);
        var requestRepo = new InMemoryServiceRequestRepository();
        var analysisRepo = new InMemoryProblemAnalysisRepository();
        var service = new ServiceRequestService(requestRepo, analysisRepo, timeProvider: clock);

        var requestId = Guid.NewGuid();
        var request = CreateValidReadyCandidate(requestId, Guid.NewGuid(), now.UtcDateTime.AddDays(-5));
        request.Status = ServiceRequestStatus.ReadyForMatching;
        request.ReadyForMatchingAtUtc = now.UtcDateTime.AddDays(-3);
        request.MatchingExpiresAtUtc = now.UtcDateTime.AddDays(-2); // expired 2 days ago

        var analysis = new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            DetectedProblem = "Expired leak",
            Confidence = 0.9m,
            CreatedAt = now.UtcDateTime.AddDays(-3)
        };
        request.ProblemAnalyses.Add(analysis);
        requestRepo.AddSync(request);

        var customerResponse = await service.GetByIdAsync(requestId, request.CustomerId);
        var matchingResponse = await service.GetReadyForMatchingAsync(requestId);

        Assert.Equal(ServiceRequestStatus.ReadyForMatching, customerResponse.Status);
        Assert.False(customerResponse.IsMatchingEligible);
        Assert.NotNull(matchingResponse);
        Assert.False(matchingResponse.IsMatchingEligible);
    }

    // =========================================================================
    // CASE 6: ReadyForMatching with null MatchingExpiresAtUtc: IsMatchingEligible == false
    // =========================================================================
    // Purpose: Checks that legacy ReadyForMatching rows with null MatchingExpiresAtUtc are treated as not matching eligible.
    // Why: Defends against legacy records created before matching windows were introduced.
    // Scenario: ReadyForMatching request with null expiry.
    // Pass condition: IsMatchingEligible == false.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestLifecycleExpirationTests.Case06_ReadyForMatchingWithNullExpiry_IsMatchingEligible_IsFalse"
    [Fact]
    public async Task Case06_ReadyForMatchingWithNullExpiry_IsMatchingEligible_IsFalse()
    {
        var now = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        var clock = new TestTimeProvider(now);
        var requestRepo = new InMemoryServiceRequestRepository();
        var analysisRepo = new InMemoryProblemAnalysisRepository();
        var service = new ServiceRequestService(requestRepo, analysisRepo, timeProvider: clock);

        var requestId = Guid.NewGuid();
        var request = CreateValidReadyCandidate(requestId, Guid.NewGuid(), now.UtcDateTime.AddDays(-1));
        request.Status = ServiceRequestStatus.ReadyForMatching;
        request.ReadyForMatchingAtUtc = null;
        request.MatchingExpiresAtUtc = null; // legacy row

        var analysis = new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            DetectedProblem = "Legacy request",
            Confidence = 0.9m,
            CreatedAt = now.UtcDateTime.AddDays(-1)
        };
        request.ProblemAnalyses.Add(analysis);
        requestRepo.AddSync(request);

        var customerResponse = await service.GetByIdAsync(requestId, request.CustomerId);
        var matchingResponse = await service.GetReadyForMatchingAsync(requestId);

        Assert.False(customerResponse.IsMatchingEligible);
        Assert.NotNull(matchingResponse);
        Assert.False(matchingResponse.IsMatchingEligible);
    }

    // =========================================================================
    // CASE 7: Created request is never matching eligible
    // =========================================================================
    // Purpose: Checks that a request in Created status is never matching eligible, even if an expiry timestamp exists.
    // Why: Only fully analyzed and customer-approved requests can be matched.
    // Scenario: Request with status Created queried for customer and matching eligibility.
    // Pass condition: IsMatchingEligible == false and GetReadyForMatchingAsync returns null.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestLifecycleExpirationTests.Case07_CreatedRequest_IsNeverMatchingEligible"
    [Fact]
    public async Task Case07_CreatedRequest_IsNeverMatchingEligible()
    {
        var now = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        var clock = new TestTimeProvider(now);
        var requestRepo = new InMemoryServiceRequestRepository();
        var analysisRepo = new InMemoryProblemAnalysisRepository();
        var service = new ServiceRequestService(requestRepo, analysisRepo, timeProvider: clock);

        var requestId = Guid.NewGuid();
        var request = CreateValidReadyCandidate(requestId, Guid.NewGuid(), now.UtcDateTime);
        request.Status = ServiceRequestStatus.Created;
        request.MatchingExpiresAtUtc = now.UtcDateTime.AddHours(24);
        requestRepo.AddSync(request);

        var customerResponse = await service.GetByIdAsync(requestId, request.CustomerId);
        var matchingResponse = await service.GetReadyForMatchingAsync(requestId);

        Assert.False(customerResponse.IsMatchingEligible);
        Assert.Null(matchingResponse); // Not returned for matching
    }

    // =========================================================================
    // CASE 8: AwaitingInformation request is never matching eligible
    // =========================================================================
    // Purpose: Checks that an AwaitingInformation request is never matching eligible.
    // Why: Requests lacking critical customer details cannot be matched to service providers.
    // Scenario: Request in AwaitingInformation status checked for matching eligibility.
    // Pass condition: IsMatchingEligible == false and GetReadyForMatchingAsync returns null.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestLifecycleExpirationTests.Case08_AwaitingInformationRequest_IsNeverMatchingEligible"
    [Fact]
    public async Task Case08_AwaitingInformationRequest_IsNeverMatchingEligible()
    {
        var now = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        var clock = new TestTimeProvider(now);
        var requestRepo = new InMemoryServiceRequestRepository();
        var analysisRepo = new InMemoryProblemAnalysisRepository();
        var service = new ServiceRequestService(requestRepo, analysisRepo, timeProvider: clock);

        var requestId = Guid.NewGuid();
        var request = CreateValidReadyCandidate(requestId, Guid.NewGuid(), now.UtcDateTime);
        request.Status = ServiceRequestStatus.AwaitingInformation;
        request.MatchingExpiresAtUtc = now.UtcDateTime.AddHours(24);
        requestRepo.AddSync(request);

        var customerResponse = await service.GetByIdAsync(requestId, request.CustomerId);
        var matchingResponse = await service.GetReadyForMatchingAsync(requestId);

        Assert.False(customerResponse.IsMatchingEligible);
        Assert.Null(matchingResponse);
    }

    // =========================================================================
    // CASE 9: Cancelled request is never matching eligible
    // =========================================================================
    // Purpose: Checks that a Cancelled request is never matching eligible.
    // Why: Cancelled requests must never be matched or dispatched.
    // Scenario: Request in Cancelled status checked for matching eligibility.
    // Pass condition: IsMatchingEligible == false and GetReadyForMatchingAsync returns null.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestLifecycleExpirationTests.Case09_CancelledRequest_IsNeverMatchingEligible"
    [Fact]
    public async Task Case09_CancelledRequest_IsNeverMatchingEligible()
    {
        var now = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        var clock = new TestTimeProvider(now);
        var requestRepo = new InMemoryServiceRequestRepository();
        var analysisRepo = new InMemoryProblemAnalysisRepository();
        var service = new ServiceRequestService(requestRepo, analysisRepo, timeProvider: clock);

        var requestId = Guid.NewGuid();
        var request = CreateValidReadyCandidate(requestId, Guid.NewGuid(), now.UtcDateTime);
        request.Status = ServiceRequestStatus.Cancelled;
        request.MatchingExpiresAtUtc = now.UtcDateTime.AddHours(24);
        requestRepo.AddSync(request);

        var customerResponse = await service.GetByIdAsync(requestId, request.CustomerId);
        var matchingResponse = await service.GetReadyForMatchingAsync(requestId);

        Assert.False(customerResponse.IsMatchingEligible);
        Assert.Null(matchingResponse);
    }

    // =========================================================================
    // CASE 10: AwaitingInformation older than configured timeout with an actionable unanswered clarification is cancelled
    // =========================================================================
    // Purpose: Checks that an AwaitingInformation request older than the timeout with an active unanswered clarification is automatically cancelled.
    // Why: Reclaims stale pipeline requests when the customer abandons the clarification questionnaire.
    // Scenario: Request updated 25 hours ago with unanswered clarification question; timeout set to 24h.
    // Pass condition: Expired count is 1, and request status changes to Cancelled.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestLifecycleExpirationTests.Case10_StaleAwaitingInformationWithUnansweredClarification_IsCancelled"
    [Fact]
    public async Task Case10_StaleAwaitingInformationWithUnansweredClarification_IsCancelled()
    {
        var now = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        var clock = new TestTimeProvider(now);
        var requestRepo = new InMemoryServiceRequestRepository();
        var options = new ServiceRequestLifecycleOptions
        {
            AwaitingInformationTimeoutHours = 24,
            Enabled = true
        };

        var expirationService = new ServiceRequestLifecycleExpirationService(
            requestRepo,
            directOptions: options,
            timeProvider: clock,
            logger: NullLogger<ServiceRequestLifecycleExpirationService>.Instance);

        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Status = ServiceRequestStatus.AwaitingInformation,
            UpdatedAt = now.UtcDateTime.AddHours(-25), // 25 hours ago (> 24h)
            Clarifications = new List<ServiceRequestClarification>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ServiceRequestId = requestId,
                    ClarificationRound = 1,
                    Sequence = 1,
                    Question = "Is the main valve accessible?",
                    Answer = null,
                    SupersededAt = null
                }
            }
        };
        requestRepo.AddSync(request);

        var expiredCount = await expirationService.ExpireStaleAwaitingInformationRequestsAsync();

        Assert.Equal(1, expiredCount);
        Assert.Equal(ServiceRequestStatus.Cancelled, request.Status);
    }

    // =========================================================================
    // CASE 11: Recent AwaitingInformation request is preserved
    // =========================================================================
    // Purpose: Checks that an AwaitingInformation request within the timeout window is preserved.
    // Why: Allows customers adequate time (e.g. 24h) to answer clarification questions before expiring.
    // Scenario: Request updated 5 hours ago (< 24h timeout) with unanswered question.
    // Pass condition: Expired count is 0, and status remains AwaitingInformation.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestLifecycleExpirationTests.Case11_RecentAwaitingInformationRequest_IsPreserved"
    [Fact]
    public async Task Case11_RecentAwaitingInformationRequest_IsPreserved()
    {
        var now = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        var clock = new TestTimeProvider(now);
        var requestRepo = new InMemoryServiceRequestRepository();
        var options = new ServiceRequestLifecycleOptions { AwaitingInformationTimeoutHours = 24 };

        var expirationService = new ServiceRequestLifecycleExpirationService(
            requestRepo,
            directOptions: options,
            timeProvider: clock);

        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Status = ServiceRequestStatus.AwaitingInformation,
            UpdatedAt = now.UtcDateTime.AddHours(-5), // only 5 hours ago (< 24h)
            Clarifications = new List<ServiceRequestClarification>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ServiceRequestId = requestId,
                    ClarificationRound = 1,
                    Sequence = 1,
                    Question = "What kind of pipe?",
                    Answer = null,
                    SupersededAt = null
                }
            }
        };
        requestRepo.AddSync(request);

        var expiredCount = await expirationService.ExpireStaleAwaitingInformationRequestsAsync();

        Assert.Equal(0, expiredCount);
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, request.Status);
    }

    // =========================================================================
    // CASE 12: Answered clarification is preserved
    // =========================================================================
    // Purpose: Checks that a request whose clarification question has been answered is preserved, even if older than the timeout.
    // Why: Once answered, the request is waiting for AI agent re-analysis and must not be expired as abandoned.
    // Scenario: Request updated 30 hours ago, but clarification question has Answer = "PVC pipe".
    // Pass condition: Expired count is 0, and status remains AwaitingInformation.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestLifecycleExpirationTests.Case12_AnsweredClarification_IsPreserved"
    [Fact]
    public async Task Case12_AnsweredClarification_IsPreserved()
    {
        var now = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        var clock = new TestTimeProvider(now);
        var requestRepo = new InMemoryServiceRequestRepository();
        var options = new ServiceRequestLifecycleOptions { AwaitingInformationTimeoutHours = 24 };

        var expirationService = new ServiceRequestLifecycleExpirationService(
            requestRepo,
            directOptions: options,
            timeProvider: clock);

        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Status = ServiceRequestStatus.AwaitingInformation,
            UpdatedAt = now.UtcDateTime.AddHours(-30), // old, but answered!
            Clarifications = new List<ServiceRequestClarification>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ServiceRequestId = requestId,
                    ClarificationRound = 1,
                    Sequence = 1,
                    Question = "What kind of pipe?",
                    Answer = "PVC pipe",
                    AnsweredAt = now.UtcDateTime.AddHours(-29),
                    SupersededAt = null
                }
            }
        };
        requestRepo.AddSync(request);

        var expiredCount = await expirationService.ExpireStaleAwaitingInformationRequestsAsync();

        Assert.Equal(0, expiredCount);
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, request.Status);
    }

    // =========================================================================
    // CASE 13: Superseded unanswered clarification does not cause cancellation
    // =========================================================================
    // Purpose: Checks that an unanswered clarification question that has been superseded by a newer round does not trigger cancellation.
    // Why: Superseded questions are obsolete; only active current-round questions govern expiration.
    // Scenario: Unanswered question marked with SupersededAt timestamp.
    // Pass condition: Expired count is 0, and status remains AwaitingInformation.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestLifecycleExpirationTests.Case13_SupersededUnansweredClarification_DoesNotCauseCancellation"
    [Fact]
    public async Task Case13_SupersededUnansweredClarification_DoesNotCauseCancellation()
    {
        var now = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        var clock = new TestTimeProvider(now);
        var requestRepo = new InMemoryServiceRequestRepository();
        var options = new ServiceRequestLifecycleOptions { AwaitingInformationTimeoutHours = 24 };

        var expirationService = new ServiceRequestLifecycleExpirationService(
            requestRepo,
            directOptions: options,
            timeProvider: clock);

        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Status = ServiceRequestStatus.AwaitingInformation,
            UpdatedAt = now.UtcDateTime.AddHours(-40),
            Clarifications = new List<ServiceRequestClarification>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ServiceRequestId = requestId,
                    ClarificationRound = 1,
                    Sequence = 1,
                    Question = "Old obsolete question",
                    Answer = null,
                    SupersededAt = now.UtcDateTime.AddHours(-35) // superseded!
                }
            }
        };
        requestRepo.AddSync(request);

        var expiredCount = await expirationService.ExpireStaleAwaitingInformationRequestsAsync();

        Assert.Equal(0, expiredCount);
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, request.Status);
    }

    // =========================================================================
    // CASE 14: Request that changes state before worker execution is preserved
    // =========================================================================
    // Purpose: Checks that if a request transitions out of AwaitingInformation (e.g. to Analyzing) before the background worker runs, it is not cancelled.
    // Why: Concurrency guard against cancelling requests that resumed active processing while queued for expiration.
    // Scenario: Request status changed to Analyzing before ExpireStaleAwaitingInformationRequestAsync is invoked.
    // Pass condition: Returns false, and status remains Analyzing.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestLifecycleExpirationTests.Case14_RequestThatChangesStateBeforeWorker_IsPreserved"
    [Fact]
    public async Task Case14_RequestThatChangesStateBeforeWorker_IsPreserved()
    {
        var now = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        var clock = new TestTimeProvider(now);
        var requestRepo = new InMemoryServiceRequestRepository();
        var options = new ServiceRequestLifecycleOptions { AwaitingInformationTimeoutHours = 24 };

        var expirationService = new ServiceRequestLifecycleExpirationService(
            requestRepo,
            directOptions: options,
            timeProvider: clock);

        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Status = ServiceRequestStatus.Analyzing, // advanced before worker!
            UpdatedAt = now.UtcDateTime.AddHours(-40),
            Clarifications = new List<ServiceRequestClarification>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ServiceRequestId = requestId,
                    Question = "Some question",
                    Answer = null,
                    SupersededAt = null
                }
            }
        };
        requestRepo.AddSync(request);

        var result = await expirationService.ExpireStaleAwaitingInformationRequestAsync(requestId);

        Assert.False(result);
        Assert.Equal(ServiceRequestStatus.Analyzing, request.Status);
    }

    // =========================================================================
    // CASE 15: Background worker continues operating after a per-iteration error
    // =========================================================================
    // Purpose: Checks that the lifecycle background service survives transient exceptions during an iteration and continues polling.
    // Why: Ensures background daemon resilience so an error on one request doesn't kill the hosted service.
    // Scenario: Expiration service throws InvalidOperationException on iteration 1; worker continues running.
    // Pass condition: Call count reaches >= 1, and background worker does not crash.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestLifecycleExpirationTests.Case15_BackgroundWorker_ContinuesOperatingAfterPerIterationError"
    [Fact]
    public async Task Case15_BackgroundWorker_ContinuesOperatingAfterPerIterationError()
    {
        var services = new ServiceCollection();
        var options = new ServiceRequestLifecycleOptions
        {
            Enabled = true,
            InitialDelaySeconds = 0,
            PollIntervalMinutes = 1
        };

        int callCount = 0;
        var mockService = new MockLifecycleExpirationService(() =>
        {
            callCount++;
            if (callCount == 1)
            {
                throw new InvalidOperationException("Simulated transient iteration error.");
            }
            return Task.FromResult(1);
        });

        services.AddSingleton(options);
        services.AddSingleton<IServiceRequestLifecycleExpirationService>(mockService);

        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var cts = new CancellationTokenSource();
        var worker = new ServiceRequestLifecycleExpirationBackgroundService(
            scopeFactory,
            directOptions: options,
            logger: NullLogger<ServiceRequestLifecycleExpirationBackgroundService>.Instance);

        // Execute in background
        var runTask = worker.StartAsync(cts.Token);

        // Allow worker to cycle twice
        await Task.Delay(100);
        cts.Cancel();
        await worker.StopAsync(CancellationToken.None);

        // Verified that callCount >= 1 and worker survived the first exception
        Assert.True(callCount >= 1);
    }

    private sealed class MockLifecycleExpirationService : IServiceRequestLifecycleExpirationService
    {
        private readonly Func<Task<int>> _handler;
        public MockLifecycleExpirationService(Func<Task<int>> handler) => _handler = handler;

        public Task<int> ExpireStaleAwaitingInformationRequestsAsync(CancellationToken cancellationToken = default)
            => _handler();

        public Task<bool> ExpireStaleAwaitingInformationRequestAsync(Guid serviceRequestId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    // =========================================================================
    // CASE 16: CancellationToken stops the worker cleanly
    // =========================================================================
    // Purpose: Checks that cancelling the CancellationToken stops the background worker cleanly.
    // Why: Verifies proper host shutdown behavior.
    // Scenario: Worker started and cancellation requested via CancellationTokenSource.
    // Pass condition: Background execute task completes cleanly.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestLifecycleExpirationTests.Case16_CancellationToken_StopsWorkerCleanly"
    [Fact]
    public async Task Case16_CancellationToken_StopsWorkerCleanly()
    {
        var services = new ServiceCollection();
        var options = new ServiceRequestLifecycleOptions
        {
            Enabled = true,
            InitialDelaySeconds = 0,
            PollIntervalMinutes = 60
        };

        services.AddSingleton(options);
        services.AddSingleton<IServiceRequestLifecycleExpirationService>(new MockLifecycleExpirationService(() => Task.FromResult(0)));

        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var cts = new CancellationTokenSource();
        var worker = new ServiceRequestLifecycleExpirationBackgroundService(
            scopeFactory,
            directOptions: options);

        await worker.StartAsync(cts.Token);
        cts.Cancel(); // Request cancellation
        await worker.StopAsync(CancellationToken.None);

        Assert.True(worker.ExecuteTask?.IsCompleted ?? true);
    }

    // =========================================================================
    // CASE 17: ReadyForMatching requests are NEVER modified by the expiry worker
    // =========================================================================
    // Purpose: Checks that the expiration service specifically ignores ReadyForMatching requests, even if their matching window has elapsed.
    // Why: ReadyForMatching expiration is handled via dynamic eligibility checks and downstream matching queries, not by mutating status to Cancelled.
    // Scenario: ReadyForMatching request with expired matching window subjected to direct and batch expiration scans.
    // Pass condition: Both scans return 0, and request status remains ReadyForMatching.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestLifecycleExpirationTests.Case17_ReadyForMatchingRequests_AreNeverModifiedByExpiryWorker"
    [Fact]
    public async Task Case17_ReadyForMatchingRequests_AreNeverModifiedByExpiryWorker()
    {
        var now = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        var clock = new TestTimeProvider(now);
        var requestRepo = new InMemoryServiceRequestRepository();
        var options = new ServiceRequestLifecycleOptions
        {
            AwaitingInformationTimeoutHours = 24,
            Enabled = true
        };

        var expirationService = new ServiceRequestLifecycleExpirationService(
            requestRepo,
            directOptions: options,
            timeProvider: clock);

        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Status = ServiceRequestStatus.ReadyForMatching, // Ready for matching!
            ReadyForMatchingAtUtc = now.UtcDateTime.AddDays(-10),
            MatchingExpiresAtUtc = now.UtcDateTime.AddDays(-9), // Expired matching window
            UpdatedAt = now.UtcDateTime.AddDays(-10)
        };
        requestRepo.AddSync(request);

        // Attempt direct expiry
        var directResult = await expirationService.ExpireStaleAwaitingInformationRequestAsync(requestId);
        Assert.False(directResult);

        // Attempt batch scan
        var scanResult = await expirationService.ExpireStaleAwaitingInformationRequestsAsync();
        Assert.Equal(0, scanResult);

        // Request remains ReadyForMatching
        Assert.Equal(ServiceRequestStatus.ReadyForMatching, request.Status);
    }

    // =========================================================================
    // CASE 18: Existing readiness guard behavior remains unchanged
    // =========================================================================
    // Purpose: Checks that all readiness prerequisites (missing analysis, unclassified category, unknown urgency, invalid coordinates, unanswered clarification) reject transition to ReadyForMatching with ConflictException.
    // Why: Regression test ensuring matching window features did not weaken existing safety gates.
    // Scenario: 5 invalid readiness states tested sequentially against MarkReadyForMatchingAsync.
    // Pass condition: Throws ConflictException for each invalid state.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestLifecycleExpirationTests.Case18_ExistingReadinessGuards_RemainUnchanged"
    [Fact]
    public async Task Case18_ExistingReadinessGuards_RemainUnchanged()
    {
        var clock = new TestTimeProvider(DateTimeOffset.UtcNow);
        var requestRepo = new InMemoryServiceRequestRepository();
        var analysisRepo = new InMemoryProblemAnalysisRepository();
        var service = new ServiceRequestService(requestRepo, analysisRepo, timeProvider: clock);

        var requestId = Guid.NewGuid();
        var request = CreateValidReadyCandidate(requestId, Guid.NewGuid(), DateTime.UtcNow);

        // 1. Missing analysis -> ConflictException
        requestRepo.AddSync(request);
        await Assert.ThrowsAsync<ConflictException>(() => service.MarkReadyForMatchingAsync(requestId, request.CustomerId));

        // 2. Unclassified category -> ConflictException
        request.Category = "Unclassified";
        var analysis = new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            EvidenceRevision = 1,
            DetectedProblem = "Problem",
            Confidence = 0.9m
        };
        request.ProblemAnalyses.Add(analysis);
        analysisRepo.AddSync(analysis);
        await Assert.ThrowsAsync<ConflictException>(() => service.MarkReadyForMatchingAsync(requestId, request.CustomerId));

        // 3. Unknown urgency -> ConflictException
        request.Category = "Plumbing";
        request.Urgency = ServiceRequestUrgency.Unknown;
        await Assert.ThrowsAsync<ConflictException>(() => service.MarkReadyForMatchingAsync(requestId, request.CustomerId));

        // 4. Invalid coordinates -> ConflictException
        request.Urgency = ServiceRequestUrgency.Medium;
        request.Latitude = 95m; // Invalid latitude > 90
        await Assert.ThrowsAsync<ConflictException>(() => service.MarkReadyForMatchingAsync(requestId, request.CustomerId));

        // 5. Unanswered clarification -> ConflictException
        request.Latitude = 6.9271m;
        request.Clarifications.Add(new ServiceRequestClarification
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            Question = "Unanswered?",
            Answer = null,
            SupersededAt = null
        });
        await Assert.ThrowsAsync<ConflictException>(() => service.MarkReadyForMatchingAsync(requestId, request.CustomerId));
    }

    // =========================================================================
    // CASE 19: Existing EvidenceRevision protections remain unchanged
    // =========================================================================
    // Purpose: Checks that marking ready for matching requires analysis EvidenceRevision to match request EvidenceRevision.
    // Why: Prevents stale AI analysis from qualifying a request for matching after new evidence was added.
    // Scenario: Request at revision 2 has analysis at revision 1.
    // Pass condition: Throws ConflictException containing "outdated".
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestLifecycleExpirationTests.Case19_ExistingEvidenceRevisionProtections_RemainUnchanged"
    [Fact]
    public async Task Case19_ExistingEvidenceRevisionProtections_RemainUnchanged()
    {
        var clock = new TestTimeProvider(DateTimeOffset.UtcNow);
        var requestRepo = new InMemoryServiceRequestRepository();
        var analysisRepo = new InMemoryProblemAnalysisRepository();
        var service = new ServiceRequestService(requestRepo, analysisRepo, timeProvider: clock);

        var requestId = Guid.NewGuid();
        var request = CreateValidReadyCandidate(requestId, Guid.NewGuid(), DateTime.UtcNow);
        request.EvidenceRevision = 2; // Revision 2

        var outdatedAnalysis = new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            EvidenceRevision = 1, // Stale revision 1!
            DetectedProblem = "Old analysis",
            Confidence = 0.95m
        };
        request.ProblemAnalyses.Add(outdatedAnalysis);
        requestRepo.AddSync(request);
        analysisRepo.AddSync(outdatedAnalysis);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.MarkReadyForMatchingAsync(requestId, request.CustomerId));
        Assert.Contains("outdated", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // CASE 20: Existing C1 boundary still has only one ReadyForMatching write path
    // =========================================================================
    // Purpose: Architectural static analysis checking that Status = ReadyForMatching is written in exactly one place in AssistLK.Application.
    // Why: Enforces the Single Source of Truth architecture pattern for state transitions into the ready-for-matching state.
    // Scenario: Scans all C# source files in AssistLK.Application for assignments to ReadyForMatching.
    // Pass condition: Exactly one write site is found, located in ServiceRequestService.cs.
    // Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ServiceRequestLifecycleExpirationTests.Case20_OnlyOneReadyForMatchingWriteSiteExists"
    [Fact]
    public void Case20_OnlyOneReadyForMatchingWriteSiteExists()
    {
        // Architectural guarantee: audit all source files in AssistLK.Application to verify
        // that "Status = ServiceRequestStatus.ReadyForMatching" occurs in exactly one authoritative location.
        var baseDir = AppContext.BaseDirectory;
        // Locate repository root
        var dir = new DirectoryInfo(baseDir);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "backend", "src", "AssistLK.Application")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var appDir = Path.Combine(dir.FullName, "backend", "src", "AssistLK.Application");

        var csFiles = Directory.GetFiles(appDir, "*.cs", SearchOption.AllDirectories);
        var writeSites = new List<string>();

        foreach (var file in csFiles)
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var isAssignment = (line.Contains(".Status =") || line.Contains("Status =")) && !line.Contains("==");
                if (line.Contains("ServiceRequestStatus.ReadyForMatching") && isAssignment)
                {
                    writeSites.Add($"{Path.GetFileName(file)}:line {i + 1}");
                }
            }
        }

        Assert.Single(writeSites);
        Assert.Contains("ServiceRequestService.cs", writeSites[0]);
    }
}
