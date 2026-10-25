using System.Globalization;
using System.Net;
using System.Text.Json;
using OpenGitBase.Cli.Auth;
using OpenGitBase.Cli.Tests.TestSupport;

namespace OpenGitBase.Cli.Tests;

public sealed class IssueCommandExtendedTests : IDisposable
{
    private const string CommentJson = """
        {"id":"22222222-2222-2222-2222-222222222222","bodyMarkdown":"continued","createdAt":"2026-07-12T10:00:01Z","authorUsername":"alice"}
        """;

    private const string CreatedDiscussionJson = """
        {"id":"11111111-1111-1111-1111-111111111111","number":42,"title":"[PRD] Long","status":0,"createdAt":"2026-07-12T10:00:00Z","updatedAt":"2026-07-12T10:00:00Z","tags":[]}
        """;

    private static readonly DateTimeOffset DiscussionCreatedAt = DateTimeOffset.Parse("2026-07-12T10:00:00Z", CultureInfo.InvariantCulture);

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
    public async Task Issue_comment_posts_markdown_body()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            """
            {"id":"22222222-2222-2222-2222-222222222222","bodyMarkdown":"Hello","createdAt":"2026-07-12T10:00:00Z","authorUsername":"alice"}
            """);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CliApp.RunAsync(
            [
                "--hostname", "https://forge.example.com",
                "issue", "-R", "acme/demo", "comment", "42", "--body", "Hello",
            ],
            output,
            error,
            CliTestSupport.CreateOverrides(handler, "https://forge.example.com"));

        Assert.Equal(0, exitCode);
        Assert.Contains("/discussions/42/comments", handler.Requests.Single().RequestUri!.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_list_renders_table_rows()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            """
            [{"id":"11111111-1111-1111-1111-111111111111","number":1,"title":"One","status":0,"createdAt":"2026-07-12T10:00:00Z","updatedAt":"2026-07-12T11:00:00Z","tags":[]}]
            """);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CliApp.RunAsync(
            ["--hostname", "https://forge.example.com", "issue", "-R", "acme/demo", "list"],
            output,
            error,
            CliTestSupport.CreateOverrides(handler, "https://forge.example.com"));

        Assert.Equal(0, exitCode);
        var text = output.ToString();
        Assert.Contains("One", text, StringComparison.Ordinal);
        Assert.Contains("1", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_list_status_filter_adds_query_param()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, "[]");

        using var output = new StringWriter();
        using var error = new StringWriter();
        await CliApp.RunAsync(
            [
                "--hostname", "https://forge.example.com",
                "issue", "-R", "acme/demo", "list", "--status", "open",
            ],
            output,
            error,
            CliTestSupport.CreateOverrides(handler, "https://forge.example.com"));

        Assert.Contains("status=open", handler.Requests.Single().RequestUri!.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Issue_view_includes_comments()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            """
            {"id":"11111111-1111-1111-1111-111111111111","number":42,"title":"Bug","status":1,"createdAt":"2026-07-12T10:00:00Z","updatedAt":"2026-07-12T10:00:00Z","tags":[],"comments":[{"id":"22222222-2222-2222-2222-222222222222","bodyMarkdown":"note","createdAt":"2026-07-12T10:01:00Z","authorUsername":"alice"}]}
            """);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CliApp.RunAsync(
            ["--hostname", "https://forge.example.com", "issue", "-R", "acme/demo", "view", "42"],
            output,
            error,
            CliTestSupport.CreateOverrides(handler, "https://forge.example.com"));

        Assert.Equal(0, exitCode);
        Assert.Contains("include=comments", handler.Requests.Single().RequestUri!.Query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("note", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_status_prints_enum_only()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            """
            {"id":"11111111-1111-1111-1111-111111111111","number":42,"title":"Bug","status":2,"createdAt":"2026-07-12T10:00:00Z","updatedAt":"2026-07-12T10:00:00Z","tags":[]}
            """);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CliApp.RunAsync(
            ["--hostname", "https://forge.example.com", "issue", "-R", "acme/demo", "status", "42"],
            output,
            error,
            CliTestSupport.CreateOverrides(handler, "https://forge.example.com"));

        Assert.Equal(0, exitCode);
        Assert.Equal("Resolved" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public async Task Issue_create_body_file_reads_from_disk()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.Created,
            """
            {"id":"11111111-1111-1111-1111-111111111111","number":3,"title":"Bug","status":0,"createdAt":"2026-07-12T10:00:00Z","updatedAt":"2026-07-12T10:00:00Z","tags":[]}
            """);

        var bodyFile = Path.GetTempFileName();
        _tempPaths.Add(bodyFile);
        await File.WriteAllTextAsync(bodyFile, "From file");

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CliApp.RunAsync(
            [
                "--hostname", "https://forge.example.com",
                "issue", "-R", "acme/demo", "create", "--title", "Bug", "--body-file", bodyFile,
            ],
            output,
            error,
            CliTestSupport.CreateOverrides(handler, "https://forge.example.com"));

        Assert.Equal(0, exitCode);
        Assert.Contains("From file", handler.RequestBodies.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_create_infers_repo_from_git_origin()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.Created,
            """
            {"id":"11111111-1111-1111-1111-111111111111","number":9,"title":"Bug","status":0,"createdAt":"2026-07-12T10:00:00Z","updatedAt":"2026-07-12T10:00:00Z","tags":[]}
            """);

        var repoDir = CliTestSupport.CreateGitRepoWithOrigin("https://forge.example.com", "acme", "demo");
        _tempPaths.Add(repoDir);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var overrides = CliTestSupport.CreateOverrides(handler, "https://forge.example.com");
        overrides = new CliServiceOverrides
        {
            CredentialStore = overrides.CredentialStore,
            HttpClient = overrides.HttpClient,
            WorkingDirectory = repoDir,
        };

        var exitCode = await CliApp.RunAsync(
            ["--hostname", "https://forge.example.com", "issue", "create", "--title", "Bug"],
            output,
            error,
            overrides);

        Assert.Equal(0, exitCode);
        Assert.Contains("/acme/demo/discussions", handler.Requests.Single().RequestUri!.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Not_logged_in_exits_non_zero()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var handler = new StubHttpMessageHandler();
        var exitCode = await CliApp.RunAsync(
            [
                "--hostname", "https://forge.example.com",
                "issue", "-R", "acme/demo", "create", "--title", "Bug",
            ],
            output,
            error,
            new CliServiceOverrides { HttpClient = new HttpClient(handler), CredentialStore = new InMemoryCredentialStore() });

        Assert.NotEqual(0, exitCode);
        Assert.Empty(handler.Requests);
        Assert.Contains("auth login", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Session_expired_exits_non_zero_with_message()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.Unauthorized, """{"error":"Sign in required."}""");

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CliApp.RunAsync(
            ["--hostname", "https://forge.example.com", "issue", "-R", "acme/demo", "list"],
            output,
            error,
            CliTestSupport.CreateOverrides(handler, "https://forge.example.com"));

        Assert.NotEqual(0, exitCode);
        Assert.Contains("auth login", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Json_issue_create_emits_structured_output()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.Created,
            """
            {"id":"11111111-1111-1111-1111-111111111111","number":5,"title":"Bug","status":0,"createdAt":"2026-07-12T10:00:00Z","updatedAt":"2026-07-12T10:00:00Z","tags":[]}
            """);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CliApp.RunAsync(
            [
                "--json", "--hostname", "https://forge.example.com",
                "issue", "-R", "acme/demo", "create", "--title", "Bug",
            ],
            output,
            error,
            CliTestSupport.CreateOverrides(handler, "https://forge.example.com"));

        Assert.Equal(0, exitCode);
        Assert.Contains("\"number\":5", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"url\":", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Json_error_includes_http_status()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.Forbidden, """{"error":"Forbidden"}""");

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CliApp.RunAsync(
            [
                "--json", "--hostname", "https://forge.example.com",
                "issue", "-R", "acme/demo", "list",
            ],
            output,
            error,
            CliTestSupport.CreateOverrides(handler, "https://forge.example.com"));

        Assert.NotEqual(0, exitCode);
        Assert.Contains("\"httpStatus\":403", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_create_splits_long_body_into_continuation_comments()
    {
        var body = string.Join("\n\n", Enumerable.Range(1, 40).Select(index => $"## Part {index}\n\n{new string('x', 600)}"));
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.Created,
            """
            {"id":"11111111-1111-1111-1111-111111111111","number":42,"title":"[PRD] Long","status":0,"createdAt":"2026-07-12T10:00:00Z","updatedAt":"2026-07-12T10:00:00Z","tags":[]}
            """);
        handler.EnqueueResponse(HttpStatusCode.Created, CommentJson);
        handler.EnqueueResponse(HttpStatusCode.Created, CommentJson);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CliApp.RunAsync(
            [
                "--hostname", "https://forge.example.com",
                "issue", "-R", "acme/demo", "create", "--title", "[PRD] Long", "--body", body,
            ],
            output,
            error,
            CliTestSupport.CreateOverrides(handler, "https://forge.example.com"));

        Assert.Equal(0, exitCode);
        Assert.Equal(
            [
                "/api/repository/by-slug/acme/demo/discussions",
                "/api/repository/by-slug/acme/demo/discussions/42/comments",
                "/api/repository/by-slug/acme/demo/discussions/42/comments",
            ],
            handler.Requests.Select(request => request.RequestUri!.AbsolutePath));
        var openingBody = ReadJsonString(handler.RequestBodies[0], "body");
        var continuations = handler.RequestBodies.Skip(1).Select(json => ReadJsonString(json, "bodyMarkdown")).ToList();
        Assert.True(openingBody.Length <= DiscussionBodySplitter.MaxBodyLength);
        Assert.Equal(body, string.Join("\n\n", [openingBody, .. continuations]));
        Assert.Contains("2 continuation comment(s)", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_create_names_created_issue_when_continuation_comment_fails()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.Created,
            """
            {"id":"11111111-1111-1111-1111-111111111111","number":42,"title":"[PRD] Long","status":0,"createdAt":"2026-07-12T10:00:00Z","updatedAt":"2026-07-12T10:00:00Z","tags":[]}
            """);
        handler.EnqueueResponse(HttpStatusCode.InternalServerError, """{"error":"Boom"}""");
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            DiscussionWithCommentsJson(new string('a', 7000), SpecComment(new string('a', 7000), secondsAfterCreate: 1)));

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CliApp.RunAsync(
            [
                "--hostname", "https://forge.example.com",
                "issue", "-R", "acme/demo", "create", "--title", "[PRD] Long",
                "--body", $"{new string('a', 7000)}\n\n{new string('b', 7000)}",
            ],
            output,
            error,
            CliTestSupport.CreateOverrides(handler, "https://forge.example.com"));

        Assert.NotEqual(0, exitCode);
        Assert.Contains("#42", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("part 2 of 2", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(HttpMethod.Get, handler.Requests[^1].Method);
    }

    [Fact]
    public async Task Issue_create_carries_on_when_a_failed_continuation_comment_was_saved()
    {
        // Prod, 2026-10-18: the API saved body part 2 of 3 yet answered 500, and the publish stopped half-way.
        string[] parts = [new string('a', 7000), new string('b', 15000), new string('c', 5000)];
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.Created, CreatedDiscussionJson);
        handler.EnqueueResponse(HttpStatusCode.InternalServerError, """{"error":"Boom"}""");
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            DiscussionWithCommentsJson(parts[0], SpecComment(parts[0], secondsAfterCreate: 1), SpecComment(parts[1], secondsAfterCreate: 40)));
        handler.EnqueueResponse(HttpStatusCode.Created, CommentJson);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CliApp.RunAsync(
            [
                "--hostname", "https://forge.example.com",
                "issue", "-R", "acme/demo", "create", "--title", "[PRD] Long", "--body", string.Join("\n\n", parts),
            ],
            output,
            error,
            CliTestSupport.CreateOverrides(handler, "https://forge.example.com"));

        Assert.Equal(0, exitCode);
        Assert.Equal(
            [
                "POST /api/repository/by-slug/acme/demo/discussions",
                "POST /api/repository/by-slug/acme/demo/discussions/42/comments",
                "GET /api/repository/by-slug/acme/demo/discussions/42?include=comments",
                "POST /api/repository/by-slug/acme/demo/discussions/42/comments",
            ],
            handler.Requests.Select(request => $"{request.Method} {request.RequestUri!.PathAndQuery}"));
        Assert.Equal(parts[2], ReadJsonString(handler.RequestBodies[^1], "bodyMarkdown"));
        Assert.Contains("2 continuation comment(s)", output.ToString(), StringComparison.Ordinal);
    }

    private static string DiscussionWithCommentsJson(string body, params object[] comments) =>
        JsonSerializer.Serialize(new
        {
            id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            number = 42,
            title = "[PRD] Long",
            body,
            status = 0,
            creatorUsername = "alice",
            createdAt = DiscussionCreatedAt,
            updatedAt = DiscussionCreatedAt,
            tags = Array.Empty<object>(),
            comments,
        });

    private static object SpecComment(string body, int secondsAfterCreate) =>
        new
        {
            id = Guid.NewGuid(),
            authorUsername = "alice",
            bodyMarkdown = body,
            createdAt = DiscussionCreatedAt.AddSeconds(secondsAfterCreate),
            updatedAt = DiscussionCreatedAt.AddSeconds(secondsAfterCreate),
        };

    private static string ReadJsonString(string json, string property)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty(property).GetString()!;
    }
}
