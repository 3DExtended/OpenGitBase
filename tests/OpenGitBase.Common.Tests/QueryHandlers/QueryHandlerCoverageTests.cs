using OpenGitBase.Common.Tests.QueryHandlers.HealthCheck;

namespace OpenGitBase.Common.Tests.QueryHandlers;

public class QueryHandlerCoverageTests
{
    [Theory]
    [ClassData(typeof(QueryHandlerCoverageTestData))]
    public void QueryHandler_ShouldHaveMatchingTestClass(Type queryHandlerType)
    {
        var testTypes = typeof(SystemHealthCheckQueryHandlerTests).Assembly.GetTypes();
        var testClassType = Array.Find(testTypes, t => t.Name == $"{queryHandlerType.Name}Tests");

        Assert.NotNull(testClassType);
    }
}
