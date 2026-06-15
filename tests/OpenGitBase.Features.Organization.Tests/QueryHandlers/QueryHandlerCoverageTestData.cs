using System.Collections;
using System.Reflection;

using OpenGitBase.Common;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization;

namespace OpenGitBase.Features.Organization.Tests.QueryHandlers;

public class QueryHandlerCoverageTestData : IEnumerable<object[]>
{
    private static readonly Assembly ProductionAssembly = typeof(OrganizationMapsterConfig).Assembly;

    public IEnumerator<object[]> GetEnumerator()
    {
        var queryHandlerType = typeof(IQueryHandler<,>);
        var implementations = ProductionAssembly
            .GetTypes()
            .Where(type =>
                type is { IsAbstract: false, IsInterface: false }
                && type.GetCustomAttribute<ExcludeFromCoverageTestsAttribute>() is null
                && Array.Exists(
                    type.GetInterfaces(),
                    i => i.IsGenericType && i.GetGenericTypeDefinition() == queryHandlerType
                )
            )
            .OrderBy(type => type.Name);

        foreach (var implementation in implementations)
        {
            yield return [implementation];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
