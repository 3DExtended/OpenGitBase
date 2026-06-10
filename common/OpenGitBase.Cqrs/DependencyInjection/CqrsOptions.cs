using System.Reflection;

namespace OpenGitBase.Cqrs.DependencyInjection;

public sealed class CqrsOptions
{
    private readonly List<Assembly> _assembliesToLoadQueryHandlersFrom = [];

    internal IReadOnlyList<Assembly> AssembliesToLoadQueryHandlersFrom =>
        _assembliesToLoadQueryHandlersFrom;

    public CqrsOptions WithQueryHandlersFrom(params Assembly[] assemblies)
    {
        _assembliesToLoadQueryHandlersFrom.AddRange(assemblies);
        return this;
    }
}
