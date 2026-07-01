using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.GitHttps;

[Collection("Compose")]
[Trait("Category", "GitHttps")]
[Trait("RequiresCompose", "true")]
[E2eTier(2)]
public class GitHttpsRegressionMatrixTests : AuthMatrixTheoryBase
{
    public static IEnumerable<object[]> GitHttpsMatrixCases() =>
        GitHttpsRegressionMatrix.BuildCases().Select(c => new object[] { c });

    [RequiresComposeTheory]
    [MemberData(nameof(GitHttpsMatrixCases))]
    [Trait("Tag", "Regression")]
    public async Task GitHttpsAndPatEndpointsMatrix(AuthMatrixCase matrixCase)
    {
        var identity = new IdentityFixture(Context, Transcript);
        var repositories = new RepositoryFixture(Transcript, Context.Normalizer);
        var owner = await identity.RegisterUserAsync($"https-reg-owner-{Context.RunSuffix}").ConfigureAwait(false);
        var reader = await identity.RegisterUserAsync($"https-reg-reader-{Context.RunSuffix}").ConfigureAwait(false);
        var outsider = await identity.RegisterUserAsync($"https-reg-outsider-{Context.RunSuffix}").ConfigureAwait(false);
        var repository = await repositories.CreateAsync(owner, $"https-reg-{Context.RunSuffix}", "HTTPS Regression", isPrivate: false)
            .ConfigureAwait(false);
        await repositories.AddMemberAsync(owner, repository.RepositoryId, reader.UserId, role: 1).ConfigureAwait(false);

        var resolved = matrixCase with
        {
            RelativeUrl = matrixCase.RelativeUrl
                .Replace("{{OWNER}}", owner.Username, StringComparison.Ordinal)
                .Replace("{{SLUG}}", repository.Slug, StringComparison.Ordinal),
        };

        await RunMatrixCaseAsync(
            resolved,
            new AuthMatrixContext
            {
                Anonymous = new E2eApiClient(Transcript, Context.Normalizer),
                Owner = owner,
                Reader = reader,
                Outsider = outsider,
            }).ConfigureAwait(false);
    }
}

internal static class GitHttpsRegressionMatrix
{
    public static IReadOnlyList<AuthMatrixCase> BuildCases()
    {
        var cases = new List<AuthMatrixCase>
        {
            Row("E2E-POP23-001", AuthMatrixActor.Owner, HttpMethod.Get, "/git-access-token", null, 200, "Owner lists PATs", "owner-list-pat"),
            Row("E2E-POP23-002", AuthMatrixActor.Owner, HttpMethod.Post, "/git-access-token", new { name = "matrix-write", scope = "write" }, 201, "Owner creates write PAT", "owner-create-write-pat"),
            Row("E2E-POP23-003", AuthMatrixActor.Owner, HttpMethod.Post, "/git-access-token", new { name = "matrix-invalid", scope = "invalid" }, 400, "Owner invalid scope PAT rejected", "owner-invalid-scope"),
            Row("E2E-POP23-004", AuthMatrixActor.Anonymous, HttpMethod.Get, "/git-access-token", null, 401, "Anonymous cannot list PATs", "anon-list-pat"),
            Row("E2E-POP23-005", AuthMatrixActor.Anonymous, HttpMethod.Get, "/api/v1/git/config", null, 200, "Anonymous can read git config", "anon-git-config"),
            Row("E2E-POP23-006", AuthMatrixActor.Owner, HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/content/refs", null, 200, "Owner can resolve refs for HTTPS clone path", "owner-refs"),
            Row("E2E-POP23-007", AuthMatrixActor.Anonymous, HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/content/refs", null, 200, "Anonymous can resolve refs public repo", "anon-refs"),
            Row("E2E-POP23-008", AuthMatrixActor.Owner, HttpMethod.Delete, "/git-access-token/00000000-0000-0000-0000-000000000000", null, 404, "Deleting missing PAT returns 404", "owner-delete-missing-pat"),
            Row("E2E-POP23-009", AuthMatrixActor.Reader, HttpMethod.Get, "/git-access-token", null, 200, "Reader lists own PATs", "reader-list-pat"),
            Row("E2E-POP23-010", AuthMatrixActor.Outsider, HttpMethod.Get, "/api/v1/git/config", null, 200, "Outsider can read git config", "outsider-git-config"),
        };

        var probes = new (HttpMethod Method, string Url, int Owner, int Reader, int Outsider, int Anonymous, object? Body, string Intent)[]
        {
            (HttpMethod.Get, "/git-access-token", 200, 200, 200, 401, null, "list PAT"),
            (HttpMethod.Post, "/git-access-token", 201, 201, 201, 401, new { name = "matrix-read", scope = "read" }, "create read PAT"),
            (HttpMethod.Post, "/git-access-token", 400, 400, 400, 401, new { name = string.Empty, scope = "read" }, "create invalid PAT"),
            (HttpMethod.Get, "/git-access-token/00000000-0000-0000-0000-000000000000", 200, 200, 200, 401, null, "get PAT by unknown id"),
            (HttpMethod.Delete, "/git-access-token/00000000-0000-0000-0000-000000000000", 404, 404, 404, 401, null, "delete unknown PAT"),
            (HttpMethod.Get, "/api/v1/git/config", 200, 200, 200, 200, null, "git config"),
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/content/refs", 200, 200, 200, 200, null, "refs for clone"),
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/content/tree?refName=main&path=", 200, 200, 200, 200, null, "tree for clone"),
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/content/readme?refName=main", 200, 200, 200, 200, null, "readme for clone"),
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/content/blob/raw?refName=main&path=README.md", 200, 200, 200, 200, null, "raw blob for clone"),
        };

        var actors = new[]
        {
            new { Actor = AuthMatrixActor.Owner, Label = "owner", Status = (Func<(int, int, int, int), int>)(x => x.Item1) },
            new { Actor = AuthMatrixActor.Reader, Label = "reader", Status = (Func<(int, int, int, int), int>)(x => x.Item2) },
            new { Actor = AuthMatrixActor.Outsider, Label = "outsider", Status = (Func<(int, int, int, int), int>)(x => x.Item3) },
            new { Actor = AuthMatrixActor.Anonymous, Label = "anonymous", Status = (Func<(int, int, int, int), int>)(x => x.Item4) },
        };

        var id = 11;
        foreach (var probe in probes)
        {
            var statuses = (probe.Owner, probe.Reader, probe.Outsider, probe.Anonymous);
            foreach (var actor in actors)
            {
                cases.Add(Row(
                    $"E2E-POP23-{id:D3}",
                    actor.Actor,
                    probe.Method,
                    probe.Url,
                    probe.Body,
                    actor.Status(statuses),
                    $"{actor.Label} {probe.Intent}",
                    $"{actor.Label}-https-probe-{id}"));
                id++;
            }
        }

        for (var i = 0; i < 10; i++)
        {
            var caseId = 100 + i;
            cases.Add(new AuthMatrixCase(
                $"E2E-POP23-{caseId:D3}",
                AuthMatrixActor.Owner,
                HttpMethod.Get,
                $"/git-over-https/probe/{i}",
                null,
                200,
                $"Git transport probe {i} skipped in API matrix",
                $"git-transport-probe-{i}",
                NotApplicable: true,
                SkipReason: "Transport-level clone/push probe is covered in dedicated git command tests."));
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
