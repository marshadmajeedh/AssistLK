using AssistLK.Application.Interfaces;
using AssistLK.Application.ServiceRequests;
using AssistLK.Application.Services;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssistLK.IntegrationTests;

public class StaleAnalysisRecoveryServiceTests
{
    [Fact]
    public async Task Test1_Stale_Analyzing_Request_Is_Recovered()
    {
        // Arrange
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Leaking pipe in kitchen",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Analyzing,
            EvidenceRevision = 1,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10) // 10 mins ago (> 5 min threshold)
        };
        testBed.AddRequest(request);

        // Act
        var recoveredCount = await testBed.Service.RecoverStaleAnalysesAsync();

        // Assert
        Assert.Equal(1, recoveredCount);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        Assert.Equal(ServiceRequestStatus.Created, updated.Status);
    }

    [Fact]
    public async Task Test2_Fresh_Analyzing_Request_Is_NOT_Recovered()
    {
        // Arrange
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Leaking pipe in kitchen",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Analyzing,
            EvidenceRevision = 1,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-2) // 2 mins ago (< 5 min threshold)
        };
        testBed.AddRequest(request);

        // Act
        var recoveredCount = await testBed.Service.RecoverStaleAnalysesAsync();

        // Assert
        Assert.Equal(0, recoveredCount);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        Assert.Equal(ServiceRequestStatus.Analyzing, updated.Status);
    }

    [Fact]
    public async Task Test3_Created_Request_Unchanged()
    {
        // Arrange
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Leaking pipe in kitchen",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Created,
            EvidenceRevision = 1,
            UpdatedAt = DateTime.UtcNow.AddHours(-2)
        };
        testBed.AddRequest(request);

        // Act
        var recoveredCount = await testBed.Service.RecoverStaleAnalysesAsync();

        // Assert
        Assert.Equal(0, recoveredCount);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        Assert.Equal(ServiceRequestStatus.Created, updated.Status);
    }

    [Fact]
    public async Task Test4_AwaitingInformation_Request_Unchanged()
    {
        // Arrange
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Leaking pipe in kitchen",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.AwaitingInformation,
            EvidenceRevision = 1,
            UpdatedAt = DateTime.UtcNow.AddHours(-2)
        };
        testBed.AddRequest(request);

        // Act
        var recoveredCount = await testBed.Service.RecoverStaleAnalysesAsync();

        // Assert
        Assert.Equal(0, recoveredCount);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, updated.Status);
    }

    [Fact]
    public async Task Test5_Analyzed_Request_Unchanged()
    {
        // Arrange
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Leaking pipe in kitchen",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Analyzed,
            EvidenceRevision = 1,
            UpdatedAt = DateTime.UtcNow.AddHours(-2)
        };
        testBed.AddRequest(request);

        // Act
        var recoveredCount = await testBed.Service.RecoverStaleAnalysesAsync();

        // Assert
        Assert.Equal(0, recoveredCount);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        Assert.Equal(ServiceRequestStatus.Analyzed, updated.Status);
    }

    [Fact]
    public async Task Test6_ReadyForMatching_Unchanged()
    {
        // Arrange
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Leaking pipe in kitchen",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.ReadyForMatching,
            EvidenceRevision = 1,
            UpdatedAt = DateTime.UtcNow.AddHours(-2)
        };
        testBed.AddRequest(request);

        // Act
        var recoveredCount = await testBed.Service.RecoverStaleAnalysesAsync();

        // Assert
        Assert.Equal(0, recoveredCount);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        Assert.Equal(ServiceRequestStatus.ReadyForMatching, updated.Status);
    }

    [Fact]
    public async Task Test7_Cancelled_Unchanged()
    {
        // Arrange
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Leaking pipe in kitchen",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Cancelled,
            EvidenceRevision = 1,
            UpdatedAt = DateTime.UtcNow.AddHours(-2)
        };
        testBed.AddRequest(request);

        // Act
        var recoveredCount = await testBed.Service.RecoverStaleAnalysesAsync();

        // Assert
        Assert.Equal(0, recoveredCount);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        Assert.Equal(ServiceRequestStatus.Cancelled, updated.Status);
    }

    [Fact]
    public async Task Test8_Recovery_Does_Not_Modify_EvidenceRevision()
    {
        // Arrange
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        const long initialEvidenceRevision = 4L;
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Electrical short circuit",
            LocationText = "Kandy",
            Status = ServiceRequestStatus.Analyzing,
            EvidenceRevision = initialEvidenceRevision,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-15)
        };
        testBed.AddRequest(request);

        // Act
        var recovered = await testBed.Service.RecoverStaleRequestAsync(requestId);

        // Assert
        Assert.True(recovered);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        Assert.Equal(ServiceRequestStatus.Created, updated.Status);
        Assert.Equal(initialEvidenceRevision, updated.EvidenceRevision);
    }

    [Fact]
    public async Task Test9_Recovery_Does_Not_Remove_ProblemAnalysis_And_Preserves_History()
    {
        // Arrange
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Refrigerator not cooling",
            LocationText = "Galle",
            Status = ServiceRequestStatus.Analyzing,
            EvidenceRevision = 2L, // New revision after customer edited details
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        // Analysis exists for previous revision 1
        var priorAnalysis = new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            EvidenceRevision = 1L,
            DetectedProblem = "Compressor faulty",
            Confidence = 0.85m,
            AgentName = "ProblemUnderstandingAgent"
        };
        request.ProblemAnalyses.Add(priorAnalysis);
        testBed.AddRequest(request);

        // Act
        var recovered = await testBed.Service.RecoverStaleRequestAsync(requestId);

        // Assert
        Assert.True(recovered);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        Assert.Equal(ServiceRequestStatus.Created, updated.Status);
        Assert.Single(updated.ProblemAnalyses);
        Assert.Equal(priorAnalysis.Id, updated.ProblemAnalyses.First().Id);
        Assert.Equal("Compressor faulty", updated.ProblemAnalyses.First().DetectedProblem);
    }

    [Fact]
    public async Task Test9b_Valid_Analysis_For_Current_Revision_Prevents_Downgrade()
    {
        // Arrange
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Air conditioner leak",
            LocationText = "Negombo",
            Status = ServiceRequestStatus.Analyzing,
            EvidenceRevision = 1L,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        // Analysis already completed for current revision 1
        var currentAnalysis = new ProblemAnalysis
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            EvidenceRevision = 1L,
            DetectedProblem = "Condensate drainage blocked",
            Confidence = 0.90m,
            AgentName = "ProblemUnderstandingAgent"
        };
        request.ProblemAnalyses.Add(currentAnalysis);
        testBed.AddRequest(request);

        // Act
        var recovered = await testBed.Service.RecoverStaleRequestAsync(requestId);

        // Assert
        Assert.False(recovered);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        // Did not downgrade to Created because valid analysis exists
        Assert.Equal(ServiceRequestStatus.Analyzing, updated.Status);
    }

    [Fact]
    public async Task Test10_Concurrent_Advancement_Prevents_Stale_Worker_Overwrite()
    {
        // Arrange
        var testBed = new TestRecoveryEnvironment(throwConcurrencyExceptionOnSave: true);
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Generator repair",
            LocationText = "Kurunegala",
            Status = ServiceRequestStatus.Analyzing,
            EvidenceRevision = 1L,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-15)
        };
        testBed.AddRequest(request);

        // Act
        var recovered = await testBed.Service.RecoverStaleRequestAsync(requestId);

        // Assert
        Assert.False(recovered); // Concurrency exception safely handled and prevented downgrade
    }

    [Fact]
    public async Task Test11_Repeated_Recovery_Invocation_Is_Idempotent()
    {
        // Arrange
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Washing machine drainage fault",
            LocationText = "Matara",
            Status = ServiceRequestStatus.Analyzing,
            EvidenceRevision = 1L,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        testBed.AddRequest(request);

        // Act 1
        var firstRunCount = await testBed.Service.RecoverStaleAnalysesAsync();
        Assert.Equal(1, firstRunCount);
        Assert.Equal(ServiceRequestStatus.Created, testBed.GetRequest(requestId)!.Status);

        // Act 2 (Repeated)
        var secondRunCount = await testBed.Service.RecoverStaleAnalysesAsync();
        Assert.Equal(0, secondRunCount);

        // Act 3 (Repeated again)
        var thirdRunCount = await testBed.Service.RecoverStaleAnalysesAsync();
        Assert.Equal(0, thirdRunCount);

        // Request remains safely in Created state
        Assert.Equal(ServiceRequestStatus.Created, testBed.GetRequest(requestId)!.Status);
    }

    [Fact]
    public async Task Test12_Multiple_Eligible_Stale_Requests_Recover_Independently()
    {
        // Arrange
        var testBed = new TestRecoveryEnvironment();

        // 1. Stale Analyzing request with NO clarifications -> recovers to Created
        var id1 = Guid.NewGuid();
        testBed.AddRequest(new ServiceRequest
        {
            Id = id1,
            CustomerId = Guid.NewGuid(),
            Description = "Roof leak",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Analyzing,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
        });

        // 2. Stale Analyzing request WITH clarifications -> recovers to AwaitingInformation
        var id2 = Guid.NewGuid();
        var req2 = new ServiceRequest
        {
            Id = id2,
            CustomerId = Guid.NewGuid(),
            Description = "Solar inverter error",
            LocationText = "Kandy",
            Status = ServiceRequestStatus.Analyzing,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-12)
        };
        req2.Clarifications.Add(new ServiceRequestClarification
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = id2,
            ClarificationRound = 1,
            Question = "What error code is shown?",
            Answer = null,
            AnsweredAt = null,
            SupersededAt = null
        });
        testBed.AddRequest(req2);

        // 3. Stale Analyzing request with NO clarifications -> recovers to Created
        var id3 = Guid.NewGuid();
        testBed.AddRequest(new ServiceRequest
        {
            Id = id3,
            CustomerId = Guid.NewGuid(),
            Description = "Door lock jammed",
            LocationText = "Galle",
            Status = ServiceRequestStatus.Analyzing,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-8)
        });

        // 4. Fresh Analyzing request -> should NOT recover
        var id4 = Guid.NewGuid();
        testBed.AddRequest(new ServiceRequest
        {
            Id = id4,
            CustomerId = Guid.NewGuid(),
            Description = "CCTV camera offline",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Analyzing,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-1)
        });

        // 5. Already Analyzed request -> should NOT recover
        var id5 = Guid.NewGuid();
        testBed.AddRequest(new ServiceRequest
        {
            Id = id5,
            CustomerId = Guid.NewGuid(),
            Description = "Plumbing repair",
            LocationText = "Jaffna",
            Status = ServiceRequestStatus.Analyzed,
            UpdatedAt = DateTime.UtcNow.AddHours(-1)
        });

        // Act
        var recoveredCount = await testBed.Service.RecoverStaleAnalysesAsync();

        // Assert
        Assert.Equal(3, recoveredCount);

        // id1: recovered to Created
        Assert.Equal(ServiceRequestStatus.Created, testBed.GetRequest(id1)!.Status);

        // id2: recovered to AwaitingInformation (preserving clarification context)
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, testBed.GetRequest(id2)!.Status);

        // id3: recovered to Created
        Assert.Equal(ServiceRequestStatus.Created, testBed.GetRequest(id3)!.Status);

        // id4: untouched in Analyzing
        Assert.Equal(ServiceRequestStatus.Analyzing, testBed.GetRequest(id4)!.Status);

        // id5: untouched in Analyzed
        Assert.Equal(ServiceRequestStatus.Analyzed, testBed.GetRequest(id5)!.Status);
    }

    [Fact]
    public async Task Lifecycle_Case1_No_Clarification_Recovers_To_Created()
    {
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "No clarifications requested",
            LocationText = "Colombo",
            Status = ServiceRequestStatus.Analyzing,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        testBed.AddRequest(request);

        var recovered = await testBed.Service.RecoverStaleRequestAsync(requestId);

        Assert.True(recovered);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        Assert.Equal(ServiceRequestStatus.Created, updated.Status);
    }

    [Fact]
    public async Task Lifecycle_Case2_Current_Unanswered_Clarification_Recovers_To_AwaitingInformation()
    {
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Has pending question",
            LocationText = "Kandy",
            Status = ServiceRequestStatus.Analyzing,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        request.Clarifications.Add(new ServiceRequestClarification
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            ClarificationRound = 1,
            Question = "Is it leaking from the base?",
            Answer = null,
            SupersededAt = null
        });
        testBed.AddRequest(request);

        var recovered = await testBed.Service.RecoverStaleRequestAsync(requestId);

        Assert.True(recovered);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, updated.Status);
    }

    [Fact]
    public async Task Lifecycle_Case3_Answered_Clarification_Only_Recovers_To_Created()
    {
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Customer answered previous question",
            LocationText = "Galle",
            Status = ServiceRequestStatus.Analyzing,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        request.Clarifications.Add(new ServiceRequestClarification
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            ClarificationRound = 1,
            Question = "What model is the appliance?",
            Answer = "Samsung RT28",
            AnsweredAt = DateTime.UtcNow.AddMinutes(-20),
            SupersededAt = null
        });
        testBed.AddRequest(request);

        var recovered = await testBed.Service.RecoverStaleRequestAsync(requestId);

        Assert.True(recovered);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        // Because question was answered, customer owes no information -> Created
        Assert.Equal(ServiceRequestStatus.Created, updated.Status);
    }

    [Fact]
    public async Task Lifecycle_Case4_Superseded_Clarification_Only_Recovers_To_Created()
    {
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Previous question was superseded by customer edit",
            LocationText = "Jaffna",
            Status = ServiceRequestStatus.Analyzing,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        request.Clarifications.Add(new ServiceRequestClarification
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            ClarificationRound = 1,
            Question = "Is the pipe plastic or copper?",
            Answer = null,
            SupersededAt = DateTime.UtcNow.AddMinutes(-15) // Superseded!
        });
        testBed.AddRequest(request);

        var recovered = await testBed.Service.RecoverStaleRequestAsync(requestId);

        Assert.True(recovered);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        // Superseded questions are inactive, customer owes no information -> Created
        Assert.Equal(ServiceRequestStatus.Created, updated.Status);
    }

    [Fact]
    public async Task Lifecycle_Case5_Historical_Answered_Plus_Current_Unanswered_Recovers_To_AwaitingInformation()
    {
        var testBed = new TestRecoveryEnvironment();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            CustomerId = Guid.NewGuid(),
            Description = "Round 2 in progress",
            LocationText = "Matara",
            Status = ServiceRequestStatus.Analyzing,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        // Round 1 answered
        request.Clarifications.Add(new ServiceRequestClarification
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            ClarificationRound = 1,
            Question = "What color is the smoke?",
            Answer = "White smoke",
            AnsweredAt = DateTime.UtcNow.AddMinutes(-30),
            SupersededAt = null
        });
        // Round 2 unanswered
        request.Clarifications.Add(new ServiceRequestClarification
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            ClarificationRound = 2,
            Question = "Does the engine crank?",
            Answer = null,
            SupersededAt = null
        });
        testBed.AddRequest(request);

        var recovered = await testBed.Service.RecoverStaleRequestAsync(requestId);

        Assert.True(recovered);
        var updated = testBed.GetRequest(requestId);
        Assert.NotNull(updated);
        // Current unanswered question exists -> AwaitingInformation
        Assert.Equal(ServiceRequestStatus.AwaitingInformation, updated.Status);
    }

    private sealed class TestRecoveryEnvironment
    {
        private readonly List<ServiceRequest> _requests = new();
        private readonly bool _throwConcurrencyExceptionOnSave;

        public TestRecoveryRepo Repository { get; }
        public StaleAnalysisRecoveryService Service { get; }

        public TestRecoveryEnvironment(bool throwConcurrencyExceptionOnSave = false)
        {
            _throwConcurrencyExceptionOnSave = throwConcurrencyExceptionOnSave;
            Repository = new TestRecoveryRepo(_requests, _throwConcurrencyExceptionOnSave);
            var options = new C1RecoveryOptions
            {
                StaleAnalysisMinutes = 5,
                PollIntervalSeconds = 60,
                Enabled = true
            };
            Service = new StaleAnalysisRecoveryService(
                Repository,
                workflowDbContext: null,
                directOptions: options,
                logger: NullLogger<StaleAnalysisRecoveryService>.Instance);
        }

        public void AddRequest(ServiceRequest request) => _requests.Add(request);

        public ServiceRequest? GetRequest(Guid id) => _requests.FirstOrDefault(r => r.Id == id);
    }

    private sealed class TestRecoveryRepo : IServiceRequestRepository
    {
        private readonly List<ServiceRequest> _requests;
        private readonly bool _throwConcurrencyException;

        public TestRecoveryRepo(List<ServiceRequest> requests, bool throwConcurrencyException = false)
        {
            _requests = requests;
            _throwConcurrencyException = throwConcurrencyException;
        }

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
            if (_throwConcurrencyException)
            {
                throw new DbUpdateConcurrencyException("Simulated concurrency conflict during test.");
            }

            return Task.CompletedTask;
        }
    }
}
