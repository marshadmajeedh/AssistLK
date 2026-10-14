using AssistLK.Agents.Core;
using AssistLK.Application.Interfaces;
using AssistLK.Application.Services;
using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.IntegrationTests;

/// <summary>
/// What this class tests:
///   AI agent action risk classification, human-in-the-loop approval policies, and safety checks across actions including ANALYZE_PROBLEM, CREATE_BOOKING, MAKE_PAYMENT, SEND_QUOTATION, and SEARCH_PROVIDER.
/// Why these tests are needed:
///   Ensures autonomous AI agents cannot execute high-risk operations (such as making financial transactions or committing bookings) without human approval, while permitting read/analysis tasks to run friction-free.
/// What is real and what is mocked/faked:
///   - Real: AgentSafetyPolicyEngine and AgentSafetyService running against an EF Core In-Memory database (AssistLKDbContext).
///   - Mocked/Faked: None. Uses real policy engine rules and in-memory persistence.
/// Prerequisites:
///   None. Runs completely in-memory without external services or network calls.
/// Run from repository root:
///   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.Component1SafetyTests"
/// </summary>
public class Component1SafetyTests
{
    private readonly AgentSafetyPolicyEngine _policyEngine = new();

    /// <summary>
    /// Purpose: Checks that ANALYZE_PROBLEM action evaluates to LOW risk and requires no human approval.
    /// Why: Problem understanding is a read-only analysis operation that must run automatically without blocking for manual approval.
    /// Scenario: Evaluates action type "ANALYZE_PROBLEM" against policy engine.
    /// Pass condition: RiskLevel is "LOW" and RequiresApproval is false.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1SafetyTests.AnalyzeProblem_EvaluatesToLowRiskAndNoApprovalRequired"
    /// </summary>
    [Fact]
    public void AnalyzeProblem_EvaluatesToLowRiskAndNoApprovalRequired()
    {
        var rule = _policyEngine.Evaluate("ANALYZE_PROBLEM");

        Assert.NotNull(rule);
        Assert.Equal("LOW", rule.RiskLevel);
        Assert.False(rule.RequiresApproval);
    }

    /// <summary>
    /// Purpose: Checks that critical actions like booking creation and payments require mandatory human approval.
    /// Why: Prevents agents from autonomously spending customer funds or committing contractors to jobs.
    /// Scenario: Evaluates "CREATE_BOOKING" (HIGH risk) and "MAKE_PAYMENT" (CRITICAL risk).
    /// Pass condition: Both rules evaluate to RequiresApproval = true with respective HIGH and CRITICAL risk levels.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1SafetyTests.ExistingHighRiskActions_RetainApprovalRequirements"
    /// </summary>
    [Fact]
    public void ExistingHighRiskActions_RetainApprovalRequirements()
    {
        var bookingRule = _policyEngine.Evaluate("CREATE_BOOKING");
        Assert.NotNull(bookingRule);
        Assert.Equal("HIGH", bookingRule.RiskLevel);
        Assert.True(bookingRule.RequiresApproval);

        var paymentRule = _policyEngine.Evaluate("MAKE_PAYMENT");
        Assert.NotNull(paymentRule);
        Assert.Equal("CRITICAL", paymentRule.RiskLevel);
        Assert.True(paymentRule.RequiresApproval);
    }

    /// <summary>
    /// Purpose: Checks that existing provider search and quotation actions retain their defined safety policy levels.
    /// Why: Guard against accidental regression of established agent safety configurations.
    /// Scenario: Evaluates "SEARCH_PROVIDER" (LOW) and "SEND_QUOTATION" (MEDIUM).
    /// Pass condition: Both actions evaluate to RequiresApproval = false with expected risk levels.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1SafetyTests.SafetyRegression_QuotationAndProviderSearch_RetainOriginalSafetyLevels"
    /// </summary>
    [Fact]
    public void SafetyRegression_QuotationAndProviderSearch_RetainOriginalSafetyLevels()
    {
        var providerSearchRule = _policyEngine.Evaluate("SEARCH_PROVIDER");
        Assert.NotNull(providerSearchRule);
        Assert.Equal("LOW", providerSearchRule.RiskLevel);
        Assert.False(providerSearchRule.RequiresApproval);

        var quotationRule = _policyEngine.Evaluate("SEND_QUOTATION");
        Assert.NotNull(quotationRule);
        Assert.Equal("MEDIUM", quotationRule.RiskLevel);
        Assert.False(quotationRule.RequiresApproval);
    }

    /// <summary>
    /// Purpose: Checks comprehensive risk level and approval requirements for all 5 canonical agent actions.
    /// Why: Verifies the full action safety table in a parameterized test suite.
    /// Scenario: Tests CREATE_BOOKING, MAKE_PAYMENT, SEND_QUOTATION, SEARCH_PROVIDER, and ANALYZE_PROBLEM.
    /// Pass condition: Every canonical action matches its expected risk level and approval flag.
    /// Note: Parameterized test (Theory). The command below executes all 5 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.Component1SafetyTests.SafetyPolicyEngine_EvaluatesAllCanonicalRulesAccurately"
    /// </summary>
    [Theory]
    [InlineData("CREATE_BOOKING", "HIGH", true)]
    [InlineData("MAKE_PAYMENT", "CRITICAL", true)]
    [InlineData("SEND_QUOTATION", "MEDIUM", false)]
    [InlineData("SEARCH_PROVIDER", "LOW", false)]
    [InlineData("ANALYZE_PROBLEM", "LOW", false)]
    public void SafetyPolicyEngine_EvaluatesAllCanonicalRulesAccurately(string actionType, string expectedRisk, bool expectedApproval)
    {
        var rule = _policyEngine.Evaluate(actionType);
        Assert.NotNull(rule);
        Assert.Equal(expectedRisk, rule.RiskLevel);
        Assert.Equal(expectedApproval, rule.RequiresApproval);
    }

    /// <summary>
    /// Purpose: Checks that AgentSafetyService records and evaluates ANALYZE_PROBLEM as not requiring approval.
    /// Why: Verifies that safety evaluation integrates cleanly with the database persistence layer during live workflows.
    /// Scenario: CheckActionAsync is called with action "ANALYZE_PROBLEM" and workflow Guid.
    /// Pass condition: Returned action has ActionType "ANALYZE_PROBLEM", RiskLevel "LOW", and RequiresApproval = false.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1SafetyTests.AgentSafetyService_CheckActionAsync_SetsRequiresApprovalFalseForAnalyzeProblem"
    /// </summary>
    [Fact]
    public async Task AgentSafetyService_CheckActionAsync_SetsRequiresApprovalFalseForAnalyzeProblem()
    {
        var options = new DbContextOptionsBuilder<AssistLKDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new AssistLKDbContext(options);
        var safetyService = new AgentSafetyService(_policyEngine, db);

        var action = await safetyService.CheckActionAsync(
            Guid.NewGuid(),
            "ANALYZE_PROBLEM",
            "Analyzing customer problem description");

        Assert.NotNull(action);
        Assert.Equal("ANALYZE_PROBLEM", action.ActionType);
        Assert.Equal("LOW", action.RiskLevel);
        Assert.False(action.RequiresApproval);
    }

    /// <summary>
    /// Purpose: Checks that unrecognized or unclassified action types default to requiring human approval (fail-safe).
    /// Why: Enforces the principle of least privilege: unknown agent behaviors must never execute silently.
    /// Scenario: CheckActionAsync is called with an unknown action "DELETE_USER_DATA".
    /// Pass condition: RequiresApproval is true.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.Component1SafetyTests.AgentSafetyService_CheckActionAsync_RequiresApprovalForUnknownOrSensitiveActions"
    /// </summary>
    [Fact]
    public async Task AgentSafetyService_CheckActionAsync_RequiresApprovalForUnknownOrSensitiveActions()
    {
        var options = new DbContextOptionsBuilder<AssistLKDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new AssistLKDbContext(options);
        var safetyService = new AgentSafetyService(_policyEngine, db);

        var unknownAction = await safetyService.CheckActionAsync(
            Guid.NewGuid(),
            "DELETE_USER_DATA",
            "Sensitive unknown action");

        Assert.True(unknownAction.RequiresApproval);
    }
}
