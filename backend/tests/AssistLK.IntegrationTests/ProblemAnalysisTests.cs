using AssistLK.Domain.Enums;

namespace AssistLK.IntegrationTests;

public class ProblemAnalysisTests
{
    [Fact]
    public void ServiceRequestUrgency_ContainsTheExpectedValues()
    {
        var values = Enum.GetNames<ServiceRequestUrgency>();

        Assert.Equal(
            new[] { "Low", "Medium", "High", "Critical" },
            values);
    }

    [Fact]
    public void ServiceRequestStatus_ContainsTheExpectedValues()
    {
        var values = Enum.GetNames<ServiceRequestStatus>();

        Assert.Equal(
            new[]
            {
                "Created",
                "Analyzing",
                "AwaitingInformation",
                "Analyzed",
                "ReadyForMatching",
                "Cancelled"
            },
            values);
    }
}