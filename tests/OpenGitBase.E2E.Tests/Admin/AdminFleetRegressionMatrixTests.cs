using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.Admin;

[Collection("Compose")]
[Trait("Category", "Admin")]
[Trait("RequiresCompose", "true")]
[E2eTier(0)]
public class AdminFleetRegressionMatrixTests : AuthMatrixTheoryBase
{
    public static IEnumerable<object[]> AdminMatrixCases() =>
        AdminFleetRegressionMatrix.BuildCases().Select(c => new object[] { c });

    [RequiresComposeTheory]
    [MemberData(nameof(AdminMatrixCases))]
    [Trait("Tag", "Regression")]
    public async Task AdminAndFleetEndpointsMatrix(AuthMatrixCase matrixCase)
    {
        var identity = new IdentityFixture(Context, Transcript);
        var owner = await identity.RegisterUserAsync($"admin-reg-owner-{Context.RunSuffix}").ConfigureAwait(false);
        var outsider = await identity.RegisterUserAsync($"admin-reg-outsider-{Context.RunSuffix}").ConfigureAwait(false);

        await RunMatrixCaseAsync(
            matrixCase,
            new AuthMatrixContext
            {
                Anonymous = new E2eApiClient(Transcript, Context.Normalizer),
                Owner = owner,
                Outsider = outsider,
            }).ConfigureAwait(false);
    }
}

internal static class AdminFleetRegressionMatrix
{
    public static IReadOnlyList<AuthMatrixCase> BuildCases()
    {
        var cases = new List<AuthMatrixCase>
        {
            Row("E2E-POP27-001", AuthMatrixActor.Owner, HttpMethod.Get, "/admin/storage-nodes", null, 403, "Non-admin cannot list storage nodes", "owner-storage-nodes-denied"),
            Row("E2E-POP27-002", AuthMatrixActor.Anonymous, HttpMethod.Get, "/admin/storage-nodes", null, 401, "Anonymous cannot list storage nodes", "anon-storage-nodes-denied"),
            Row("E2E-POP27-003", AuthMatrixActor.Owner, HttpMethod.Get, "/admin/storage-enrollments", null, 403, "Non-admin cannot list enrollments", "owner-enrollments-denied"),
            Row("E2E-POP27-004", AuthMatrixActor.Anonymous, HttpMethod.Get, "/admin/storage-enrollments", null, 401, "Anonymous cannot list enrollments", "anon-enrollments-denied"),
            Row("E2E-POP27-005", AuthMatrixActor.Owner, HttpMethod.Post, "/admin/storage-enrollments", new { nodeId = "node-a", expiresInHours = 2 }, 403, "Non-admin cannot create enrollment", "owner-create-enrollment-denied"),
            Row("E2E-POP27-006", AuthMatrixActor.Owner, HttpMethod.Get, "/admin/fleet/dispatcher-ssh-public-key", null, 403, "Non-admin cannot get fleet ssh key", "owner-fleet-key-denied"),
            Row("E2E-POP27-007", AuthMatrixActor.Anonymous, HttpMethod.Get, "/admin/fleet/dispatcher-ssh-public-key", null, 401, "Anonymous cannot get fleet ssh key", "anon-fleet-key-denied"),
            Row("E2E-POP27-008", AuthMatrixActor.Owner, HttpMethod.Post, "/admin/fleet/dispatcher-ssh-keys/generate", null, 403, "Non-admin cannot generate fleet keys", "owner-fleet-generate-denied"),
            Row("E2E-POP27-009", AuthMatrixActor.Anonymous, HttpMethod.Post, "/admin/fleet/dispatcher-ssh-keys/generate", null, 401, "Anonymous cannot generate fleet keys", "anon-fleet-generate-denied"),
            Row("E2E-POP27-010", AuthMatrixActor.Outsider, HttpMethod.Get, "/admin/fleet/dispatcher-ssh-public-key", null, 403, "Outsider cannot read fleet key", "outsider-fleet-key-denied"),
        };

        var probes = new (HttpMethod Method, string Url, int Owner, int Outsider, int Anonymous, object? Body, string Intent)[]
        {
            (HttpMethod.Get, "/admin/storage-nodes", 403, 403, 401, null, "list storage nodes"),
            (HttpMethod.Get, "/admin/storage-enrollments", 403, 403, 401, null, "list storage enrollments"),
            (HttpMethod.Post, "/admin/storage-enrollments", 403, 403, 401, new { nodeId = "node-reg", expiresInHours = 1 }, "create storage enrollment"),
            (HttpMethod.Post, "/admin/storage-enrollments", 403, 403, 401, new { nodeId = string.Empty, expiresInHours = 1 }, "create invalid storage enrollment"),
            (HttpMethod.Get, "/admin/fleet/dispatcher-ssh-public-key", 403, 403, 401, null, "get fleet ssh public key"),
            (HttpMethod.Post, "/admin/fleet/dispatcher-ssh-keys/generate", 403, 403, 401, null, "generate fleet ssh keys"),
        };

        var actors = new[]
        {
            new { Actor = AuthMatrixActor.Owner, Label = "owner", Status = (Func<(int, int, int), int>)(x => x.Item1) },
            new { Actor = AuthMatrixActor.Outsider, Label = "outsider", Status = (Func<(int, int, int), int>)(x => x.Item2) },
            new { Actor = AuthMatrixActor.Anonymous, Label = "anonymous", Status = (Func<(int, int, int), int>)(x => x.Item3) },
        };

        var id = 11;
        foreach (var probe in probes)
        {
            var statuses = (probe.Owner, probe.Outsider, probe.Anonymous);
            foreach (var actor in actors)
            {
                cases.Add(Row(
                    $"E2E-POP27-{id:D3}",
                    actor.Actor,
                    probe.Method,
                    probe.Url,
                    probe.Body,
                    actor.Status(statuses),
                    $"{actor.Label} {probe.Intent}",
                    $"{actor.Label}-admin-probe-{id}"));
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
