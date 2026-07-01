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

    [RequiresFullHaFact]
    [MemberData(nameof(HaMatrixCases))]
    [Trait("Tag", "FullHa")]
    [Trait("Tag", "Regression")]
    public async Task HighAvailabilityMatrix(AuthMatrixCase matrixCase)
    {
        var identity = new IdentityFixture(Context, Transcript);
        var owner = await identity.RegisterUserAsync($"ha-reg-owner-{Context.RunSuffix}").ConfigureAwait(false);
        var outsider = await identity.RegisterUserAsync($"ha-reg-outsider-{Context.RunSuffix}").ConfigureAwait(false);

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
            Row("E2E-POP26-003", AuthMatrixActor.Owner, HttpMethod.Get, "/admin/storage-nodes", null, 403, "Non-admin denied storage node list", "owner-admin-storage-denied"),
            Row("E2E-POP26-004", AuthMatrixActor.Anonymous, HttpMethod.Get, "/admin/storage-nodes", null, 401, "Anonymous denied storage node list", "anon-admin-storage-denied"),
            Row("E2E-POP26-005", AuthMatrixActor.Owner, HttpMethod.Get, "/admin/storage-enrollments", null, 403, "Non-admin denied enrollment list", "owner-admin-enrollments-denied"),
        };

        for (var i = 6; i <= 35; i++)
        {
            var actor = i % 2 == 0 ? AuthMatrixActor.Owner : AuthMatrixActor.Anonymous;
            var authed = actor != AuthMatrixActor.Anonymous;
            var endpoint = (i % 3) switch
            {
                0 => "/admin/storage-nodes",
                1 => "/admin/storage-enrollments",
                _ => "/admin/fleet/dispatcher-ssh-public-key",
            };
            var expected = authed ? 403 : 401;
            cases.Add(new AuthMatrixCase(
                $"E2E-POP26-{i:D3}",
                actor,
                HttpMethod.Get,
                endpoint,
                null,
                expected,
                $"{actor} HA admin probe {i}",
                $"ha-admin-probe-{i}",
                NotApplicable: !fullHaActive,
                SkipReason: !fullHaActive ? skipReason : null));
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
        new(
            id,
            actor,
            method,
            url,
            body,
            status,
            intent,
            baseline,
            NotApplicable: false,
            SkipReason: null);
}
