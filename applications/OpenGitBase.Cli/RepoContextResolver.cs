using OpenGitBase.Cli.Configuration;
using OpenGitBase.Cli.Git;
using OpenGitBase.Cli.Output;

namespace OpenGitBase.Cli;

public static class RepoContextResolver
{
    public static RepoSlug ResolveRepo(CliServices services)
    {
        if (!string.IsNullOrWhiteSpace(services.RepoOverride)
            && services.GitRemoteResolver.TryParseRepoOption(services.RepoOverride, out var explicitRepo))
        {
            return explicitRepo;
        }

        if (services.GitRemoteResolver.TryResolveFromWorkingDirectory(services.WorkingDirectory, out var inferredRepo))
        {
            return inferredRepo;
        }

        throw new InvalidOperationException(
            "Could not determine repository context. Run inside a git clone or pass -R owner/repo.");
    }

    public static string BuildDiscussionUrl(string host, RepoSlug repo, int number) =>
        $"{host.TrimEnd('/')}/{repo.Owner}/{repo.Slug}/discussions/{number}";
}
