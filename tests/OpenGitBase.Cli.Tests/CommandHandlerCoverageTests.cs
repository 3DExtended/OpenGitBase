using OpenGitBase.Cli.Commands;

namespace OpenGitBase.Cli.Tests;

public sealed class CommandHandlerCoverageTests
{
    [Theory]
    [ClassData(typeof(CommandHandlerCoverageTestData))]
    public void CommandHandlers_ShouldHaveMatchingTestClass(Type handlerType)
    {
        var prefix = handlerType.Name.Replace("CommandHandlers", string.Empty, StringComparison.Ordinal);
        var testTypes = typeof(IssueCommandTests).Assembly.GetTypes();

        var testClassType = Array.Find(testTypes, t =>
        {
            if (t.Name == $"{prefix}CommandTests"
                || t.Name == $"{prefix}CommandExtendedTests"
                || t.Name == $"{handlerType.Name}Tests")
            {
                return true;
            }

            return false;
        });

        Assert.NotNull(testClassType);
    }
}
