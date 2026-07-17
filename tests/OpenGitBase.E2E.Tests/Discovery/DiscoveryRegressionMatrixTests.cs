using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.Discovery;

[Collection("Compose")]
[Trait("Category", "Discovery")]
[Trait("RequiresCompose", "true")]
[E2eTier(4)]
public class DiscoveryRegressionMatrixTests : AuthMatrixTheoryBase
{
    public static IEnumerable<object[]> DiscoveryMatrixCases() =>
        DiscoveryRegressionMatrix.BuildCases().Select(c => new object[] { c });

    [RequiresComposeTheory]
    [MemberData(nameof(DiscoveryMatrixCases))]
    [Trait("Tag", "Regression")]
    public async Task DiscoveryAndNotificationsMatrix(AuthMatrixCase matrixCase)
    {
        var identity = new IdentityFixture(Context, Transcript);
        var repositories = new RepositoryFixture(Transcript, Context.Normalizer);
        var owner = await identity.RegisterUserAsync($"discv-reg-owner-{Context.RunSuffix}").ConfigureAwait(false);
        var reader = await identity.RegisterUserAsync($"discv-reg-reader-{Context.RunSuffix}").ConfigureAwait(false);
        var outsider = await identity.RegisterUserAsync($"discv-reg-outsider-{Context.RunSuffix}").ConfigureAwait(false);
        var repository = await repositories.CreateAsync(owner, $"discovery-{Context.RunSuffix}", "Discovery Regression", isPrivate: false)
            .ConfigureAwait(false);

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

internal static class DiscoveryRegressionMatrix
{
    public static IReadOnlyList<AuthMatrixCase> BuildCases()
    {
        var cases = new List<AuthMatrixCase>
        {
            Row("E2E-POP25-001", AuthMatrixActor.Anonymous, HttpMethod.Get, "/public/repositories", null, 200, "Anonymous can list public repositories", "anon-public-repositories"),
            Row("E2E-POP25-002", AuthMatrixActor.Anonymous, HttpMethod.Get, "/public/repositories/recent", null, 200, "Anonymous can list recent repositories", "anon-public-recent"),
            Row("E2E-POP25-003", AuthMatrixActor.Anonymous, HttpMethod.Get, "/public/owners/{{OWNER}}", null, 200, "Anonymous can get owner profile", "anon-owner-profile"),
            Row("E2E-POP25-004", AuthMatrixActor.Owner, HttpMethod.Get, "/notifications?unreadOnly=true", null, 200, "Owner can list unread notifications", "owner-notifications"),
            Row("E2E-POP25-005", AuthMatrixActor.Reader, HttpMethod.Get, "/notifications?unreadOnly=false", null, 200, "Reader can list notifications", "reader-notifications"),
            Row("E2E-POP25-006", AuthMatrixActor.Outsider, HttpMethod.Get, "/notifications?unreadOnly=true", null, 200, "Outsider can list own notifications", "outsider-notifications"),
            Row("E2E-POP25-007", AuthMatrixActor.Anonymous, HttpMethod.Get, "/notifications?unreadOnly=true", null, 401, "Anonymous cannot list notifications", "anon-notifications-denied"),
            Row("E2E-POP25-008", AuthMatrixActor.Owner, HttpMethod.Post, "/notifications/00000000-0000-0000-0000-000000000000/read", null, 404, "Owner mark unknown notification read", "owner-mark-read-unknown"),
            Row("E2E-POP25-009", AuthMatrixActor.Anonymous, HttpMethod.Post, "/notifications/00000000-0000-0000-0000-000000000000/read", null, 401, "Anonymous cannot mark read", "anon-mark-read-denied"),
            Row("E2E-POP25-010", AuthMatrixActor.Owner, HttpMethod.Get, "/public/repositories?q=discovery", null, 200, "Owner can query public discovery", "owner-public-query"),
        };

        var probes = new (HttpMethod Method, string Url, int Owner, int Reader, int Outsider, int Anonymous, string Intent)[]
        {
            (HttpMethod.Get, "/public/repositories", 200, 200, 200, 200, "list public repositories"),
            (HttpMethod.Get, "/public/repositories?q={{SLUG}}", 200, 200, 200, 200, "search public repositories"),
            (HttpMethod.Get, "/public/repositories/recent", 200, 200, 200, 200, "list recent repositories"),
            (HttpMethod.Get, "/public/owners/{{OWNER}}", 200, 200, 200, 200, "get owner profile"),
            (HttpMethod.Get, "/notifications?unreadOnly=true", 200, 200, 200, 401, "list unread notifications"),
            (HttpMethod.Get, "/notifications?unreadOnly=false", 200, 200, 200, 401, "list all notifications"),
            (HttpMethod.Post, "/notifications/00000000-0000-0000-0000-000000000000/read", 404, 404, 404, 401, "mark missing notification read"),
            (HttpMethod.Get, "/public/repositories?q={{OWNER}}", 200, 200, 200, 200, "repository query by owner"),
            (HttpMethod.Post, "/notifications/00000000-0000-0000-0000-000000000001/read", 404, 404, 404, 401, "mark another missing notification read"),
            (HttpMethod.Get, "/public/repositories?q=", 200, 200, 200, 200, "empty search query"),
            (HttpMethod.Get, "/public/owners/missing-owner-{{SLUG}}", 404, 404, 404, 404, "missing owner profile"),
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
                    $"E2E-POP25-{id:D3}",
                    actor.Actor,
                    probe.Method,
                    probe.Url,
                    null,
                    actor.Status(statuses),
                    $"{actor.Label} {probe.Intent}",
                    $"{actor.Label}-discovery-probe-{id}"));
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
