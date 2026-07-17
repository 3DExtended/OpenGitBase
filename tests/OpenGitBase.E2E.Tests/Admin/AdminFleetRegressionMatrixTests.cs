using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.Admin;

public sealed class AdminFleetMatrixFixture : IAsyncLifetime
{
    public TestIsolation Context { get; } = new();

    public OperationTranscript Transcript { get; } = new();

    public AuthenticatedClient Admin { get; private set; } = null!;

    public AuthenticatedClient Owner { get; private set; } = null!;

    public AuthenticatedClient Outsider { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Context.ClearCapturedEmailsAsync().ConfigureAwait(false);
        var identity = new IdentityFixture(Context, Transcript);
        Admin = await identity.LoginPlatformAdminAsync().ConfigureAwait(false);
        Owner = await identity.RegisterUserAsync($"admin-fx-owner-{Context.RunSuffix}").ConfigureAwait(false);
        Outsider = await identity.RegisterUserAsync($"admin-fx-outsider-{Context.RunSuffix}").ConfigureAwait(false);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[Collection("Compose")]
[Trait("Category", "Admin")]
[Trait("RequiresCompose", "true")]
[E2eTier(0)]
public class AdminFleetRegressionMatrixTests : AuthMatrixTheoryBase, IClassFixture<AdminFleetMatrixFixture>
{
    private readonly AdminFleetMatrixFixture _fixture;

    public AdminFleetRegressionMatrixTests(AdminFleetMatrixFixture fixture)
    {
        _fixture = fixture;
    }

    public static IEnumerable<object[]> AdminMatrixCases() =>
        AdminFleetRegressionMatrix.BuildCases().Select(c => new object[] { c });

    [RequiresComposeTheory]
    [MemberData(nameof(AdminMatrixCases))]
    [Trait("Tag", "Regression")]
    public async Task AdminAndFleetEndpointsMatrix(AuthMatrixCase matrixCase)
    {
        var admin = CloneAuth(_fixture.Admin);
        var owner = CloneAuth(_fixture.Owner);
        var outsider = CloneAuth(_fixture.Outsider);

        var resolved = matrixCase with
        {
            RelativeUrl = matrixCase.RelativeUrl
                .Replace("{{REPO_ID}}", "00000000-0000-0000-0000-000000000000", StringComparison.Ordinal),
            Body = ResolveAdminBody(matrixCase.Body, Context.RunSuffix, matrixCase.CatalogId),
        };

        await RunMatrixCaseAsync(
            resolved,
            new AuthMatrixContext
            {
                Anonymous = new E2eApiClient(Transcript, Context.Normalizer),
                Owner = owner,
                Outsider = outsider,
                Admin = admin,
            }).ConfigureAwait(false);
    }

    private AuthenticatedClient CloneAuth(AuthenticatedClient source) =>
        new()
        {
            Username = source.Username,
            UserId = source.UserId,
            Token = source.Token,
            Client = new E2eApiClient(Transcript, Context.Normalizer, source.Token),
        };

    private static object? ResolveAdminBody(object? body, string runSuffix, string catalogId)
    {
        if (body is null)
        {
            return null;
        }

        var caseToken = catalogId.Replace("E2E-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        var json = System.Text.Json.JsonSerializer.Serialize(body)
            .Replace("{{RUN}}", runSuffix, StringComparison.Ordinal)
            .Replace("{{CASE}}", $"{runSuffix}-{caseToken}", StringComparison.Ordinal);
        return System.Text.Json.JsonSerializer.Deserialize<object>(json);
    }
}

internal static class AdminFleetRegressionMatrix
{
    public static IReadOnlyList<AuthMatrixCase> BuildCases()
    {
        var cases = new List<AuthMatrixCase>
        {
            Row("E2E-POP27-001", AuthMatrixActor.Admin, HttpMethod.Get, "/admin/storage-nodes", null, 200, "Admin lists storage nodes", "admin-storage-nodes"),
            Row("E2E-POP27-002", AuthMatrixActor.Owner, HttpMethod.Get, "/admin/storage-nodes", null, 403, "Non-admin cannot list storage nodes", "owner-storage-nodes-denied"),
            Row("E2E-POP27-003", AuthMatrixActor.Anonymous, HttpMethod.Get, "/admin/storage-nodes", null, 401, "Anonymous cannot list storage nodes", "anon-storage-nodes-denied"),
            Row("E2E-POP27-004", AuthMatrixActor.Admin, HttpMethod.Get, "/admin/storage-enrollments", null, 200, "Admin lists storage enrollments", "admin-enrollments"),
            Row("E2E-POP27-005", AuthMatrixActor.Owner, HttpMethod.Get, "/admin/storage-enrollments", null, 403, "Non-admin cannot list enrollments", "owner-enrollments-denied"),
            Row("E2E-POP27-006", AuthMatrixActor.Admin, HttpMethod.Get, "/admin/fleet/dispatcher-ssh-public-key", null, 200, "Admin can get fleet ssh key", "admin-fleet-key"),
            Row("E2E-POP27-007", AuthMatrixActor.Owner, HttpMethod.Get, "/admin/fleet/dispatcher-ssh-public-key", null, 403, "Non-admin cannot get fleet ssh key", "owner-fleet-key-denied"),
            Row("E2E-POP27-008", AuthMatrixActor.Admin, HttpMethod.Get, "/admin/storage-nodes/replication-summary", null, 200, "Admin replication summary", "admin-replication-summary"),
            Row("E2E-POP27-009", AuthMatrixActor.Admin, HttpMethod.Get, "/admin/repositories", null, 200, "Admin lists repositories replication", "admin-repositories"),
            Row("E2E-POP27-010", AuthMatrixActor.Outsider, HttpMethod.Get, "/admin/fleet/dispatcher-ssh-public-key", null, 403, "Outsider cannot read fleet key", "outsider-fleet-key-denied"),
        };

        var probes = new (HttpMethod Method, string Url, int Admin, int Owner, int Outsider, int Anonymous, object? Body, string Intent)[]
        {
            (HttpMethod.Get, "/admin/storage-nodes", 200, 403, 403, 401, null, "list storage nodes"),
            (HttpMethod.Get, "/admin/storage-enrollments", 200, 403, 403, 401, null, "list storage enrollments"),
            (HttpMethod.Post, "/admin/storage-enrollments", 200, 403, 403, 401, new { nodeId = "node-{{CASE}}", expiresInHours = 1 }, "create storage enrollment"),
            (HttpMethod.Post, "/admin/storage-enrollments", 400, 403, 403, 401, new { nodeId = string.Empty, expiresInHours = 1 }, "create invalid storage enrollment"),
            (HttpMethod.Get, "/admin/fleet/dispatcher-ssh-public-key", 200, 403, 403, 401, null, "get fleet ssh public key"),
            (HttpMethod.Get, "/admin/storage-nodes/replication-summary", 200, 403, 403, 401, null, "replication summary"),
            (HttpMethod.Get, "/admin/repositories", 200, 403, 403, 401, null, "list admin repositories"),
            (HttpMethod.Get, "/admin/repositories?page=1&pageSize=10", 200, 403, 403, 401, null, "list admin repositories paged"),
            (HttpMethod.Get, "/admin/repositories?attention=degraded", 200, 403, 403, 401, null, "list repositories attention degraded"),
            (HttpMethod.Get, "/admin/repositories?search=missing", 200, 403, 403, 401, null, "list repositories search"),
            (HttpMethod.Get, "/admin/repositories/{{REPO_ID}}/replication", 404, 403, 403, 401, null, "get missing repo replication"),
            (HttpMethod.Get, "/admin/compute-nodes", 200, 403, 403, 401, null, "list compute nodes"),
            (HttpMethod.Get, "/admin/compute-enrollments", 200, 403, 403, 401, null, "list compute enrollments"),
            (HttpMethod.Get, "/admin/fleet/dispatcher-ssh-public-key", 200, 403, 403, 401, null, "get fleet key again"),
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
