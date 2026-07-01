using System.Text.Json;
using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.MergeRequest;

[Collection("Compose")]
[Trait("Category", "MergeRequest")]
[Trait("RequiresCompose", "true")]
[E2eTier(6)]
public class MergeRequestRegressionMatrixTests : AuthMatrixTheoryBase
{
    public static IEnumerable<object[]> MergeRequestMatrixCases() =>
        MergeRequestRegressionMatrix.BuildCases().Select(c => new object[] { c });

    [RequiresComposeTheory]
    [MemberData(nameof(MergeRequestMatrixCases))]
    [Trait("Tag", "Regression")]
    public async Task MergeRequestEndpointsMatrix(AuthMatrixCase matrixCase)
    {
        var identity = new IdentityFixture(Context, Transcript);
        var mergeRequests = new MergeRequestFixture(Transcript, Context.Normalizer);
        var owner = await identity.RegisterUserAsync($"mr-reg-owner-{Context.RunSuffix}").ConfigureAwait(false);
        var writer = await identity.RegisterUserAsync($"mr-reg-writer-{Context.RunSuffix}").ConfigureAwait(false);
        var outsider = await identity.RegisterUserAsync($"mr-reg-outsider-{Context.RunSuffix}").ConfigureAwait(false);
        var seed = await mergeRequests.SeedMrReadyAsync(
            owner,
            writer,
            $"mr-reg-{Context.RunSuffix}",
            Path.Combine(Path.GetTempPath(), $"e2e-mr-reg-{Context.RunSuffix}"))
            .ConfigureAwait(false);

        var create = await owner.Client.PostAsync(seed.MergeRequestBase, new
        {
            title = "Matrix merge request",
            body = "Seeded for matrix endpoints",
            sourceRef = seed.FeatureBranch,
            targetRef = "main",
            isDraft = false,
        }).ConfigureAwait(false);
        var number = ParseInt(create.Body, "number");

        var resolved = matrixCase with
        {
            RelativeUrl = matrixCase.RelativeUrl
                .Replace("{{OWNER}}", owner.Username, StringComparison.Ordinal)
                .Replace("{{SLUG}}", seed.Repository.Slug, StringComparison.Ordinal)
                .Replace("{{MR_NUMBER}}", number.ToString(), StringComparison.Ordinal),
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

internal static class MergeRequestRegressionMatrix
{
    public static IReadOnlyList<AuthMatrixCase> BuildCases()
    {
        var cases = new List<AuthMatrixCase>
        {
            Row("E2E-POP22-001", AuthMatrixActor.Owner, HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/merge-requests", null, 200, "Owner lists merge requests", "owner-list"),
            Row("E2E-POP22-002", AuthMatrixActor.Anonymous, HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/merge-requests", null, 200, "Anonymous can list public merge requests", "anon-list"),
            Row("E2E-POP22-003", AuthMatrixActor.Owner, HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/merge-requests/{{MR_NUMBER}}", null, 200, "Owner gets merge request by number", "owner-get"),
            Row("E2E-POP22-004", AuthMatrixActor.Writer, HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/merge-requests/{{MR_NUMBER}}/mergeability", null, 200, "Writer gets mergeability", "writer-mergeability"),
            Row("E2E-POP22-005", AuthMatrixActor.Anonymous, HttpMethod.Post, "/repository/by-slug/{{OWNER}}/{{SLUG}}/merge-requests", CreateBody("anonymous"), 401, "Anonymous cannot create merge request", "anon-create-denied"),
            Row("E2E-POP22-006", AuthMatrixActor.Writer, HttpMethod.Post, "/repository/by-slug/{{OWNER}}/{{SLUG}}/merge-requests/{{MR_NUMBER}}/approve", null, 200, "Writer can approve merge request", "writer-approve"),
            Row("E2E-POP22-007", AuthMatrixActor.Owner, HttpMethod.Post, "/repository/by-slug/{{OWNER}}/{{SLUG}}/merge-requests/{{MR_NUMBER}}/refresh-shas", null, 200, "Owner can refresh SHAs", "owner-refresh-shas"),
            Row("E2E-POP22-008", AuthMatrixActor.Owner, HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/merge-requests/{{MR_NUMBER}}/discussion-links", null, 200, "Owner can list discussion links", "owner-discussion-links"),
            Row("E2E-POP22-009", AuthMatrixActor.Outsider, HttpMethod.Post, "/repository/by-slug/{{OWNER}}/{{SLUG}}/merge-requests/{{MR_NUMBER}}/merge", new { strategy = 0, deleteSourceBranch = false }, 403, "Outsider cannot merge", "outsider-merge-denied"),
            Row("E2E-POP22-010", AuthMatrixActor.Anonymous, HttpMethod.Post, "/repository/by-slug/{{OWNER}}/{{SLUG}}/merge-requests/{{MR_NUMBER}}/merge", new { strategy = 0, deleteSourceBranch = false }, 401, "Anonymous cannot merge", "anon-merge-denied"),
        };

        var probes = new (HttpMethod Method, string Url, int Owner, int Writer, int Outsider, int Anonymous, object? Body, string Intent)[]
        {
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/merge-requests", 200, 200, 200, 200, null, "list merge requests"),
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/merge-requests/{{MR_NUMBER}}", 200, 200, 200, 200, null, "get merge request"),
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/merge-requests/{{MR_NUMBER}}/mergeability", 200, 200, 200, 200, null, "get mergeability"),
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/merge-requests/{{MR_NUMBER}}/discussion-links", 200, 200, 200, 200, null, "list discussion links"),
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/merge-requests/branch-ahead-summary?refName=main", 200, 200, 200, 200, null, "branch ahead summary"),
            (HttpMethod.Post, "/repository/by-slug/{{OWNER}}/{{SLUG}}/merge-requests", 400, 400, 400, 401, CreateBody(string.Empty), "create invalid merge request"),
            (HttpMethod.Patch, "/repository/by-slug/{{OWNER}}/{{SLUG}}/merge-requests/{{MR_NUMBER}}", 200, 200, 403, 401, new { title = "Updated from matrix" }, "patch merge request"),
            (HttpMethod.Post, "/repository/by-slug/{{OWNER}}/{{SLUG}}/merge-requests/{{MR_NUMBER}}/publish", 200, 200, 403, 401, null, "publish merge request"),
            (HttpMethod.Post, "/repository/by-slug/{{OWNER}}/{{SLUG}}/merge-requests/{{MR_NUMBER}}/close", 200, 200, 403, 401, null, "close merge request"),
            (HttpMethod.Post, "/repository/by-slug/{{OWNER}}/{{SLUG}}/merge-requests/{{MR_NUMBER}}/approve", 403, 200, 403, 401, null, "approve merge request"),
            (HttpMethod.Post, "/repository/by-slug/{{OWNER}}/{{SLUG}}/merge-requests/{{MR_NUMBER}}/merge", 200, 403, 403, 401, new { strategy = 0, deleteSourceBranch = false }, "merge merge request"),
            (HttpMethod.Post, "/repository/by-slug/{{OWNER}}/{{SLUG}}/merge-requests/{{MR_NUMBER}}/refresh-shas", 200, 200, 200, 200, null, "refresh shas"),
            (HttpMethod.Post, "/repository/by-slug/{{OWNER}}/{{SLUG}}/merge-requests/{{MR_NUMBER}}/discussion-links", 404, 404, 404, 401, new { discussionNumber = 999, relationshipType = "Related" }, "create missing discussion link"),
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
                    $"E2E-POP22-{id:D3}",
                    actor.Actor,
                    probe.Method,
                    probe.Url,
                    probe.Body,
                    actor.Status(statuses),
                    $"{actor.Label} {probe.Intent}",
                    $"{actor.Label}-mr-probe-{id}"));
                id++;
            }
        }

        return cases;
    }

    private static object CreateBody(string title) => new
    {
        title,
        body = "matrix body",
        sourceRef = "feature/mr-e2e",
        targetRef = "main",
        isDraft = false,
    };

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
