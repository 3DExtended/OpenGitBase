using System.Collections;
using System.Reflection;

using OpenGitBase.Common;
using OpenGitBase.Common.QueryHandlers.HealthCheck;
using OpenGitBase.Common.SendGrid.QueryHandlers;

using OpenGitBase.Cqrs;

namespace OpenGitBase.Common.Tests.QueryHandlers;

public class QueryHandlerCoverageTestData : IEnumerable<object[]>
{
    private static readonly Assembly[] ProductionAssemblies =
    [
        typeof(SystemHealthCheckQueryHandler).Assembly,
        typeof(EmailSendQueryHandler).Assembly,
    ];

    public IEnumerator<object[]> GetEnumerator()
    {
        var queryHandlerType = typeof(IQueryHandler<,>);
        var implementations = ProductionAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type =>
                type is { IsAbstract: false, IsInterface: false }
                && type.GetCustomAttribute<ExcludeFromCoverageTestsAttribute>() is null
                && Array.Exists(
                    type.GetInterfaces(),
                    i => i.IsGenericType && i.GetGenericTypeDefinition() == queryHandlerType
                )
            )
            .Distinct()
            .OrderBy(type => type.Name);

        foreach (var implementation in implementations)
        {
            yield return [implementation];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
