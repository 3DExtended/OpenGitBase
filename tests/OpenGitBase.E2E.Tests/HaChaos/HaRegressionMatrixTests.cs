using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.HaChaos;

[Collection("Compose")]
[Trait("Category", "HaChaos")]
[Trait("RequiresCompose", "true")]
[E2eTier(7)]
public class HaRegressionMatrixTests : AuthMatrixTheoryBase
{
    public static IEnumerable<object[]> HaMatrixCases() =>
        HaRegressionMatrix.BuildCases().Select(c => new object[] { c });

    [RequiresFullHaTheory]
    [MemberData(nameof(HaMatrixCases))]
    [Trait("Tag", "FullHa")]
    [Trait("Tag", "Regression")]
    public async Task HighAvailabilityMatrix(AuthMatrixCase matrixCase)
    {
        var identity = new IdentityFixture(Context, Transcript);
        var owner = await identity.RegisterUserAsync($"ha-reg-owner-{Context.RunSuffix}").ConfigureAwait(false);
        var outsider = await identity.RegisterUserAsync($"ha-reg-outsider-{Context.RunSuffix}").ConfigureAwait(false);
        AuthenticatedClient? admin = null;
        if (matrixCase.Actor == AuthMatrixActor.Admin)
        {
            admin = await identity.LoginPlatformAdminAsync().ConfigureAwait(false);
        }

        await RunMatrixCaseAsync(
            matrixCase,
            new AuthMatrixContext
            {
                Anonymous = new E2eApiClient(Transcript, Context.Normalizer),
                Owner = owner,
                Outsider = outsider,
                Admin = admin,
            }).ConfigureAwait(false);
    }
}

internal static class HaRegressionMatrix
{
    public static IReadOnlyList<AuthMatrixCase> BuildCases()
    {
        ComposeFullHaGate.Refresh();
        var fullHaActive = ComposeFullHaGate.IsFullHaProfile;
        var skipReason = ComposeFullHaGate.SkipReason;

        var cases = new List<AuthMatrixCase>
        {
            Row("E2E-POP26-001", AuthMatrixActor.Anonymous, HttpMethod.Get, "/health", null, 200, "Anonymous health endpoint", "health-anonymous"),
            Row("E2E-POP26-002", AuthMatrixActor.Owner, HttpMethod.Get, "/health", null, 200, "Authenticated health endpoint", "health-owner"),
            Row("E2E-POP26-003", AuthMatrixActor.Admin, HttpMethod.Get, "/admin/storage-nodes", null, 200, "Admin lists storage nodes under HA", "admin-storage-nodes"),
            Row("E2E-POP26-004", AuthMatrixActor.Owner, HttpMethod.Get, "/admin/storage-nodes", null, 403, "Non-admin denied storage node list", "owner-admin-storage-denied"),
            Row("E2E-POP26-005", AuthMatrixActor.Anonymous, HttpMethod.Get, "/admin/storage-nodes", null, 401, "Anonymous denied storage node list", "anon-admin-storage-denied"),
            Row("E2E-POP26-006", AuthMatrixActor.Admin, HttpMethod.Get, "/admin/storage-nodes/replication-summary", null, 200, "Admin replication summary under HA", "admin-replication-summary"),
            Row("E2E-POP26-007", AuthMatrixActor.Admin, HttpMethod.Get, "/admin/repositories", null, 200, "Admin repository replication list under HA", "admin-repositories"),
            Row("E2E-POP26-008", AuthMatrixActor.Admin, HttpMethod.Get, "/admin/storage-enrollments", null, 200, "Admin enrollments under HA", "admin-enrollments"),
            Row("E2E-POP26-009", AuthMatrixActor.Outsider, HttpMethod.Get, "/admin/storage-nodes/replication-summary", null, 403, "Outsider denied replication summary", "outsider-replication-denied"),
            Row("E2E-POP26-010", AuthMatrixActor.Owner, HttpMethod.Get, "/admin/fleet/dispatcher-ssh-public-key", null, 403, "Owner denied fleet key under HA", "owner-fleet-denied"),
        };

        var probes = new (HttpMethod Method, string Url, int Admin, int Owner, int Outsider, int Anonymous, string Intent)[]
        {
            (HttpMethod.Get, "/health", 200, 200, 200, 200, "health endpoint"),
            (HttpMethod.Get, "/admin/storage-nodes", 200, 403, 403, 401, "storage nodes"),
            (HttpMethod.Get, "/admin/storage-enrollments", 200, 403, 403, 401, "storage enrollments"),
            (HttpMethod.Get, "/admin/storage-nodes/replication-summary", 200, 403, 403, 401, "replication summary"),
            (HttpMethod.Get, "/admin/repositories", 200, 403, 403, 401, "admin repositories"),
            (HttpMethod.Get, "/admin/repositories?attention=degraded", 200, 403, 403, 401, "attention degraded"),
            (HttpMethod.Get, "/admin/fleet/dispatcher-ssh-public-key", 200, 403, 403, 401, "fleet ssh key"),
            (HttpMethod.Get, "/admin/status/incident", 200, 403, 403, 401, "incident status"),
        };

        var actors = new[]
        {
            new { Actor = AuthMatrixActor.Admin, Label = "admin", Status = (Func<(int, int, int, int), int>)(x => x.Item1) },
            new { Actor = AuthMatrixActor.Owner, Label = "owner", Status = (Func<(int, int, int, int), int>)(x => x.Item2) },
            new { Actor = AuthMatrixActor.Outsider, Label = "outsider", Status = (Func<(int, int, int, int), int>)(x => x.Item3) },
            new { Actor = AuthMatrixActor.Anonymous, Label = "anonymous", Status = (Func<(int, int, int, int), int>)(x => x.Item4) },
        };

        var id = 11;
        foreach (var probe in probes)
        {
            var statuses = (probe.Admin, probe.Owner, probe.Outsider, probe.Anonymous);
            foreach (var actor in actors)
            {
                cases.Add(Row(
                    $"E2E-POP26-{id:D3}",
                    actor.Actor,
                    probe.Method,
                    probe.Url,
                    null,
                    actor.Status(statuses),
                    $"{actor.Label} {probe.Intent}",
                    $"{actor.Label}-ha-probe-{id}"));
                id++;
            }
        }

        // Distinct FullHa-gated fleet visibility checks (node stop/start proofs live in HaSmokeTests).
        var chaos = new (string Intent, string Key, string Url)[]
        {
            ("HA fleet lists storage nodes after bootstrap", "ha-fleet-nodes", "/admin/storage-nodes"),
            ("HA fleet lists enrollments after bootstrap", "ha-fleet-enrollments", "/admin/storage-enrollments"),
            ("HA replication summary reachable for admin", "ha-replication-summary", "/admin/storage-nodes/replication-summary"),
            ("HA admin repository list reachable", "ha-admin-repos", "/admin/repositories"),
            ("HA attention filter degraded reachable", "ha-attention-degraded", "/admin/repositories?attention=degraded"),
            ("HA fleet dispatcher key readable by admin", "ha-fleet-key", "/admin/fleet/dispatcher-ssh-public-key"),
            ("HA incident status endpoint reachable", "ha-incident", "/admin/status/incident"),
            ("HA health remains the control-plane pulse", "ha-health-control", "/health"),
            ("HA compute nodes list reachable", "ha-compute-nodes", "/admin/compute-nodes"),
            ("HA compute enrollments list reachable", "ha-compute-enrollments", "/admin/compute-enrollments"),
        };

        foreach (var item in chaos)
        {
            cases.Add(new AuthMatrixCase(
                $"E2E-POP26-{id:D3}",
                AuthMatrixActor.Admin,
                HttpMethod.Get,
                item.Url,
                null,
                200,
                item.Intent,
                item.Key,
                NotApplicable: !fullHaActive,
                SkipReason: !fullHaActive ? skipReason : null));
            id++;
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
