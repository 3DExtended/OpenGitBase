using System.Collections;
using System.Reflection;

using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Common;

namespace OpenGitBase.Api.Tests.Controllers;

public class ControllerCoverageTestData : IEnumerable<object[]>
{
    private static readonly Assembly ProductionAssembly = typeof(ApiEntryPoint).Assembly;

    public IEnumerator<object[]> GetEnumerator()
    {
        var implementations = ProductionAssembly
            .GetTypes()
            .Where(type =>
                type is { IsAbstract: false, IsInterface: false }
                && type.GetCustomAttribute<ExcludeFromCoverageTestsAttribute>() is null
                && typeof(ControllerBase).IsAssignableFrom(type)
            )
            .OrderBy(type => type.Name);

        foreach (var implementation in implementations)
        {
            yield return [implementation];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
