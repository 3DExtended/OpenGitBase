using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.Repository;

[Collection("Compose")]
[Trait("Category", "Repository")]
[Trait("RequiresCompose", "true")]
[E2eTier(5)]
public class BrowseRegressionMatrixTests : AuthMatrixTheoryBase
{
    private readonly GitTestDataFixture _gitData;

    public BrowseRegressionMatrixTests()
    {
        _gitData = new GitTestDataFixture(Transcript, Context.Normalizer);
    }

    public static IEnumerable<object[]> BrowseMatrixCases() =>
        BrowseRegressionMatrix.BuildCases().Select(c => new object[] { c });

    [RequiresComposeTheory]
    [MemberData(nameof(BrowseMatrixCases))]
    [Trait("Tag", "Regression")]
    public async Task RepositoryBrowseMatrix(AuthMatrixCase matrixCase)
    {
        var identity = new IdentityFixture(Context, Transcript);
        var repositories = new RepositoryFixture(Transcript, Context.Normalizer);
        var owner = await identity.RegisterUserAsync($"browse-reg-owner-{Context.RunSuffix}").ConfigureAwait(false);
        var reader = await identity.RegisterUserAsync($"browse-reg-reader-{Context.RunSuffix}").ConfigureAwait(false);
        var outsider = await identity.RegisterUserAsync($"browse-reg-outsider-{Context.RunSuffix}").ConfigureAwait(false);
        var workRoot = Path.Combine(Path.GetTempPath(), $"e2e-browse-reg-{Context.RunSuffix}");
        var browseRepo = await _gitData.GetBrowsePublicRepoAsync(owner, Context.RunSuffix, workRoot).ConfigureAwait(false);
        var privateRepo = await repositories.CreateAsync(owner, $"browse-private-{Context.RunSuffix}", "Browse Private", isPrivate: true)
            .ConfigureAwait(false);
        await repositories.AddMemberAsync(owner, privateRepo.RepositoryId, reader.UserId, role: 1).ConfigureAwait(false);

        var resolved = matrixCase with
        {
            RelativeUrl = matrixCase.RelativeUrl
                .Replace("{{OWNER}}", browseRepo.OwnerUsername, StringComparison.Ordinal)
                .Replace("{{SLUG}}", browseRepo.Slug, StringComparison.Ordinal)
                .Replace("{{PRIVATE_SLUG}}", privateRepo.Slug, StringComparison.Ordinal),
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

internal static class BrowseRegressionMatrix
{
    public static IReadOnlyList<AuthMatrixCase> BuildCases()
    {
        var cases = new List<AuthMatrixCase>
        {
            Row("E2E-POP20-001", AuthMatrixActor.Anonymous, HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/content/refs", null, 200, "Anonymous can read refs on public repository", "anon-public-refs"),
            Row("E2E-POP20-002", AuthMatrixActor.Anonymous, HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/content/tree?refName=main&path=", null, 200, "Anonymous can read tree on public repository", "anon-public-tree"),
            Row("E2E-POP20-003", AuthMatrixActor.Anonymous, HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{PRIVATE_SLUG}}/content/refs", null, 404, "Anonymous cannot read refs on private repository", "anon-private-refs"),
            Row("E2E-POP20-004", AuthMatrixActor.Outsider, HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{PRIVATE_SLUG}}/content/tree?refName=main&path=", null, 404, "Outsider cannot browse private tree", "outsider-private-tree"),
            Row("E2E-POP20-005", AuthMatrixActor.Reader, HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{PRIVATE_SLUG}}/content/tree?refName=main&path=", null, 200, "Reader can browse private tree", "reader-private-tree"),
            Row("E2E-POP20-006", AuthMatrixActor.Owner, HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/content/blob?refName=main&path=README.md", null, 200, "Owner can read README blob", "owner-readme-blob"),
            Row("E2E-POP20-007", AuthMatrixActor.Anonymous, HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/content/blob?refName=main&path=README.md", null, 200, "Anonymous can read README blob", "anon-readme-blob"),
            Row("E2E-POP20-008", AuthMatrixActor.Anonymous, HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/content/blob/raw?refName=main&path=README.md", null, 200, "Anonymous can read raw README blob", "anon-readme-raw"),
            Row("E2E-POP20-009", AuthMatrixActor.Anonymous, HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/content/readme?refName=main", null, 200, "Anonymous can read readme endpoint", "anon-readme-endpoint"),
            Row("E2E-POP20-010", AuthMatrixActor.Anonymous, HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/content/tree?path=", null, 400, "Tree requires refName query", "tree-missing-ref"),
        };

        var probes = new (HttpMethod Method, string Url, int Owner, int Reader, int Outsider, int Anonymous, string Intent)[]
        {
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/content/refs", 200, 200, 200, 200, "public refs"),
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/content/tree?refName=main&path=", 200, 200, 200, 200, "public root tree"),
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/content/tree?refName=main&path=src/foo", 200, 200, 200, 200, "public nested tree"),
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/content/blob?refName=main&path=assets/logo.svg", 200, 200, 200, 200, "public svg blob"),
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/content/blob?refName=main&path=data/large.bin", 200, 200, 200, 200, "public large blob"),
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/content/blob/raw?refName=main&path=README.md", 200, 200, 200, 200, "public raw blob"),
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/content/readme?refName=main", 200, 200, 200, 200, "public readme"),
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{PRIVATE_SLUG}}/content/refs", 200, 200, 404, 404, "private refs"),
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{PRIVATE_SLUG}}/content/tree?refName=main&path=", 200, 200, 404, 404, "private tree"),
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{PRIVATE_SLUG}}/content/blob?refName=main&path=README.md", 404, 404, 404, 404, "private blob unavailable"),
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/content/blob?path=README.md", 400, 400, 400, 400, "blob missing ref"),
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/content/blob?refName=main", 400, 400, 400, 400, "blob missing path"),
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
                    $"E2E-POP20-{id:D3}",
                    actor.Actor,
                    probe.Method,
                    probe.Url,
                    null,
                    actor.Status(statuses),
                    $"{actor.Label} {probe.Intent}",
                    $"{actor.Label}-browse-probe-{id}"));
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
