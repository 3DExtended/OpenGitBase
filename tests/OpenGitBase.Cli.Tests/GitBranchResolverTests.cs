using OpenGitBase.Cli.Git;

namespace OpenGitBase.Cli.Tests;

public sealed class GitBranchResolverTests
{
    [Fact]
    public void TryGetCurrentBranch_returns_false_outside_git_repo()
    {
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ogb-no-git-" + Guid.NewGuid().ToString("N")));
        try
        {
            var resolver = new GitBranchResolver();
            Assert.False(resolver.TryGetCurrentBranch(dir.FullName, out _));
        }
        finally
        {
            Directory.Delete(dir.FullName, recursive: true);
        }
    }
}
