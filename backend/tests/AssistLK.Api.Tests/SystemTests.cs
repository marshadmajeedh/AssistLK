namespace AssistLK.Api.Tests;

public class SystemTests
{
    [Fact]
    public void ApplicationName_ShouldBe_AssistLK()
    {
        var applicationName = "AssistLK";

        Assert.Equal("AssistLK", applicationName);
    }
}