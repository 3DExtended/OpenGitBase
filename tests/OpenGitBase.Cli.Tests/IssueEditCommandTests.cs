using System.Net;
using System.Text.Json;
using OpenGitBase.Cli.Tests.TestSupport;

namespace OpenGitBase.Cli.Tests;

public sealed class IssueEditCommandTests
{
    private const string Host = "https://forge.example.com";
    private const string DiscussionsPath = "/api/repository/by-slug/acme/demo/discussions";

    private static readonly DateTimeOffset CreatedAt = new(2026, 7, 15, 12, 21, 42, TimeSpan.Zero);
    private static readonly Guid OpeningId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid ContinuationId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid ThirdPartId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");

    [Fact]
    public async Task Issue_edit_patches_the_opening_comment_of_a_one_part_spec()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, DiscussionJson("Old spec", Comment(OpeningId, "Old spec", 1)));
        handler.EnqueueResponse(HttpStatusCode.OK, CommentResponseJson(OpeningId, "New spec"));

        var (exitCode, output, error) = await RunAsync(handler, "issue", "-R", "acme/demo", "edit", "42", "--body", "New spec");

        Assert.True(exitCode == 0, error);
        Assert.Equal(
            [
                $"GET {DiscussionsPath}/42?include=comments",
                $"PATCH {DiscussionsPath}/comments/{OpeningId}",
            ],
            handler.Requests.Select(Describe));
        Assert.Equal("New spec", ReadJsonString(handler.RequestBodies.Single(), "bodyMarkdown"));
        Assert.Contains("#42", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_edit_patches_only_the_spec_parts_whose_text_changed()
    {
        var partOne = Paragraph('a', 8000);
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            DiscussionJson(
                partOne,
                Comment(OpeningId, partOne, 1),
                Comment(ContinuationId, "Old part two", 2)));
        handler.EnqueueResponse(HttpStatusCode.OK, CommentResponseJson(ContinuationId, "New part two"));
        var newPartTwo = Paragraph('c', 9000);

        var (exitCode, output, error) = await RunAsync(
            handler,
            "issue", "-R", "acme/demo", "edit", "42", "--body", $"{partOne}\n\n{newPartTwo}");

        Assert.True(exitCode == 0, error);
        Assert.Equal(
            [
                $"GET {DiscussionsPath}/42?include=comments",
                $"PATCH {DiscussionsPath}/comments/{ContinuationId}",
            ],
            handler.Requests.Select(Describe));
        Assert.Equal(newPartTwo, ReadJsonString(handler.RequestBodies.Single(), "bodyMarkdown"));
        Assert.Contains("1 of 2", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_edit_changes_nothing_when_the_spec_already_matches()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, DiscussionJson("Same spec", Comment(OpeningId, "Same spec", 1)));

        var (exitCode, output, error) = await RunAsync(handler, "issue", "-R", "acme/demo", "edit", "42", "--body", "Same spec\n");

        Assert.True(exitCode == 0, error);
        Assert.Single(handler.Requests);
        Assert.Contains("already matches", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_edit_soft_deletes_spec_comments_a_shorter_text_no_longer_needs()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            DiscussionJson(
                "Part one",
                Comment(OpeningId, "Part one", 1),
                Comment(ContinuationId, "Part two", 2),
                Comment(ThirdPartId, "Part three", 3)));
        handler.EnqueueResponse(HttpStatusCode.OK, CommentResponseJson(OpeningId, "Short spec"));
        handler.EnqueueResponse(HttpStatusCode.OK, CommentResponseJson(ContinuationId, "Part two"));
        handler.EnqueueResponse(HttpStatusCode.OK, CommentResponseJson(ThirdPartId, "Part three"));

        var (exitCode, output, error) = await RunAsync(handler, "issue", "-R", "acme/demo", "edit", "42", "--body", "Short spec");

        Assert.True(exitCode == 0, error);
        Assert.Equal(
            [
                $"GET {DiscussionsPath}/42?include=comments",
                $"PATCH {DiscussionsPath}/comments/{OpeningId}",
                $"DELETE {DiscussionsPath}/comments/{ContinuationId}",
                $"DELETE {DiscussionsPath}/comments/{ThirdPartId}",
            ],
            handler.Requests.Select(Describe));
        Assert.Contains("removed 2", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Issue_edit_lets_a_one_part_spec_grow_to_the_comment_limit()
    {
        var grown = Paragraph('a', 12000);
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, DiscussionJson("Old spec", Comment(OpeningId, "Old spec", 1)));
        handler.EnqueueResponse(HttpStatusCode.OK, CommentResponseJson(OpeningId, grown));

        var (exitCode, _, error) = await RunAsync(handler, "issue", "-R", "acme/demo", "edit", "42", "--body", grown);

        Assert.True(exitCode == 0, error);
        Assert.Equal(grown, ReadJsonString(handler.RequestBodies.Single(), "bodyMarkdown"));
    }

    [Fact]
    public async Task Issue_edit_fails_before_writing_when_the_text_needs_more_parts_than_the_spec_has()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, DiscussionJson("Old spec", Comment(OpeningId, "Old spec", 1)));

        var (exitCode, _, error) = await RunAsync(
            handler,
            "issue", "-R", "acme/demo", "edit", "42", "--body", $"{Paragraph('a', 7000)}\n\n{Paragraph('b', 12000)}");

        Assert.NotEqual(0, exitCode);
        Assert.Single(handler.Requests);
        Assert.Contains("needs 2 part(s)", error, StringComparison.Ordinal);
        Assert.Contains("#42 has 1", error, StringComparison.Ordinal);
        Assert.Contains("publish burst", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_edit_refuses_a_spec_someone_else_published()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            DiscussionJson("bob", "Bob's spec", Comment(OpeningId, "Bob's spec", 1, author: "bob")));

        var (exitCode, _, error) = await RunAsync(handler, "issue", "-R", "acme/demo", "edit", "42", "--body", "Mine now");

        Assert.NotEqual(0, exitCode);
        Assert.Single(handler.Requests);
        Assert.Contains("bob", error, StringComparison.Ordinal);
        Assert.Contains("alice", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_edit_requires_spec_text()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            DiscussionJson("Part one", Comment(OpeningId, "Part one", 1), Comment(ContinuationId, "Part two", 2)));

        var (exitCode, _, error) = await RunAsync(handler, "issue", "-R", "acme/demo", "edit", "42", "--body", "  \n ");

        Assert.NotEqual(0, exitCode);
        Assert.Single(handler.Requests);
        Assert.Contains("--body-file", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_edit_refuses_a_spec_whose_body_has_no_opening_comment()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, DiscussionJson("Summary", Comment(ContinuationId, "Details", 1)));

        var (exitCode, _, error) = await RunAsync(handler, "issue", "-R", "acme/demo", "edit", "42", "--body", "Summary, revised");

        Assert.NotEqual(0, exitCode);
        Assert.Single(handler.Requests);
        Assert.Contains("opening comment", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_edit_reports_progress_when_a_part_update_fails()
    {
        var partOne = Paragraph('a', 8000);
        var partTwo = Paragraph('b', 9000);
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            DiscussionJson(
                "Old part one",
                Comment(OpeningId, "Old part one", 1),
                Comment(ContinuationId, "Old part two", 2)));
        handler.EnqueueResponse(HttpStatusCode.OK, CommentResponseJson(OpeningId, partOne));
        handler.EnqueueResponse(HttpStatusCode.InternalServerError, """{"error":"Boom"}""");

        var (exitCode, _, error) = await RunAsync(
            handler,
            "issue", "-R", "acme/demo", "edit", "42", "--body", $"{partOne}\n\n{partTwo}");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("part 2 of 2", error, StringComparison.Ordinal);
        Assert.Contains("Boom", error, StringComparison.Ordinal);
        Assert.Contains("1 part(s) already updated", error, StringComparison.Ordinal);
        Assert.Contains("run the same command again", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_edit_reports_progress_when_removing_a_surplus_comment_fails()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            DiscussionJson("Part one", Comment(OpeningId, "Part one", 1), Comment(ContinuationId, "Part two", 2)));
        handler.EnqueueResponse(HttpStatusCode.OK, CommentResponseJson(OpeningId, "Short spec"));
        handler.EnqueueResponse(HttpStatusCode.InternalServerError, """{"error":"Boom"}""");

        var (exitCode, _, error) = await RunAsync(handler, "issue", "-R", "acme/demo", "edit", "42", "--body", "Short spec");

        Assert.NotEqual(0, exitCode);
        Assert.Contains(ContinuationId.ToString(), error, StringComparison.Ordinal);
        Assert.Contains("Boom", error, StringComparison.Ordinal);
        Assert.Contains("run the same command again", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_edit_json_matches_golden()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            DiscussionJson("Part one", Comment(OpeningId, "Part one", 1), Comment(ContinuationId, "Part two", 2)));
        handler.EnqueueResponse(HttpStatusCode.OK, CommentResponseJson(OpeningId, "Short spec"));
        handler.EnqueueResponse(HttpStatusCode.OK, CommentResponseJson(ContinuationId, "Part two"));

        var (exitCode, output, error) = await RunAsync(
            handler,
            "--json", "issue", "-R", "acme/demo", "edit", "42", "--body", "Short spec");

        Assert.True(exitCode == 0, error);
        var goldenPath = Path.Combine(AppContext.BaseDirectory, "Goldens", "issue-edit.json");
        Assert.Equal(
            Normalize(await File.ReadAllTextAsync(goldenPath)),
            Normalize(output));
    }

    [Fact]
    public async Task Issue_comment_edit_patches_the_comment_by_id()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, CommentResponseJson(ContinuationId, "Fixed note"));

        var (exitCode, output, error) = await RunAsync(
            handler,
            "issue", "-R", "acme/demo", "comment-edit", ContinuationId.ToString(), "--body", "Fixed note");

        Assert.True(exitCode == 0, error);
        Assert.Equal($"PATCH {DiscussionsPath}/comments/{ContinuationId}", Describe(handler.Requests.Single()));
        Assert.Equal("Fixed note", ReadJsonString(handler.RequestBodies.Single(), "bodyMarkdown"));
        Assert.Contains(ContinuationId.ToString(), output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_comment_edit_explains_that_only_the_author_can_edit()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.NotFound, string.Empty);

        var (exitCode, _, error) = await RunAsync(
            handler,
            "issue", "-R", "acme/demo", "comment-edit", ContinuationId.ToString(), "--body", "Fixed note");

        Assert.NotEqual(0, exitCode);
        Assert.Contains(ContinuationId.ToString(), error, StringComparison.Ordinal);
        Assert.Contains("only its author", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_comment_edit_rejects_text_over_the_comment_limit_before_sending()
    {
        var handler = new StubHttpMessageHandler();

        var (exitCode, _, error) = await RunAsync(
            handler,
            "issue", "-R", "acme/demo", "comment-edit", ContinuationId.ToString(), "--body", new string('x', 16001));

        Assert.NotEqual(0, exitCode);
        Assert.Empty(handler.Requests);
        Assert.Contains("16000", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_comment_edit_rejects_a_malformed_comment_id()
    {
        var handler = new StubHttpMessageHandler();

        var (exitCode, _, error) = await RunAsync(
            handler,
            "issue", "-R", "acme/demo", "comment-edit", "42", "--body", "Fixed note");

        Assert.NotEqual(0, exitCode);
        Assert.Empty(handler.Requests);
        Assert.Contains("42", error, StringComparison.Ordinal);
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunAsync(
        StubHttpMessageHandler handler,
        params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CliApp.RunAsync(
            ["--hostname", Host, .. args],
            output,
            error,
            CliTestSupport.CreateOverrides(handler, Host));
        return (exitCode, output.ToString(), error.ToString());
    }

    private static string Describe(HttpRequestMessage request) =>
        $"{request.Method} {request.RequestUri!.PathAndQuery}";

    private static string DiscussionJson(string? body, params object[] comments) =>
        DiscussionJson("alice", body, comments);

    private static string DiscussionJson(string creator, string? body, params object[] comments) =>
        JsonSerializer.Serialize(new
        {
            id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            number = 42,
            title = "[PRD] Spec",
            body,
            status = 0,
            creatorUsername = creator,
            createdAt = CreatedAt,
            updatedAt = CreatedAt,
            tags = Array.Empty<object>(),
            comments,
        });

    private static object Comment(Guid id, string body, int secondsAfterCreate, string author = "alice") =>
        new
        {
            id,
            authorUsername = author,
            bodyMarkdown = body,
            createdAt = CreatedAt.AddSeconds(secondsAfterCreate),
            updatedAt = CreatedAt.AddSeconds(secondsAfterCreate),
        };

    private static string CommentResponseJson(Guid id, string body) =>
        JsonSerializer.Serialize(new
        {
            id,
            authorUsername = "alice",
            bodyMarkdown = body,
            createdAt = CreatedAt.AddSeconds(1),
            updatedAt = CreatedAt.AddDays(1),
            editedAt = CreatedAt.AddDays(1),
        });

    private static string Paragraph(char letter, int length) =>
        string.Join(' ', Enumerable.Repeat(new string(letter, 9), length / 10));

    private static string Normalize(string json) =>
        JsonSerializer.Serialize(JsonDocument.Parse(json.Trim()), new JsonSerializerOptions { WriteIndented = true });

    private static string ReadJsonString(string json, string property)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty(property).GetString()!;
    }
}
