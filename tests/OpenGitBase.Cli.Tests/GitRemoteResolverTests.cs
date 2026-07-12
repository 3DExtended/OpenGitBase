using OpenGitBase.Cli.Git;

namespace OpenGitBase.Cli.Tests;

public sealed class GitRemoteResolverTests
{
    private readonly GitRemoteResolver _resolver = new();

    [Theory]
    [InlineData("https://github.example.com/acme/widget.git", "acme", "widget")]
    [InlineData("https://github.example.com/acme/widget", "acme", "widget")]
    [InlineData("git@github.example.com:acme/widget.git", "acme", "widget")]
    [InlineData("ssh://git@github.example.com/acme/widget", "acme", "widget")]
    public void Parses_remote_urls(string remoteUrl, string owner, string slug)
    {
        Assert.True(_resolver.TryParseRemoteUrl(remoteUrl, out var repo));
        Assert.Equal(owner, repo.Owner);
        Assert.Equal(slug, repo.Slug);
    }

    [Fact]
    public void Repo_option_takes_precedence_over_git_remote()
    {
        Assert.True(_resolver.TryParseRepoOption("other/repo", out var repo));
        Assert.Equal("other", repo.Owner);
        Assert.Equal("repo", repo.Slug);
    }

    [Fact]
    public void Resolves_origin_from_working_directory()
    {
        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var gitDir = Directory.CreateDirectory(Path.Combine(tempDir.FullName, ".git"));
        File.WriteAllText(
            Path.Combine(gitDir.FullName, "config"),
            """
            [remote "origin"]
                url = https://forge.example.com/acme/demo.git
            """);

        Assert.True(_resolver.TryResolveFromWorkingDirectory(tempDir.FullName, out var repo));
        Assert.Equal("acme", repo.Owner);
        Assert.Equal("demo", repo.Slug);
    }
}
