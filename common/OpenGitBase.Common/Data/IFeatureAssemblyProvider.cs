using System.Reflection;

namespace OpenGitBase.Common.Data;

public interface IFeatureAssemblyProvider
{
    IReadOnlyList<Assembly> Assemblies { get; }
}
