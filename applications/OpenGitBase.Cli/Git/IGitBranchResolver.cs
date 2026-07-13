namespace OpenGitBase.Cli.Git;

public interface IGitBranchResolver
{
    bool TryGetCurrentBranch(string? workingDirectory, out string branchName);
}
