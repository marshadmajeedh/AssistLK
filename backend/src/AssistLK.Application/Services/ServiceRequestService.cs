using System.ComponentModel.DataAnnotations;
using AssistLK.Application.Common.Exceptions;
using AssistLK.Application.Interfaces;
using AssistLK.Application.ServiceRequests.DTOs;
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

        var serviceRequest = new ServiceRequest
        {
            CustomerId = customerId,
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
            cancellationToken: cancellationToken);

        if (!EditableStatuses.Contains(serviceRequest.Status))
        {
            throw new ConflictException(
                "Service request cannot be edited in its current status.");
        }

        serviceRequest.Description = request.Description.Trim();
        serviceRequest.LocationText = request.LocationText.Trim();
        serviceRequest.Latitude = request.Latitude;
        serviceRequest.Longitude = request.Longitude;

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
        serviceRequest.Status = result.NeedsMoreInformation
            ? ServiceRequestStatus.AwaitingInformation
            : ServiceRequestStatus.Analyzed;

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

    private async Task<ServiceRequest> GetOwnedRequestAsync(
        Guid serviceRequestId,
        Guid customerId,
        bool includeProblemAnalyses = false,
        CancellationToken cancellationToken = default)
    {
        var serviceRequest = await _serviceRequestRepository.GetByIdAndCustomerIdAsync(
            serviceRequestId,
            customerId,
            includeProblemAnalyses: includeProblemAnalyses,
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
        return new ServiceRequestResponse
        {
            ServiceRequestId = serviceRequest.Id,
            CustomerId = serviceRequest.CustomerId,
            Category = serviceRequest.Category,
            Description = serviceRequest.Description,
            LocationText = serviceRequest.LocationText,
            Latitude = serviceRequest.Latitude,
            Longitude = serviceRequest.Longitude,
            Urgency = serviceRequest.Urgency,
            Status = serviceRequest.Status,
            CreatedAt = serviceRequest.CreatedAt,
            UpdatedAt = serviceRequest.UpdatedAt
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
