using AssistLK.Domain.Enums;

namespace AssistLK.IntegrationTests;

/// <summary>
/// What this class tests:
///   Domain enum definitions and integer mappings for ServiceRequestUrgency and ServiceRequestStatus in Component 1.
/// Why these tests are needed:
///   Ensures enum ordinal values and string names remain stable so serialization and database schema values do not break.
/// What is real and what is mocked/faked:
///   - Real: Domain enums (ServiceRequestUrgency, ServiceRequestStatus).
///   - Mocked/Faked: None.
/// Prerequisites:
///   None. Runs in-memory.
/// Run from repository root:
///   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.ProblemAnalysisTests"
/// </summary>
public class ProblemAnalysisTests
{
    /// <summary>
    /// Purpose: Checks that ServiceRequestUrgency contains the exact 5 values in order ("Unknown", "Low", "Medium", "High", "Critical") with Unknown = 0 and Critical = 4.
    /// Why: Verifies domain enum integrity and ordinal mapping used across AI agent outputs and API contracts.
    /// Scenario: Evaluates Enum.GetNames of ServiceRequestUrgency and cast integer values.
    /// Pass condition: Names match the 5 expected strings, Unknown integer value is 0, and Critical integer value is 4.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ProblemAnalysisTests.ServiceRequestUrgency_ContainsTheExpectedValues"
    /// </summary>
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

    /// <summary>
    /// Purpose: Checks that ServiceRequestStatus defines the exact canonical lifecycle statuses ("Created", "Analyzing", "AwaitingInformation", "Analyzed", "ReadyForMatching", "Cancelled").
    /// Why: Protects the finite state machine of service request progression from accidental deletion or renaming of statuses.
    /// Scenario: Evaluates Enum.GetNames of ServiceRequestStatus.
    /// Pass condition: Names match the 6 expected lifecycle status strings.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.ProblemAnalysisTests.ServiceRequestStatus_ContainsTheExpectedValues"
    /// </summary>
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