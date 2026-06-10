using System.Reflection;

namespace OpenGitBase.Common.Data;

public sealed class FeatureAssemblyProvider : IFeatureAssemblyProvider
{
    public FeatureAssemblyProvider(IReadOnlyList<Assembly> assemblies)
    {
        Assemblies = assemblies;
    }

    public IReadOnlyList<Assembly> Assemblies { get; }
}
