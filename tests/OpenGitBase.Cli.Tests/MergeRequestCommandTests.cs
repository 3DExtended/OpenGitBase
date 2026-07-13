using System.Net;
using OpenGitBase.Cli.Git;
using OpenGitBase.Cli.Tests.TestSupport;

namespace OpenGitBase.Cli.Tests;

public sealed class MergeRequestCommandTests : IDisposable
{
    private readonly List<string> _tempPaths = [];

    public void Dispose()
    {
        foreach (var path in _tempPaths)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Mr_list_renders_table_rows()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            """
            [{"id":"11111111-1111-1111-1111-111111111111","number":1,"title":"Add feature","status":1,"isDraft":false,"sourceRef":"feature","targetRef":"main","sourceHeadSha":"abc","targetBaseSha":"def","createdAt":"2026-07-12T10:00:00Z","updatedAt":"2026-07-12T11:00:00Z","approvalCountAtHead":0,"requiredApprovalCount":1}]
            """);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CliApp.RunAsync(
            ["--hostname", "https://forge.example.com", "mr", "-R", "acme/demo", "list"],
            output,
            error,
            CliTestSupport.CreateOverrides(handler, "https://forge.example.com")).ConfigureAwait(false);

        Assert.Equal(0, exitCode);
        var text = output.ToString();
        Assert.Contains("Add feature", text, StringComparison.Ordinal);
        Assert.Contains("feature→main", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mr_list_status_filter_adds_query_param()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, "[]");

        using var output = new StringWriter();
        using var error = new StringWriter();
        await CliApp.RunAsync(
            ["--hostname", "https://forge.example.com", "mr", "-R", "acme/demo", "list", "--status", "open"],
            output,
            error,
            CliTestSupport.CreateOverrides(handler, "https://forge.example.com")).ConfigureAwait(false);

        Assert.Contains("status=open", handler.Requests.Single().RequestUri!.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Mr_view_includes_commits_when_requested()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            """
            {"id":"11111111-1111-1111-1111-111111111111","number":7,"title":"Feature","status":1,"isDraft":false,"sourceRef":"feature","targetRef":"main","sourceHeadSha":"abc","targetBaseSha":"def","creatorUsername":"alice","createdAt":"2026-07-12T10:00:00Z","updatedAt":"2026-07-12T10:00:00Z","approvalCountAtHead":0,"requiredApprovalCount":1}
            """);
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            """
            [{"sha":"abc123","shortSha":"abc123","message":"feature commit","authorName":"alice","authoredAt":"2026-07-12T10:00:00Z"}]
            """);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CliApp.RunAsync(
            ["--hostname", "https://forge.example.com", "mr", "-R", "acme/demo", "view", "7", "--commits"],
            output,
            error,
            CliTestSupport.CreateOverrides(handler, "https://forge.example.com")).ConfigureAwait(false);

        Assert.Equal(0, exitCode);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("/commits", handler.Requests[1].RequestUri!.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains("feature commit", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mr_status_prints_state_and_mergeability()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            """
            {"id":"11111111-1111-1111-1111-111111111111","number":7,"title":"Feature","status":2,"isDraft":false,"sourceRef":"feature","targetRef":"main","sourceHeadSha":"abc","targetBaseSha":"def","createdAt":"2026-07-12T10:00:00Z","updatedAt":"2026-07-12T10:00:00Z","approvalCountAtHead":1,"requiredApprovalCount":1}
            """);
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            """{"status":"Mergeable","message":"Ready to merge."}""");

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CliApp.RunAsync(
            ["--hostname", "https://forge.example.com", "mr", "-R", "acme/demo", "status", "7"],
            output,
            error,
            CliTestSupport.CreateOverrides(handler, "https://forge.example.com")).ConfigureAwait(false);

        Assert.Equal(0, exitCode);
        var text = output.ToString();
        Assert.Contains("Approved", text, StringComparison.Ordinal);
        Assert.Contains("Mergeable", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mr_create_uses_branch_resolver_for_default_head()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            """{"aheadCount":1,"defaultRef":"main","hasActiveMergeRequest":false}""");
        handler.EnqueueResponse(
            HttpStatusCode.Created,
            """
            {"id":"11111111-1111-1111-1111-111111111111","number":2,"title":"Feature","status":1,"isDraft":false,"sourceRef":"feature/foo","targetRef":"main","sourceHeadSha":"abc","targetBaseSha":"def","createdAt":"2026-07-12T10:00:00Z","updatedAt":"2026-07-12T10:00:00Z","approvalCountAtHead":0,"requiredApprovalCount":1}
            """);

        var repoDir = CliTestSupport.CreateGitRepoWithOrigin("https://forge.example.com", "acme", "demo");
        _tempPaths.Add(repoDir);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var baseOverrides = CliTestSupport.CreateOverrides(handler, "https://forge.example.com", repoDir);
        var overrides = new CliServiceOverrides
        {
            CredentialStore = baseOverrides.CredentialStore,
            HttpClient = baseOverrides.HttpClient,
            WorkingDirectory = baseOverrides.WorkingDirectory,
            GitBranchResolver = new FakeGitBranchResolver("feature/foo"),
        };

        var exitCode = await CliApp.RunAsync(
            ["--hostname", "https://forge.example.com", "mr", "-R", "acme/demo", "create", "--title", "Feature"],
            output,
            error,
            overrides).ConfigureAwait(false);

        Assert.Equal(0, exitCode);
        Assert.Contains("#2", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("branch-ahead-summary", handler.Requests[0].RequestUri!.PathAndQuery, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mr_close_confirms_closed_status()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            """
            {"id":"11111111-1111-1111-1111-111111111111","number":7,"title":"Feature","status":4,"isDraft":false,"sourceRef":"feature","targetRef":"main","sourceHeadSha":"abc","targetBaseSha":"def","createdAt":"2026-07-12T10:00:00Z","updatedAt":"2026-07-12T10:00:00Z","approvalCountAtHead":0,"requiredApprovalCount":1}
            """);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CliApp.RunAsync(
            ["--hostname", "https://forge.example.com", "mr", "-R", "acme/demo", "close", "7"],
            output,
            error,
            CliTestSupport.CreateOverrides(handler, "https://forge.example.com")).ConfigureAwait(false);

        Assert.Equal(0, exitCode);
        Assert.Contains("Closed", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("/close", handler.Requests.Single().RequestUri!.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mr_merge_posts_strategy_body()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            """
            {"id":"11111111-1111-1111-1111-111111111111","number":7,"title":"Feature","status":3,"isDraft":false,"sourceRef":"feature","targetRef":"main","sourceHeadSha":"abc","targetBaseSha":"def","mergeCommitSha":"deadbeef","createdAt":"2026-07-12T10:00:00Z","updatedAt":"2026-07-12T10:00:00Z","approvalCountAtHead":1,"requiredApprovalCount":1}
            """);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CliApp.RunAsync(
            [
                "--hostname", "https://forge.example.com",
                "mr", "-R", "acme/demo", "merge", "7", "--strategy", "squash", "--delete-branch",
            ],
            output,
            error,
            CliTestSupport.CreateOverrides(handler, "https://forge.example.com")).ConfigureAwait(false);

        Assert.Equal(0, exitCode);
        Assert.Contains("merged", output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/merge", handler.Requests.Single().RequestUri!.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mr_help_lists_subcommands()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CliApp.RunAsync(["mr", "--help"], output, error).ConfigureAwait(false);

        Assert.Equal(0, exitCode);
        var text = output.ToString();
        Assert.Contains("list", text, StringComparison.Ordinal);
        Assert.Contains("create", text, StringComparison.Ordinal);
        Assert.Contains("merge", text, StringComparison.Ordinal);
    }

    private sealed class FakeGitBranchResolver(string branch) : IGitBranchResolver
    {
        public bool TryGetCurrentBranch(string? workingDirectory, out string branchName)
        {
            branchName = branch;
            return true;
        }
    }
}
