namespace OpenGitBase.Api.Tests.Controllers;

public class ControllerCoverageTests
{
    [Theory]
    [ClassData(typeof(ControllerCoverageTestData))]
    public void Controller_ShouldHaveMatchingTestClass(Type controllerType)
    {
        var testTypes = typeof(HealthControllerTests).Assembly.GetTypes();
        var testClassType = Array.Find(testTypes, t => t.Name == $"{controllerType.Name}Tests");

        Assert.NotNull(testClassType);
    }
}
