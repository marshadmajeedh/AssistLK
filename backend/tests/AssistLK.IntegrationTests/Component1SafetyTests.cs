using AssistLK.Agents.Core;
using AssistLK.Application.Interfaces;
using AssistLK.Application.Services;
using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.IntegrationTests;

public class Component1SafetyTests
{
    private readonly AgentSafetyPolicyEngine _policyEngine = new();

    [Fact]
    public void AnalyzeProblem_EvaluatesToLowRiskAndNoApprovalRequired()
    {
        var rule = _policyEngine.Evaluate("ANALYZE_PROBLEM");

        Assert.NotNull(rule);
        Assert.Equal("LOW", rule.RiskLevel);
        Assert.False(rule.RequiresApproval);
    }

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
