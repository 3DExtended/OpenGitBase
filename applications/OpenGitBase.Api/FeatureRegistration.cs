using System.Reflection;

namespace OpenGitBase.Api;

public static class FeatureRegistration
{
    public static IReadOnlyList<Assembly> GetFeatureAssemblies() =>
        [
            typeof(OpenGitBase.Features.Users.UsersMapsterConfig).Assembly,
            typeof(OpenGitBase.Features.PublicGitSshKey.PublicGitSshKeyMapsterConfig).Assembly,
            typeof(OpenGitBase.Features.Repository.RepositoryMapsterConfig).Assembly,
            typeof(OpenGitBase.Features.RepositoryMember.RepositoryMemberMapsterConfig).Assembly,
            typeof(OpenGitBase.Features.Organization.OrganizationMapsterConfig).Assembly,
                    typeof(global::OpenGitBase.Features.StorageNode.StorageNodeMapsterConfig).Assembly,
        typeof(global::OpenGitBase.Features.GitAccessToken.GitAccessTokenMapsterConfig).Assembly,
        typeof(global::OpenGitBase.Features.Discussion.DiscussionMapsterConfig).Assembly,
// agentGenCli:feature-assemblies
        ];
}
