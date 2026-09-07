using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;

namespace AssistLK.IntegrationTests;

public class ServiceRequestTests
{
    [Fact]
    public void ServiceRequest_CanBeConstructedWithValidValues()
    {
        var customerId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            CustomerId = customerId,
            Category = "Vehicle repair",
            Description = "The vehicle will not start.",
            LocationText = "Colombo",
            Latitude = 6.927079m,
            Longitude = 79.861244m,
            Urgency = ServiceRequestUrgency.High,
            Status = ServiceRequestStatus.Created
        };

        Assert.Equal(customerId, request.CustomerId);
        Assert.Equal("Vehicle repair", request.Category);
        Assert.Equal(ServiceRequestUrgency.High, request.Urgency);
        Assert.Equal(ServiceRequestStatus.Created, request.Status);
    }

    [Fact]
    public void ServiceRequest_UsesTheRequiredInitialValuesAsDefaults()
    {
        var request = new ServiceRequest();

        Assert.Equal(
            Enum.GetValues<ServiceRequestUrgency>().First(),
            request.Urgency);
        Assert.Equal(
            Enum.GetValues<ServiceRequestStatus>().First(),
            request.Status);
        Assert.Equal("Unclassified", request.Category);
        Assert.Equal(string.Empty, request.Description);
        Assert.Equal(string.Empty, request.LocationText);
        Assert.Empty(request.ProblemAnalyses);
    }

    [Fact]
    public void ServiceRequest_CanContainMultipleProblemAnalyses()
    {
        var request = new ServiceRequest();
        var firstAnalysis = new ProblemAnalysis { ServiceRequest = request };
        var secondAnalysis = new ProblemAnalysis { ServiceRequest = request };

        request.ProblemAnalyses.Add(firstAnalysis);
        request.ProblemAnalyses.Add(secondAnalysis);

        Assert.Equal(2, request.ProblemAnalyses.Count);
        Assert.Contains(firstAnalysis, request.ProblemAnalyses);
        Assert.Contains(secondAnalysis, request.ProblemAnalyses);
    }

    [Fact]
    public void ProblemAnalysis_CanReferenceItsParentServiceRequest()
    {
        var request = new ServiceRequest();
        var analysis = new ProblemAnalysis
        {
            ServiceRequest = request,
            ServiceRequestId = request.Id
        };

        Assert.Same(request, analysis.ServiceRequest);
        Assert.Equal(request.Id, analysis.ServiceRequestId);
    }
}