using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Cqrs.DependencyInjection;

namespace OpenGitBase.Cqrs.Tests.DependencyInjection;

public class CqrsOptionsTests
{
    [Fact]
    public void WithQueryHandlersFrom_AddsAssemblies()
    {
        var options = new CqrsOptions();
        var assembly = typeof(CqrsOptionsTests).Assembly;

        var result = options.WithQueryHandlersFrom(assembly);

        Assert.Same(options, result);
        var assemblies = typeof(CqrsOptions)
            .GetProperty(
                "AssembliesToLoadQueryHandlersFrom",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!
            .GetValue(options) as IReadOnlyList<Assembly>;

        Assert.Contains(assembly, assemblies);
    }
}
