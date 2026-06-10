using System.Reflection;

namespace OpenGitBase.Api;

public static class FeatureRegistration
{
    public static IReadOnlyList<Assembly> GetFeatureAssemblies() =>
        [
            typeof(OpenGitBase.Features.Users.UsersMapsterConfig).Assembly,
            // agentGenCli:feature-assemblies
        ];
}
