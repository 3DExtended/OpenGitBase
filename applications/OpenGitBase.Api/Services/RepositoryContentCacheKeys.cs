namespace OpenGitBase.Api.Services;

public static class RepositoryContentCacheKeys
{
    public static string Build(
        Guid repositoryId,
        string endpoint,
        string refName,
        string path
    ) => $"repo-content:{repositoryId}:{endpoint}:{refName}:{path}";
}
