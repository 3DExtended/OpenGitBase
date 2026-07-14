using System.Collections;
using System.Reflection;
using OpenGitBase.Cli.Commands;

namespace OpenGitBase.Cli.Tests;

public class CommandHandlerCoverageTestData : IEnumerable<object[]>
{
    private static readonly Assembly ProductionAssembly = typeof(IssueCommandHandlers).Assembly;

    public IEnumerator<object[]> GetEnumerator()
    {
        var implementations = ProductionAssembly
            .GetExportedTypes()
            .Where(type =>
                type is { IsInterface: false }
                && type.Name.EndsWith("CommandHandlers", StringComparison.Ordinal))
            .OrderBy(type => type.Name);

        foreach (var implementation in implementations)
        {
            yield return [implementation];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
