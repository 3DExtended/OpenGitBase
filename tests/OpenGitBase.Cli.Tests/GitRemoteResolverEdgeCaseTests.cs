using OpenGitBase.Cli.Git;

namespace OpenGitBase.Cli.Tests;

public sealed class GitRemoteResolverEdgeCaseTests : IDisposable
{
    private readonly List<string> _paths = [];

    public void Dispose()
    {
        foreach (var path in _paths)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    [Fact]
    public void Returns_false_when_no_git_directory()
    {
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        _paths.Add(dir.FullName);

        var resolver = new GitRemoteResolver();
        Assert.False(resolver.TryResolveFromWorkingDirectory(dir.FullName, out _));
    }

    [Fact]
    public void Parses_scp_style_ssh_remote()
    {
        var dir = CreateRepoWithOrigin("git@github.com:acme/demo");
        var resolver = new GitRemoteResolver();
        Assert.True(resolver.TryResolveFromWorkingDirectory(dir, out var slug));
        Assert.Equal("acme", slug.Owner);
        Assert.Equal("demo", slug.Slug);
    }

    [Fact]
    public void Repo_option_parses_owner_and_slug()
    {
        var resolver = new GitRemoteResolver();
        Assert.True(resolver.TryParseRepoOption("acme/demo", out var slug));
        Assert.Equal("acme", slug.Owner);
        Assert.Equal("demo", slug.Slug);
    }

    private string CreateRepoWithOrigin(string originUrl)
    {
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        _paths.Add(dir.FullName);
        var gitDir = Path.Combine(dir.FullName, ".git");
        Directory.CreateDirectory(gitDir);
        File.WriteAllText(
            Path.Combine(gitDir, "config"),
            $"""
            [remote "origin"]
                url = {originUrl}
                fetch = +refs/heads/*:refs/remotes/origin/*
            """);
        return dir.FullName;
    }
}
