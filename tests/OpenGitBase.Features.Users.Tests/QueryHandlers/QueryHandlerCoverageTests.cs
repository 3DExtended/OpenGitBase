namespace OpenGitBase.Features.Users.Tests.QueryHandlers;

public class QueryHandlerCoverageTests
{
    [Theory]
    [ClassData(typeof(QueryHandlerCoverageTestData))]
    public void QueryHandler_ShouldHaveMatchingTestClass(Type queryHandlerType)
    {
        var testTypes = typeof(QueryHandlerCoverageTestData).Assembly.GetTypes();
        var testClassType = Array.Find(testTypes, t => t.Name == $"{queryHandlerType.Name}Tests");

        Assert.NotNull(testClassType);
    }
}
