using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.Repository;

[Collection("Compose")]
[Trait("Category", "Repository")]
[Trait("RequiresCompose", "true")]
[E2eTier(5)]
public class RepositorySettingsRegressionMatrixTests : AuthMatrixTheoryBase
{
    public static IEnumerable<object[]> SettingsMatrixCases() =>
        RepositorySettingsRegressionMatrix.BuildCases().Select(c => new object[] { c });

    [RequiresComposeTheory]
    [MemberData(nameof(SettingsMatrixCases))]
    [Trait("Tag", "Regression")]
    public async Task RepositorySettingsAndProtectedBranchMatrix(AuthMatrixCase matrixCase)
    {
        var identity = new IdentityFixture(Context, Transcript);
        var repositories = new RepositoryFixture(Transcript, Context.Normalizer);
        var owner = await identity.RegisterUserAsync($"repo-settings-owner-{Context.RunSuffix}").ConfigureAwait(false);
        var reader = await identity.RegisterUserAsync($"repo-settings-reader-{Context.RunSuffix}").ConfigureAwait(false);
        var outsider = await identity.RegisterUserAsync($"repo-settings-outsider-{Context.RunSuffix}").ConfigureAwait(false);
        var repository = await repositories.CreateAsync(
            owner,
            $"repo-settings-{Context.RunSuffix}",
            "Repository Settings Regression",
            isPrivate: true).ConfigureAwait(false);
        await repositories.AddMemberAsync(owner, repository.RepositoryId, reader.UserId, role: 1).ConfigureAwait(false);

        var resolved = matrixCase with
        {
            RelativeUrl = matrixCase.RelativeUrl
                .Replace("{{REPO_ID}}", repository.RepositoryId, StringComparison.Ordinal)
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

internal static class RepositorySettingsRegressionMatrix
{
    public static IReadOnlyList<AuthMatrixCase> BuildCases()
    {
        var cases = new List<AuthMatrixCase>
        {
            Row("E2E-POP19-001", AuthMatrixActor.Owner, HttpMethod.Get, "/repository/{{REPO_ID}}", null, 200, "Owner can get repository", "owner-get-repo"),
            Row("E2E-POP19-002", AuthMatrixActor.Reader, HttpMethod.Get, "/repository/{{REPO_ID}}", null, 200, "Reader can get repository metadata", "reader-get-repo"),
            Row("E2E-POP19-003", AuthMatrixActor.Outsider, HttpMethod.Get, "/repository/{{REPO_ID}}", null, 200, "Outsider can get repository by id", "outsider-get-repo"),
            Row("E2E-POP19-004", AuthMatrixActor.Owner, HttpMethod.Get, "/repository/{{REPO_ID}}/usage", null, 200, "Owner can read usage", "owner-usage"),
            Row("E2E-POP19-005", AuthMatrixActor.Owner, HttpMethod.Put, "/repository/{{REPO_ID}}", new { name = "Updated name", isPrivate = true }, 204, "Owner can update metadata", "owner-update-repo"),
            Row("E2E-POP19-006", AuthMatrixActor.Reader, HttpMethod.Put, "/repository/{{REPO_ID}}", new { name = "Reader attempt", isPrivate = true }, 403, "Reader cannot update metadata", "reader-update-denied"),
            Row("E2E-POP19-007", AuthMatrixActor.Owner, HttpMethod.Get, "/repository/{{REPO_ID}}/settings/default-branch", null, 200, "Owner reads default branch", "owner-default-branch-get"),
            Row("E2E-POP19-008", AuthMatrixActor.Reader, HttpMethod.Get, "/repository/{{REPO_ID}}/settings/default-branch", null, 403, "Reader cannot read default branch settings", "reader-default-branch-denied"),
            Row("E2E-POP19-009", AuthMatrixActor.Owner, HttpMethod.Get, "/repository/{{REPO_ID}}/protected-branch-rules", null, 200, "Owner lists protected branch rules", "owner-list-rules"),
            Row("E2E-POP19-010", AuthMatrixActor.Outsider, HttpMethod.Get, "/repository/{{REPO_ID}}/protected-branch-rules", null, 403, "Outsider cannot list protected branch rules", "outsider-list-rules-denied"),
        };

        var probes = new (HttpMethod Method, string Url, int Owner, int Reader, int Outsider, int Anonymous, object? Body, string Intent)[]
        {
            (HttpMethod.Get, "/repository/{{REPO_ID}}/settings/default-branch", 200, 403, 403, 401, null, "Get default branch"),
            (HttpMethod.Patch, "/repository/{{REPO_ID}}/settings/default-branch", 400, 403, 403, 401, new { defaultBranchName = "main" }, "Patch default branch"),
            (HttpMethod.Get, "/repository/{{REPO_ID}}/protected-branch-rules", 200, 403, 403, 401, null, "List protected branch rules"),
            (HttpMethod.Get, "/repository/{{REPO_ID}}/protected-branch-rules/00000000-0000-0000-0000-000000000000", 404, 403, 403, 401, null, "Get missing protected branch rule"),
            (HttpMethod.Post, "/repository/{{REPO_ID}}/protected-branch-rules", 201, 403, 403, 401, RuleBody("main"), "Create protected branch rule"),
            (HttpMethod.Put, "/repository/{{REPO_ID}}/protected-branch-rules/00000000-0000-0000-0000-000000000000", 404, 403, 403, 401, RuleBody("main"), "Update missing protected branch rule"),
            (HttpMethod.Delete, "/repository/{{REPO_ID}}/protected-branch-rules/00000000-0000-0000-0000-000000000000", 404, 403, 403, 401, null, "Delete missing protected branch rule"),
            (HttpMethod.Get, "/repository/{{REPO_ID}}/usage", 200, 200, 200, 401, null, "Get repository usage"),
            (HttpMethod.Put, "/repository/{{REPO_ID}}", 204, 403, 403, 401, new { name = "Matrix update", isPrivate = true }, "Update repository metadata"),
            (HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}", 200, 200, 200, 401, null, "Get repository by slug"),
            (HttpMethod.Delete, "/repository/{{REPO_ID}}", 204, 403, 403, 401, null, "Delete repository"),
            (HttpMethod.Post, "/repository/{{SLUG}}", 400, 400, 400, 401, new { repositoryName = string.Empty, isPrivate = true }, "Create repository invalid payload"),
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
                    $"E2E-POP19-{id:D3}",
                    actor.Actor,
                    probe.Method,
                    probe.Url,
                    probe.Body,
                    actor.Status(statuses),
                    $"{actor.Label} {probe.Intent}",
                    $"{actor.Label}-repo-settings-probe-{id}"));
                id++;
            }
        }

        return cases;
    }

    private static object RuleBody(string pattern) => new
    {
        pattern,
        blockDirectPush = true,
        allowedPushRoles = 2,
        requiredApprovalCount = 0,
        mergeRoleThreshold = 2,
        forcePushPolicy = 0,
        pushRules = Array.Empty<object>(),
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
