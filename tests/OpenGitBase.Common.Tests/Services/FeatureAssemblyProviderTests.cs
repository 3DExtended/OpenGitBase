using OpenGitBase.Common.Data;
using OpenGitBase.Features.Users;

namespace OpenGitBase.Common.Tests.Services;

public class FeatureAssemblyProviderTests
{
    [Fact]
    public void Assemblies_ReturnsConstructorInput()
    {
        var assembly = typeof(UsersMapsterConfig).Assembly;
        var provider = new FeatureAssemblyProvider([assembly]);
        Assert.Single(provider.Assemblies);
        Assert.Same(assembly, provider.Assemblies[0]);
    }
}
