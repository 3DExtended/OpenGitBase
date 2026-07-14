using System.Net;
using OpenGitBase.Cli.Tests.TestSupport;

namespace OpenGitBase.Cli.Tests;

public sealed class IssueLinkCommandTests
{
    [Fact]
    public async Task Issue_link_posts_parent_relationship()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            """
            {"targetDiscussionNumber":42,"relationshipType":"parent","targetDiscussionTitle":"[PRD] Spec","targetDiscussionStatus":"Open"}
            """);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CliApp.RunAsync(
            [
                "--hostname", "https://forge.example.com",
                "issue", "-R", "acme/demo", "link", "43", "--parent", "42",
            ],
            output,
            error,
            CliTestSupport.CreateOverrides(handler, "https://forge.example.com"));

        Assert.Equal(0, exitCode);
        Assert.Contains("/discussions/43/links", handler.Requests.Single().RequestUri!.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains("parent", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Issue_links_lists_outgoing_links()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            """
            [{"targetDiscussionNumber":42,"relationshipType":"parent","targetDiscussionTitle":"[PRD] Spec","targetDiscussionStatus":"Open"}]
            """);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CliApp.RunAsync(
            ["--hostname", "https://forge.example.com", "issue", "-R", "acme/demo", "links", "43"],
            output,
            error,
            CliTestSupport.CreateOverrides(handler, "https://forge.example.com"));

        Assert.Equal(0, exitCode);
        Assert.Contains("42", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_unlink_deletes_link_with_relationship_query()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.NoContent, string.Empty);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CliApp.RunAsync(
            [
                "--hostname", "https://forge.example.com",
                "issue", "-R", "acme/demo", "unlink", "43", "--related", "42",
            ],
            output,
            error,
            CliTestSupport.CreateOverrides(handler, "https://forge.example.com"));

        Assert.Equal(0, exitCode);
        Assert.Contains(
            "/discussions/43/links/42?relationshipType=related",
            handler.Requests.Single().RequestUri!.PathAndQuery,
            StringComparison.Ordinal);
    }
}
