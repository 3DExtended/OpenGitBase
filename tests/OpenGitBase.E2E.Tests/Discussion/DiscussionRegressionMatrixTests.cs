using System.Text.Json;
using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.Discussion;

[Collection("Compose")]
[Trait("Category", "Discussion")]
[Trait("RequiresCompose", "true")]
[E2eTier(4)]
public class DiscussionRegressionMatrixTests : AuthMatrixTheoryBase
{
    public static IEnumerable<object[]> DiscussionMatrixCases() =>
        DiscussionRegressionMatrix.BuildCases().Select(c => new object[] { c });

    [RequiresComposeTheory]
    [MemberData(nameof(DiscussionMatrixCases))]
    [Trait("Tag", "Regression")]
    public async Task DiscussionEndpointsMatrix(AuthMatrixCase matrixCase)
    {
        var identity = new IdentityFixture(Context, Transcript);
        var repos = new RepositoryFixture(Transcript, Context.Normalizer);
        var owner = await identity.RegisterUserAsync($"disc-reg-owner-{Context.RunSuffix}").ConfigureAwait(false);
        var writer = await identity.RegisterUserAsync($"disc-reg-writer-{Context.RunSuffix}").ConfigureAwait(false);
        var outsider = await identity.RegisterUserAsync($"disc-reg-outsider-{Context.RunSuffix}").ConfigureAwait(false);
        var repository = await repos.CreateAsync(owner, $"disc-reg-{Context.RunSuffix}", "Discussion Regression", isPrivate: false)
            .ConfigureAwait(false);
        await repos.AddMemberAsync(owner, repository.RepositoryId, writer.UserId, role: 2).ConfigureAwait(false);

        var seedDiscussion = await owner.Client.PostAsync(
            $"/repository/by-slug/{owner.Username}/{repository.Slug}/discussions",
            new { title = "seed discussion", body = "seed body" }).ConfigureAwait(false);
        var discussionNumber = ParseInt(seedDiscussion.Body, "number");

        var resolved = matrixCase with
        {
            RelativeUrl = matrixCase.RelativeUrl
                .Replace("{{OWNER}}", owner.Username, StringComparison.Ordinal)
                .Replace("{{SLUG}}", repository.Slug, StringComparison.Ordinal)
                .Replace("{{DISCUSSION_NUMBER}}", discussionNumber.ToString(), StringComparison.Ordinal),
        };

        await RunMatrixCaseAsync(
            resolved,
            new AuthMatrixContext
            {
                Anonymous = new E2eApiClient(Transcript, Context.Normalizer),
                Owner = owner,
                Writer = writer,
                Outsider = outsider,
                Reader = writer,
            }).ConfigureAwait(false);
    }

    private static int ParseInt(string body, string propertyName)
    {
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty(propertyName).GetInt32();
    }
}

internal static class DiscussionRegressionMatrix
{
    public static IReadOnlyList<AuthMatrixCase> BuildCases()
    {
        var cases = new List<AuthMatrixCase>
        {
            Row("E2E-POP21-001", AuthMatrixActor.Owner, HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/discussions", null, 200, "Owner can list discussions", "owner-list"),
            Row("E2E-POP21-002", AuthMatrixActor.Anonymous, HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/discussions", null, 200, "Anonymous can list public discussions", "anon-list"),
            Row("E2E-POP21-003", AuthMatrixActor.Owner, HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/discussions/{{DISCUSSION_NUMBER}}", null, 200, "Owner can get discussion", "owner-get"),
            Row("E2E-POP21-004", AuthMatrixActor.Outsider, HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/discussions/{{DISCUSSION_NUMBER}}?include=comments", null, 200, "Outsider can get public discussion with comments", "outsider-get-comments"),
            Row("E2E-POP21-005", AuthMatrixActor.Anonymous, HttpMethod.Post, "/repository/by-slug/{{OWNER}}/{{SLUG}}/discussions", new { title = "anon create", body = "denied" }, 401, "Anonymous cannot create discussion", "anon-create-denied"),
            Row("E2E-POP21-006", AuthMatrixActor.Writer, HttpMethod.Post, "/repository/by-slug/{{OWNER}}/{{SLUG}}/discussions/{{DISCUSSION_NUMBER}}/comments", new { bodyMarkdown = "writer comment" }, 200, "Writer can comment", "writer-comment"),
            Row("E2E-POP21-007", AuthMatrixActor.Owner, HttpMethod.Post, "/repository/by-slug/{{OWNER}}/{{SLUG}}/discussions/{{DISCUSSION_NUMBER}}/resolve", null, 200, "Owner can resolve discussion", "owner-resolve"),
            Row("E2E-POP21-008", AuthMatrixActor.Writer, HttpMethod.Post, "/repository/by-slug/{{OWNER}}/{{SLUG}}/discussions/{{DISCUSSION_NUMBER}}/dismiss", null, 200, "Writer can dismiss discussion", "writer-dismiss"),
            Row("E2E-POP21-009", AuthMatrixActor.Owner, HttpMethod.Get, "/notifications?unreadOnly=true", null, 200, "Owner can list notifications", "owner-notifications"),
            Row("E2E-POP21-010", AuthMatrixActor.Anonymous, HttpMethod.Get, "/notifications?unreadOnly=true", null, 401, "Anonymous cannot list notifications", "anon-notifications"),
        };

        var probes = new (HttpMethod Method, string Url, int Owner, int Writer, int Outsider, int Anonymous, object? Body, string Intent)[]
        {
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/discussions", 200, 200, 200, 200, null, "list discussions"),
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/discussions/{{DISCUSSION_NUMBER}}", 200, 200, 200, 200, null, "get discussion"),
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/discussions/{{DISCUSSION_NUMBER}}/comments", 200, 200, 200, 200, null, "list comments"),
            (HttpMethod.Post, "/repository/by-slug/{{OWNER}}/{{SLUG}}/discussions", 201, 201, 201, 401, new { title = "matrix", body = "create" }, "create discussion"),
            (HttpMethod.Patch, "/repository/by-slug/{{OWNER}}/{{SLUG}}/discussions/{{DISCUSSION_NUMBER}}", 200, 200, 403, 401, new { title = "updated from matrix" }, "patch discussion"),
            (HttpMethod.Post, "/repository/by-slug/{{OWNER}}/{{SLUG}}/discussions/{{DISCUSSION_NUMBER}}/comments", 200, 200, 200, 401, new { bodyMarkdown = "matrix comment" }, "create comment"),
            (HttpMethod.Post, "/repository/by-slug/{{OWNER}}/{{SLUG}}/discussions/{{DISCUSSION_NUMBER}}/resolve", 200, 200, 200, 401, null, "resolve discussion"),
            (HttpMethod.Post, "/repository/by-slug/{{OWNER}}/{{SLUG}}/discussions/{{DISCUSSION_NUMBER}}/dismiss", 200, 200, 200, 401, null, "dismiss discussion"),
            (HttpMethod.Post, "/repository/by-slug/{{OWNER}}/{{SLUG}}/discussions/{{DISCUSSION_NUMBER}}/unsubscribe", 204, 204, 204, 401, null, "unsubscribe discussion"),
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/tags", 200, 200, 200, 200, null, "list tags"),
            (HttpMethod.Post, "/repository/by-slug/{{OWNER}}/{{SLUG}}/tags", 200, 200, 403, 401, new { name = "regression-tag", color = "#00ffaa" }, "create tag"),
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/blocked-users", 200, 200, 403, 401, null, "list blocked users"),
            (HttpMethod.Post, "/repository/by-slug/{{OWNER}}/{{SLUG}}/blocked-users", 200, 200, 403, 401, new { userId = "00000000-0000-0000-0000-000000000000", reason = "matrix" }, "block user"),
            (HttpMethod.Get, "/notifications?unreadOnly=true", 200, 200, 200, 401, null, "list notifications"),
        };

        var actors = new[]
        {
            new { Actor = AuthMatrixActor.Owner, Label = "owner", Status = (Func<(int, int, int, int), int>)(x => x.Item1) },
            new { Actor = AuthMatrixActor.Writer, Label = "writer", Status = (Func<(int, int, int, int), int>)(x => x.Item2) },
            new { Actor = AuthMatrixActor.Outsider, Label = "outsider", Status = (Func<(int, int, int, int), int>)(x => x.Item3) },
            new { Actor = AuthMatrixActor.Anonymous, Label = "anonymous", Status = (Func<(int, int, int, int), int>)(x => x.Item4) },
        };

        var id = 11;
        foreach (var probe in probes)
        {
            var statuses = (probe.Owner, probe.Writer, probe.Outsider, probe.Anonymous);
            foreach (var actor in actors)
            {
                cases.Add(Row(
                    $"E2E-POP21-{id:D3}",
                    actor.Actor,
                    probe.Method,
                    probe.Url,
                    probe.Body,
                    actor.Status(statuses),
                    $"{actor.Label} {probe.Intent}",
                    $"{actor.Label}-discussion-probe-{id}"));
                id++;
            }
        }

        return cases;
    }

    private static AuthMatrixCase Row(
        string id,
        AuthMatrixActor actor,
        HttpMethod method,
        string url,
        object? body,
        int status,
        string intent,
        string baseline) =>
        new(id, actor, method, url, body, status, intent, baseline);
}
