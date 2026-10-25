namespace OpenGitBase.Dispatcher.Tests.RealGit;

/// <summary>
/// 2026-10-23, Tower (git 2.55.0): `git fetch` of admin/uhs-cockpit failed every time with
/// "fatal: expected 'acknowledgments'" (protocol v1: "the remote end hung up unexpectedly"),
/// while `git -c fetch.negotiationAlgorithm=noop fetch` and `git fetch origin main` worked. The clone
/// lacked an annotated tag on an older commit and was two commits behind. Git gzips an upload-pack
/// request above 1 KiB; the tag's want plus 16 haves took this one over that line.
/// </summary>
[Trait("Category", "GitHttps")]
public sealed class GitFetchThroughDispatcherTests : IAsyncLifetime
{
    private const int HistoryLength = 40;

    private RealGitHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await RealGitHarness.StartAsync();

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    /// <summary>
    /// Two tags, not one: git 2.43 (Ubuntu 24.04, the CI image) sends a shorter agent string than 2.55
    /// ("git/2.43.0" against "git/2.55.0-Linux"), and a single tag's v2 request then stays just under
    /// the gzip line. The tags sit more than 16 commits back, so the first round carries 16 haves, as
    /// uhs-cockpit's did.
    /// </summary>
    [Theory]
    [InlineData("2")]
    [InlineData("1")]
    public async Task Fetch_OfNewTagsOnOldCommitsAndNewCommits_Succeeds(string protocolVersion)
    {
        var (upstream, client) = await CloneBehindAsync();
        var git = _harness.Git;
        await git.RunCheckedAsync(upstream, "tag", "-a", "sdk-v0.1.0", "-m", "sdk-v0.1.0", "HEAD~25");
        await git.RunCheckedAsync(upstream, "tag", "-a", "sdk-v0.0.9", "-m", "sdk-v0.0.9", "HEAD~30");
        await CommitAsync(upstream, 2);
        await git.RunCheckedAsync(upstream, "push", _harness.BareRepository, "main", "--tags");

        var fetch = await git.RunAsync(client, "-c", $"protocol.version={protocolVersion}", "fetch", "origin");

        Assert.True(fetch.ExitCode == 0, $"git fetch exited {fetch.ExitCode}: {fetch.StdErr}");
        foreach (var tag in new[] { "refs/tags/sdk-v0.1.0", "refs/tags/sdk-v0.0.9" })
        {
            Assert.Equal(
                await git.RunCheckedAsync(upstream, "rev-parse", tag),
                await git.RunCheckedAsync(client, "rev-parse", tag)
            );
        }

        Assert.Equal(
            await git.RunCheckedAsync(upstream, "rev-parse", "main"),
            await git.RunCheckedAsync(client, "rev-parse", "refs/remotes/origin/main")
        );
        AssertTheClientCompressedARequest();
    }

    /// <summary>
    /// Negotiation past commits the server does not have: from the second round on, every
    /// request carries more than 1 KiB of haves, tag or no tag.
    /// </summary>
    [Fact]
    public async Task Fetch_WithLocalOnlyCommits_Succeeds()
    {
        var (upstream, client) = await CloneBehindAsync();
        var git = _harness.Git;
        await git.RunCheckedAsync(client, "switch", "-c", "local-work");
        await CommitAsync(client, 40);
        await CommitAsync(upstream, 2);
        await git.RunCheckedAsync(upstream, "push", _harness.BareRepository, "main");

        var fetch = await git.RunAsync(client, "fetch", "origin", "main");

        Assert.True(fetch.ExitCode == 0, $"git fetch exited {fetch.ExitCode}: {fetch.StdErr}");
        Assert.Equal(
            await git.RunCheckedAsync(upstream, "rev-parse", "main"),
            await git.RunCheckedAsync(client, "rev-parse", "refs/remotes/origin/main")
        );
        AssertTheClientCompressedARequest();
    }

    /// <summary>Git gzips an upload-pack request above 1 KiB; without one the test proves nothing.</summary>
    private void AssertTheClientCompressedARequest() =>
        Assert.Contains("gzip", _harness.Dispatcher.UploadPackContentEncodings);

    /// <summary>A server repository with history and a clone of it through the dispatcher.</summary>
    private async Task<(string Upstream, string Client)> CloneBehindAsync()
    {
        var git = _harness.Git;
        var upstream = Path.Combine(_harness.Root, "upstream");
        var client = Path.Combine(_harness.Root, "client");
        Directory.CreateDirectory(upstream);
        await git.RunCheckedAsync(_harness.Root, "init", "--bare", _harness.BareRepository);
        await git.RunCheckedAsync(upstream, "init");
        await CommitAsync(upstream, HistoryLength);
        await git.RunCheckedAsync(upstream, "push", _harness.BareRepository, "main");
        await git.RunCheckedAsync(_harness.Root, "clone", _harness.RemoteUrl, client);
        return (upstream, client);
    }

    private async Task CommitAsync(string repository, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var file = Path.Combine(repository, "notes.txt");
            await File.AppendAllTextAsync(file, $"{Guid.NewGuid():N}\n");
            await _harness.Git.RunCheckedAsync(repository, "add", "notes.txt");
            await _harness.Git.RunCheckedAsync(repository, "commit", "-q", "-m", $"change {Guid.NewGuid():N}");
        }
    }
}
