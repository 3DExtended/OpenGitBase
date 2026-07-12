namespace OpenGitBase.Cli.Git;

public interface IGitRemoteResolver
{
    bool TryResolveFromWorkingDirectory(string? workingDirectory, out RepoSlug slug);

    bool TryParseRepoOption(string? repoOption, out RepoSlug slug);

    bool TryParseRemoteUrl(string remoteUrl, out RepoSlug slug);
}
