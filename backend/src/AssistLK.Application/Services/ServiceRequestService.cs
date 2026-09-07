using System.ComponentModel.DataAnnotations;
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
        CancellationToken cancellationToken = default)
    {
        var serviceRequest = await _serviceRequestRepository.GetByIdAsync(
            serviceRequestId,
            cancellationToken: cancellationToken);

        return serviceRequest is null
            ? throw new KeyNotFoundException("Service request was not found.")
            : MapResponse(serviceRequest);
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
            cancellationToken);

        if (!EditableStatuses.Contains(serviceRequest.Status))
        {
            throw new InvalidOperationException(
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
            cancellationToken);

        if (!CancellableStatuses.Contains(serviceRequest.Status))
        {
            throw new InvalidOperationException(
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
            throw new InvalidOperationException(
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

    private async Task<ServiceRequest> GetOwnedRequestAsync(
        Guid serviceRequestId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var serviceRequest = await _serviceRequestRepository.GetByIdAndCustomerIdAsync(
            serviceRequestId,
            customerId,
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
