using System.Reflection;

namespace OpenGitBase.Api;

public static class FeatureRegistration
{
    public static IReadOnlyList<Assembly> GetFeatureAssemblies() =>
        [
            typeof(OpenGitBase.Features.Users.UsersMapsterConfig).Assembly,
            typeof(OpenGitBase.Features.PublicGitSshKey.PublicGitSshKeyMapsterConfig).Assembly,
            typeof(OpenGitBase.Features.Repository.RepositoryMapsterConfig).Assembly,
            // agentGenCli:feature-assemblies
        ];
}
