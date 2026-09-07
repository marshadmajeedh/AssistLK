using AssistLK.Agents.Tools;

namespace AssistLK.IntegrationTests;

public class Component1ToolTests
{
    [Fact]
    public void Tools_CanBeInstantiatedWithoutDependencies()
    {
        var classTool = new ProblemClassificationTool();
        var locTool = new LocationExtractionTool();
        var knowTool = new ServiceKnowledgeTool();

        Assert.NotNull(classTool);
        Assert.NotNull(locTool);
        Assert.NotNull(knowTool);

        Assert.Equal("ProblemClassificationTool", classTool.Name);
        Assert.Equal("LocationExtractionTool", locTool.Name);
        Assert.Equal("ServiceKnowledgeTool", knowTool.Name);
    }

    [Theory]
    [InlineData("My kitchen pipe is leaking water all over the floor", "Plumbing")]
    [InlineData("Power socket is sparking and circuit breaker tripped", "Electrical")]
    [InlineData("Car engine won't start and battery seems dead", "Vehicle Repair")]
    [InlineData("The refrigerator is not cooling and freezer is warm", "Appliance Repair")]
    public async Task ProblemClassificationTool_ClassifiesCanonicalCategoriesCorrectly(string description, string expectedCategory)
    {
        var tool = new ProblemClassificationTool();
        var parameters = new Dictionary<string, object>
        {
            ["description"] = description
        };

        var result = await tool.ExecuteAsync(parameters);

        Assert.True(result.Success);
        var data = Assert.IsType<ProblemClassificationData>(result.Data);
        Assert.Equal(expectedCategory, data.Category);
        Assert.True(data.Confidence > 0.4m && data.Confidence <= 1m);
        Assert.NotEmpty(data.SupportingTerms);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hello world abcxyz")]
    public async Task ProblemClassificationTool_ReturnsUnclassifiedForEmptyOrUnrelatedInput(string description)
    {
        var tool = new ProblemClassificationTool();
        var parameters = new Dictionary<string, object>
        {
            ["description"] = description
        };

        var result = await tool.ExecuteAsync(parameters);

        Assert.True(result.Success);
        var data = Assert.IsType<ProblemClassificationData>(result.Data);
        Assert.Equal("Unclassified", data.Category);
    }

    [Fact]
    public async Task LocationExtractionTool_DoesNotInventCoordinatesFromText()
    {
        var tool = new LocationExtractionTool();
        var parameters = new Dictionary<string, object>
        {
            ["locationText"] = "Colombo 07, Cinnamon Gardens, Western Province"
        };

        var result = await tool.ExecuteAsync(parameters);

        Assert.True(result.Success);
        var data = Assert.IsType<LocationExtractionData>(result.Data);
        Assert.Equal("Colombo 07, Cinnamon Gardens, Western Province", data.NormalizedLocation);
        Assert.Null(data.Latitude);
        Assert.Null(data.Longitude);
        Assert.False(data.HasCoordinates);
    }

    [Fact]
    public async Task LocationExtractionTool_PassesThroughValidCoordinates()
    {
        var tool = new LocationExtractionTool();
        var parameters = new Dictionary<string, object>
        {
            ["locationText"] = " Kandy Town  ",
            ["latitude"] = 7.2906m,
            ["longitude"] = 80.6337m
        };

        var result = await tool.ExecuteAsync(parameters);

        Assert.True(result.Success);
        var data = Assert.IsType<LocationExtractionData>(result.Data);
        Assert.Equal("Kandy Town", data.NormalizedLocation);
        Assert.Equal(7.2906m, data.Latitude);
        Assert.Equal(80.6337m, data.Longitude);
        Assert.True(data.HasCoordinates);
    }

    [Fact]
    public async Task LocationExtractionTool_DiscardsInvalidOrPartialCoordinates()
    {
        var tool = new LocationExtractionTool();

        // Partial (latitude only)
        var result1 = await tool.ExecuteAsync(new Dictionary<string, object>
        {
            ["locationText"] = "Galle",
            ["latitude"] = 6.0535m
        });
        var data1 = Assert.IsType<LocationExtractionData>(result1.Data);
        Assert.Null(data1.Latitude);
        Assert.Null(data1.Longitude);
        Assert.False(data1.HasCoordinates);

        // Out-of-range latitude (> 90)
        var result2 = await tool.ExecuteAsync(new Dictionary<string, object>
        {
            ["locationText"] = "Galle",
            ["latitude"] = 95.0m,
            ["longitude"] = 80.0m
        });
        var data2 = Assert.IsType<LocationExtractionData>(result2.Data);
        Assert.Null(data2.Latitude);
        Assert.Null(data2.Longitude);
        Assert.False(data2.HasCoordinates);
    }

    [Theory]
    [InlineData("Electrical")]
    [InlineData("Plumbing")]
    [InlineData("Vehicle Repair")]
    [InlineData("Appliance Repair")]
    public async Task ServiceKnowledgeTool_UsesPossibilityLanguageAndRecommendsInspection(string category)
    {
        var tool = new ServiceKnowledgeTool();
        var parameters = new Dictionary<string, object>
        {
            ["category"] = category
        };

        var result = await tool.ExecuteAsync(parameters);

        Assert.True(result.Success);
        var data = Assert.IsType<ServiceKnowledgeData>(result.Data);

        Assert.True(data.RecommendsProfessionalInspection);
        Assert.Contains("Possible", data.SafeGeneralTerminology, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("guaranteed", data.SafeGeneralTerminology, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("definitely", data.SafeGeneralTerminology, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(data.PossibleMissingInformation);
    }

    [Fact]
    public async Task ServiceKnowledgeTool_DoesNotProvideDangerousDIYInstructions()
    {
        var tool = new ServiceKnowledgeTool();
        var parameters = new Dictionary<string, object>
        {
            ["category"] = "Electrical",
            ["description"] = "Wires sparking in the distribution box"
        };

        var result = await tool.ExecuteAsync(parameters);
        var data = Assert.IsType<ServiceKnowledgeData>(result.Data);

        var terms = data.SafeGeneralTerminology.ToLowerInvariant();
        Assert.DoesNotContain("touch", terms);
        Assert.DoesNotContain("open the panel", terms);
        Assert.DoesNotContain("fix yourself", terms);
        Assert.DoesNotContain("diy", terms);
        Assert.Contains("professional", terms);
    }
}
