using AssistLK.Domain.Enums;

namespace AssistLK.IntegrationTests;

public class ProblemAnalysisTests
{
    [Fact]
    public void ServiceRequestUrgency_ContainsTheExpectedValues()
    {
        var values = Enum.GetNames<ServiceRequestUrgency>();

        Assert.Equal(
            new[] { "Unknown", "Low", "Medium", "High", "Critical" },
            values);
        Assert.Equal(0, (int)ServiceRequestUrgency.Unknown);
        Assert.Equal(4, (int)ServiceRequestUrgency.Critical);
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