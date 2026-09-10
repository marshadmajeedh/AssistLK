using System.ComponentModel.DataAnnotations;
using AssistLK.Application.Common.Exceptions;
using AssistLK.Application.Interfaces;
using AssistLK.Application.ServiceRequests.DTOs;
using AssistLK.Domain.Constants;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;

namespace AssistLK.Application.Services;

public class ServiceRequestService : IServiceRequestService
{
    private static readonly HashSet<ServiceRequestStatus> EditableStatuses =
    [
        ServiceRequestStatus.Created,
        ServiceRequestStatus.AwaitingInformation
    ];

    private static readonly HashSet<ServiceRequestStatus> CancellableStatuses =
    [
        ServiceRequestStatus.Created,
        ServiceRequestStatus.Analyzing,
        ServiceRequestStatus.AwaitingInformation,
        ServiceRequestStatus.Analyzed
    ];

    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IProblemAnalysisRepository _problemAnalysisRepository;

    public ServiceRequestService(
        IServiceRequestRepository serviceRequestRepository,
        IProblemAnalysisRepository problemAnalysisRepository)
    {
        _serviceRequestRepository = serviceRequestRepository;
        _problemAnalysisRepository = problemAnalysisRepository;
    }

    public async Task<ServiceRequestResponse> CreateAsync(
        Guid customerId,
        CreateServiceRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer ID is required.");
        }

        ValidateRequest(request);

        CanonicalServiceCategories.IsValidHint(request.CategoryHint, out var normalizedCategoryHint);

        var serviceRequest = new ServiceRequest
        {
            CustomerId = customerId,
            CategoryHint = normalizedCategoryHint,
            Description = request.Description.Trim(),
            LocationText = request.LocationText.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Category = "Unclassified",
            Urgency = ServiceRequestUrgency.Unknown,
            Status = ServiceRequestStatus.Created
        };

        await _serviceRequestRepository.AddAsync(serviceRequest, cancellationToken);
        await _serviceRequestRepository.SaveChangesAsync(cancellationToken);

        return MapResponse(serviceRequest);
    }

    public async Task<ServiceRequestResponse> GetByIdAsync(
        Guid serviceRequestId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var serviceRequest = await GetOwnedRequestAsync(
            serviceRequestId,
            customerId,
            includeProblemAnalyses: true,
            includeClarifications: true,
            cancellationToken: cancellationToken);

        return MapResponse(serviceRequest);
    }

    public async Task<IReadOnlyList<ServiceRequestResponse>> GetCurrentCustomerRequestsAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var requests = await _serviceRequestRepository.GetByCustomerIdAsync(
            customerId,
            cancellationToken);

        return requests.Select(MapResponse).ToArray();
    }

    public async Task<ServiceRequestResponse> UpdateAsync(
        Guid customerId,
        Guid serviceRequestId,
        UpdateServiceRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var serviceRequest = await GetOwnedRequestAsync(
            serviceRequestId,
            customerId,
            includeClarifications: true,
            cancellationToken: cancellationToken);

        if (!EditableStatuses.Contains(serviceRequest.Status))
        {
            throw new ConflictException(
                "Service request cannot be edited in its current status.");
        }

        var descriptionChanged = !string.Equals(
            serviceRequest.Description,
            request.Description.Trim(),
            StringComparison.Ordinal);

        if (serviceRequest.Status == ServiceRequestStatus.AwaitingInformation && descriptionChanged)
        {
            if (serviceRequest.Clarifications != null && serviceRequest.Clarifications.Any())
            {
                var currentRound = serviceRequest.Clarifications.Max(c => c.ClarificationRound);
                var unansweredInCurrentRound = serviceRequest.Clarifications
                    .Where(c => c.ClarificationRound == currentRound &&
                                string.IsNullOrWhiteSpace(c.Answer) &&
                                c.SupersededAt == null);

                var now = DateTime.UtcNow;
                foreach (var q in unansweredInCurrentRound)
                {
                    q.SupersededAt = now;
                }
            }
        }

        serviceRequest.Description = request.Description.Trim();
        serviceRequest.LocationText = request.LocationText.Trim();
        serviceRequest.Latitude = request.Latitude;
        serviceRequest.Longitude = request.Longitude;

        CanonicalServiceCategories.IsValidHint(request.CategoryHint, out var normalizedCategoryHint);
        serviceRequest.CategoryHint = normalizedCategoryHint;

        _serviceRequestRepository.Update(serviceRequest);
        await _serviceRequestRepository.SaveChangesAsync(cancellationToken);

        return MapResponse(serviceRequest);
    }

    public async Task<ServiceRequestResponse> CancelAsync(
        Guid customerId,
        Guid serviceRequestId,
        CancellationToken cancellationToken = default)
    {
        var serviceRequest = await GetOwnedRequestAsync(
            serviceRequestId,
            customerId,
            cancellationToken: cancellationToken);

        if (!CancellableStatuses.Contains(serviceRequest.Status))
        {
            throw new ConflictException(
                "Service request cannot be cancelled in its current status.");
        }

        serviceRequest.Status = ServiceRequestStatus.Cancelled;
        _serviceRequestRepository.Update(serviceRequest);
        await _serviceRequestRepository.SaveChangesAsync(cancellationToken);

        return MapResponse(serviceRequest);
    }

    public async Task<ProblemAnalysisResponse> ApplyProblemAnalysisResultAsync(
        ApplyProblemAnalysisResult result,
        CancellationToken cancellationToken = default)
    {
        ValidateAnalysisResult(result);

        var serviceRequest = await _serviceRequestRepository.GetByIdAsync(
            result.ServiceRequestId,
            includeProblemAnalyses: false,
            includeClarifications: true,
            cancellationToken: cancellationToken);

        if (serviceRequest is null)
        {
            throw new KeyNotFoundException("Service request was not found.");
        }

        if (serviceRequest.Status is not ServiceRequestStatus.Created and
            not ServiceRequestStatus.Analyzing and
            not ServiceRequestStatus.AwaitingInformation)
        {
            throw new ConflictException(
                "Problem analysis cannot be applied in the current status.");
        }

        var analysis = new ProblemAnalysis
        {
            ServiceRequestId = serviceRequest.Id,
            DetectedProblem = result.DetectedProblem.Trim(),
            Confidence = result.Confidence,
            AgentName = result.AgentName.Trim()
        };

        serviceRequest.Category = result.Category.Trim();
        serviceRequest.Urgency = result.Urgency;

        if (result.NeedsMoreInformation)
        {
            var existingClarifications = serviceRequest.Clarifications ?? new List<ServiceRequestClarification>();
            var currentRound = existingClarifications.Any() ? existingClarifications.Max(c => c.ClarificationRound) : 0;

            // Maximum 2 clarification rounds:
            if (currentRound < 2 && result.FollowUpQuestions != null && result.FollowUpQuestions.Any())
            {
                serviceRequest.Clarifications ??= new List<ServiceRequestClarification>();
                var newClarifications = new List<ServiceRequestClarification>();
                int nextRound = currentRound + 1;
                int seq = 1;
                foreach (var q in result.FollowUpQuestions)
                {
                    if (!string.IsNullOrWhiteSpace(q))
                    {
                        var cleanQuestion = q.Trim();
                        if (cleanQuestion.Length > 500)
                        {
                            cleanQuestion = cleanQuestion[..500];
                        }

                        var clarification = new ServiceRequestClarification
                        {
                            ServiceRequestId = serviceRequest.Id,
                            ClarificationRound = nextRound,
                            Sequence = seq++,
                            Question = cleanQuestion,
                            Answer = null,
                            AnsweredAt = null,
                            SupersededAt = null
                        };

                        newClarifications.Add(clarification);
                        serviceRequest.Clarifications.Add(clarification);
                    }
                }

                if (newClarifications.Any())
                {
                    await _serviceRequestRepository.AddClarificationsAsync(newClarifications, cancellationToken);
                }
            }

            serviceRequest.Status = ServiceRequestStatus.AwaitingInformation;
        }
        else
        {
            serviceRequest.Status = ServiceRequestStatus.Analyzed;
        }

        await _problemAnalysisRepository.AddAsync(analysis, cancellationToken);
        _serviceRequestRepository.Update(serviceRequest);
        await _serviceRequestRepository.SaveChangesAsync(cancellationToken);

        return MapAnalysisResponse(analysis);
    }

    public async Task<IReadOnlyList<ProblemAnalysisResponse>> GetProblemAnalysesAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default)
    {
        var serviceRequest = await _serviceRequestRepository.GetByIdAsync(
            serviceRequestId,
            cancellationToken: cancellationToken);

        if (serviceRequest is null)
        {
            throw new KeyNotFoundException("Service request was not found.");
        }

        var analyses = await _problemAnalysisRepository.GetByServiceRequestIdAsync(
            serviceRequestId,
            cancellationToken);

        return analyses.Select(MapAnalysisResponse).ToArray();
    }

    public async Task<ServiceRequestForMatchingResponse?> GetReadyForMatchingAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default)
    {
        var serviceRequest = await _serviceRequestRepository.GetByIdAsync(
            serviceRequestId,
            includeProblemAnalyses: true,
            cancellationToken: cancellationToken);

        if (serviceRequest is null ||
            serviceRequest.Status != ServiceRequestStatus.ReadyForMatching)
        {
            return null;
        }

        var analysis = serviceRequest.ProblemAnalyses
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();

        if (analysis is null)
        {
            return null;
        }

        return new ServiceRequestForMatchingResponse
        {
            ServiceRequestId = serviceRequest.Id,
            Category = serviceRequest.Category,
            ProblemSummary = analysis.DetectedProblem,
            Confidence = analysis.Confidence,
            Urgency = serviceRequest.Urgency,
            LocationText = serviceRequest.LocationText,
            Latitude = serviceRequest.Latitude,
            Longitude = serviceRequest.Longitude,
            Status = serviceRequest.Status,
            CreatedAt = serviceRequest.CreatedAt,
            UpdatedAt = serviceRequest.UpdatedAt
        };
    }

    public async Task<ServiceRequestResponse> BeginAnalysisAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default)
    {
        var serviceRequest = await _serviceRequestRepository.GetByIdAsync(
            serviceRequestId,
            cancellationToken: cancellationToken);

        if (serviceRequest is null)
        {
            throw new KeyNotFoundException("Service request was not found.");
        }

        if (serviceRequest.Status is not ServiceRequestStatus.Created and
            not ServiceRequestStatus.AwaitingInformation)
        {
            throw new ConflictException(
                $"Service request cannot begin analysis from status '{serviceRequest.Status}'.");
        }

        serviceRequest.Status = ServiceRequestStatus.Analyzing;
        _serviceRequestRepository.Update(serviceRequest);
        await _serviceRequestRepository.SaveChangesAsync(cancellationToken);

        return MapResponse(serviceRequest);
    }

    public virtual async Task<ServiceRequestStatus> GetPreAnalysisStatusAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default)
    {
        var serviceRequest = await _serviceRequestRepository.GetByIdAsync(
            serviceRequestId,
            cancellationToken: cancellationToken);

        if (serviceRequest is null)
        {
            throw new KeyNotFoundException("Service request was not found.");
        }

        if (serviceRequest.Status is not ServiceRequestStatus.Created and
            not ServiceRequestStatus.AwaitingInformation)
        {
            throw new ConflictException(
                $"Service request cannot begin analysis from status '{serviceRequest.Status}'.");
        }

        return serviceRequest.Status;
    }

    public virtual async Task<ServiceRequestResponse> RecoverFailedAnalysisAsync(
        Guid serviceRequestId,
        ServiceRequestStatus previousStatus,
        CancellationToken cancellationToken = default)
    {
        if (previousStatus is not ServiceRequestStatus.Created and
            not ServiceRequestStatus.AwaitingInformation)
        {
            throw new ConflictException(
                $"Invalid recovery target status '{previousStatus}'. Analysis can only be recovered to Created or AwaitingInformation.");
        }

        var serviceRequest = await _serviceRequestRepository.GetByIdAsync(
            serviceRequestId,
            cancellationToken: cancellationToken);

        if (serviceRequest is null)
        {
            throw new KeyNotFoundException("Service request was not found.");
        }

        if (serviceRequest.Status != ServiceRequestStatus.Analyzing)
        {
            throw new ConflictException(
                $"Service request cannot be recovered from status '{serviceRequest.Status}'. Recovery is only allowed when status is Analyzing.");
        }

        serviceRequest.Status = previousStatus;
        _serviceRequestRepository.Update(serviceRequest);
        await _serviceRequestRepository.SaveChangesAsync(cancellationToken);

        return MapResponse(serviceRequest);
    }

    public async Task<ServiceRequestResponse> MarkReadyForMatchingAsync(
        Guid serviceRequestId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var serviceRequest = await GetOwnedRequestAsync(
            serviceRequestId,
            customerId,
            includeProblemAnalyses: true,
            cancellationToken: cancellationToken);

        if (serviceRequest.Status != ServiceRequestStatus.Analyzed)
        {
            throw new ConflictException(
                $"Service request must be in Analyzed status to be marked ready for matching, but was '{serviceRequest.Status}'.");
        }

        if (string.IsNullOrWhiteSpace(serviceRequest.Category) ||
            string.Equals(serviceRequest.Category.Trim(), "Unclassified", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException(
                "Cannot mark service request ready for matching with an unclassified category.");
        }

        if (serviceRequest.Urgency == ServiceRequestUrgency.Unknown)
        {
            throw new ConflictException(
                "Cannot mark service request ready for matching with unknown urgency.");
        }

        if (string.IsNullOrWhiteSpace(serviceRequest.LocationText))
        {
            throw new ConflictException(
                "Service request must have location text to be marked ready for matching.");
        }

        if (serviceRequest.Clarifications != null &&
            serviceRequest.Clarifications.Any(c => string.IsNullOrWhiteSpace(c.Answer) && c.SupersededAt == null))
        {
            throw new ConflictException(
                "Cannot mark service request ready for matching while clarification questions are unanswered.");
        }

        ProblemAnalysis? latestAnalysis = null;
        if (serviceRequest.ProblemAnalyses != null && serviceRequest.ProblemAnalyses.Any())
        {
            latestAnalysis = serviceRequest.ProblemAnalyses
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault();
        }
        else
        {
            latestAnalysis = await _problemAnalysisRepository.GetMostRecentByServiceRequestIdAsync(
                serviceRequestId,
                cancellationToken);
        }

        if (latestAnalysis is null)
        {
            throw new ConflictException(
                "Service request must have at least one problem analysis to be marked ready for matching.");
        }

        if (latestAnalysis.Confidence <= 0m || latestAnalysis.Confidence > 1m)
        {
            throw new ConflictException(
                "Cannot mark service request ready for matching because the latest problem analysis has an invalid confidence score.");
        }

        serviceRequest.Status = ServiceRequestStatus.ReadyForMatching;
        _serviceRequestRepository.Update(serviceRequest);
        await _serviceRequestRepository.SaveChangesAsync(cancellationToken);

        return MapResponse(serviceRequest);
    }

    public async Task<IReadOnlyList<ServiceRequestClarificationDto>> SubmitClarificationAnswersAsync(
        Guid customerId,
        Guid serviceRequestId,
        SubmitClarificationAnswersRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        ValidateRequest(request);

        var serviceRequest = await GetOwnedRequestAsync(
            serviceRequestId,
            customerId,
            includeClarifications: true,
            cancellationToken: cancellationToken);

        if (serviceRequest.Status != ServiceRequestStatus.AwaitingInformation)
        {
            throw new ConflictException(
                $"Clarification answers can only be submitted when the request is in AwaitingInformation status, but was '{serviceRequest.Status}'.");
        }

        var clarifications = serviceRequest.Clarifications ?? new List<ServiceRequestClarification>();
        if (!clarifications.Any())
        {
            throw new ConflictException("No clarification questions exist for this service request.");
        }

        var currentRound = clarifications.Max(c => c.ClarificationRound);
        if (request.ClarificationRound != currentRound)
        {
            throw new ConflictException(
                $"Submitted answers do not match the current clarification round. Current round is {currentRound}, but submitted round was {request.ClarificationRound}.");
        }

        var actionableQuestions = clarifications
            .Where(c => c.ClarificationRound == currentRound && c.SupersededAt == null)
            .ToList();

        if (!actionableQuestions.Any())
        {
            throw new ConflictException("No active questions remain to be answered for the current round.");
        }

        var submittedAnswers = new Dictionary<Guid, string>();
        foreach (var ans in request.Answers)
        {
            if (string.IsNullOrWhiteSpace(ans.Answer))
            {
                throw new ArgumentException("All answers must be non-empty.");
            }

            var trimmed = ans.Answer.Trim();
            if (trimmed.Length > 1000)
            {
                throw new ArgumentException("Answer exceeds maximum allowed length of 1000 characters.");
            }

            submittedAnswers[ans.ClarificationId] = trimmed;
        }

        foreach (var q in actionableQuestions)
        {
            if (!submittedAnswers.ContainsKey(q.Id))
            {
                throw new ArgumentException($"Missing required answer for clarification question '{q.Id}'.");
            }
        }

        var now = DateTime.UtcNow;
        foreach (var q in actionableQuestions)
        {
            q.Answer = submittedAnswers[q.Id];
            q.AnsweredAt = now;
        }

        _serviceRequestRepository.Update(serviceRequest);
        await _serviceRequestRepository.SaveChangesAsync(cancellationToken);

        return clarifications
            .OrderBy(c => c.ClarificationRound)
            .ThenBy(c => c.Sequence)
            .Select(MapClarificationResponse)
            .ToArray();
    }

    private async Task<ServiceRequest> GetOwnedRequestAsync(
        Guid serviceRequestId,
        Guid customerId,
        bool includeProblemAnalyses = false,
        bool includeClarifications = false,
        CancellationToken cancellationToken = default)
    {
        var serviceRequest = await _serviceRequestRepository.GetByIdAndCustomerIdAsync(
            serviceRequestId,
            customerId,
            includeProblemAnalyses: includeProblemAnalyses,
            includeClarifications: includeClarifications,
            cancellationToken: cancellationToken);

        return serviceRequest is null
            ? throw new KeyNotFoundException("Service request was not found.")
            : serviceRequest;
    }

    private static void ValidateRequest(object request)
    {
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();

        if (!Validator.TryValidateObject(
                request,
                validationContext,
                validationResults,
                validateAllProperties: true))
        {
            throw new ArgumentException(
                string.Join(" ", validationResults.Select(x => x.ErrorMessage)));
        }

        var coordinatesProvided = request switch
        {
            CreateServiceRequestRequest create =>
                create.Latitude.HasValue || create.Longitude.HasValue,
            UpdateServiceRequestRequest update =>
                update.Latitude.HasValue || update.Longitude.HasValue,
            _ => false
        };

        var bothCoordinatesProvided = request switch
        {
            CreateServiceRequestRequest create =>
                create.Latitude.HasValue && create.Longitude.HasValue,
            UpdateServiceRequestRequest update =>
                update.Latitude.HasValue && update.Longitude.HasValue,
            _ => false
        };

        if (coordinatesProvided && !bothCoordinatesProvided)
        {
            throw new ArgumentException(
                "Latitude and longitude must be supplied together.");
        }

        var hasRequiredText = request switch
        {
            CreateServiceRequestRequest create =>
                !string.IsNullOrWhiteSpace(create.Description) &&
                !string.IsNullOrWhiteSpace(create.LocationText),
            UpdateServiceRequestRequest update =>
                !string.IsNullOrWhiteSpace(update.Description) &&
                !string.IsNullOrWhiteSpace(update.LocationText),
            _ => true
        };

        if (!hasRequiredText)
        {
            throw new ArgumentException(
                "Description and location are required.");
        }

        var categoryHint = request switch
        {
            CreateServiceRequestRequest create => create.CategoryHint,
            UpdateServiceRequestRequest update => update.CategoryHint,
            _ => null
        };

        if (categoryHint is not null)
        {
            if (!CanonicalServiceCategories.IsValidHint(categoryHint, out _))
            {
                throw new ArgumentException(
                    $"Category hint '{categoryHint}' is invalid. Allowed values are: Plumbing, Electrical, Vehicle Repair, Appliance Repair, or null.");
            }
        }
    }

    private static void ValidateAnalysisResult(ApplyProblemAnalysisResult result)
    {
        ValidateRequest(result);

        if (result.ServiceRequestId == Guid.Empty)
        {
            throw new ArgumentException("Service request ID is required.");
        }

        if (result.Confidence < 0 || result.Confidence > 1)
        {
            throw new ArgumentException(
                "Confidence must be between 0 and 1.");
        }

        if (!Enum.IsDefined(result.Urgency))
        {
            throw new ArgumentException("Urgency is invalid.");
        }

        if (string.IsNullOrWhiteSpace(result.DetectedProblem) ||
            string.IsNullOrWhiteSpace(result.Category) ||
            string.IsNullOrWhiteSpace(result.AgentName))
        {
            throw new ArgumentException(
                "Detected problem, category, and agent name are required.");
        }
    }

    private static ServiceRequestResponse MapResponse(ServiceRequest serviceRequest)
    {
        ProblemAnalysisSummaryDto? latestAnalysis = null;
        if (serviceRequest.ProblemAnalyses != null && serviceRequest.ProblemAnalyses.Any())
        {
            var latest = serviceRequest.ProblemAnalyses
                .OrderByDescending(x => x.CreatedAt)
                .First();

            latestAnalysis = new ProblemAnalysisSummaryDto
            {
                Id = latest.Id,
                DetectedProblem = latest.DetectedProblem,
                Confidence = latest.Confidence,
                AgentName = latest.AgentName,
                CreatedAt = latest.CreatedAt
            };
        }

        var clarifications = serviceRequest.Clarifications != null
            ? serviceRequest.Clarifications
                .OrderBy(x => x.ClarificationRound)
                .ThenBy(x => x.Sequence)
                .Select(MapClarificationResponse)
                .ToArray()
            : Array.Empty<ServiceRequestClarificationDto>();

        return new ServiceRequestResponse
        {
            ServiceRequestId = serviceRequest.Id,
            CustomerId = serviceRequest.CustomerId,
            CategoryHint = serviceRequest.CategoryHint,
            Category = serviceRequest.Category,
            Description = serviceRequest.Description,
            LocationText = serviceRequest.LocationText,
            Latitude = serviceRequest.Latitude,
            Longitude = serviceRequest.Longitude,
            Urgency = serviceRequest.Urgency,
            Status = serviceRequest.Status,
            CreatedAt = serviceRequest.CreatedAt,
            UpdatedAt = serviceRequest.UpdatedAt,
            LatestAnalysis = latestAnalysis,
            Clarifications = clarifications
        };
    }

    private static ServiceRequestClarificationDto MapClarificationResponse(ServiceRequestClarification clarification)
    {
        return new ServiceRequestClarificationDto
        {
            Id = clarification.Id,
            ServiceRequestId = clarification.ServiceRequestId,
            ClarificationRound = clarification.ClarificationRound,
            Sequence = clarification.Sequence,
            Question = clarification.Question,
            Answer = clarification.Answer,
            AnsweredAt = clarification.AnsweredAt,
            SupersededAt = clarification.SupersededAt,
            CreatedAt = clarification.CreatedAt,
            UpdatedAt = clarification.UpdatedAt
        };
    }

    private static ProblemAnalysisResponse MapAnalysisResponse(ProblemAnalysis analysis)
    {
        return new ProblemAnalysisResponse
        {
            ProblemAnalysisId = analysis.Id,
            ServiceRequestId = analysis.ServiceRequestId,
            DetectedProblem = analysis.DetectedProblem,
            Confidence = analysis.Confidence,
            AgentName = analysis.AgentName,
            CreatedAt = analysis.CreatedAt,
            UpdatedAt = analysis.UpdatedAt
        };
    }
}
