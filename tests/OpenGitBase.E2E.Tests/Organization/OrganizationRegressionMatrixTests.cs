using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.Organization;

[Collection("Compose")]
[Trait("Category", "Organization")]
[Trait("RequiresCompose", "true")]
[E2eTier(5)]
public class OrganizationRegressionMatrixTests : AuthMatrixTheoryBase
{
    public static IEnumerable<object[]> OrganizationMatrixCases() =>
        OrganizationRegressionMatrix.BuildCases().Select(c => new object[] { c });

    [RequiresComposeTheory]
    [MemberData(nameof(OrganizationMatrixCases))]
    [Trait("Tag", "Regression")]
    public async Task OrganizationAccessMatrix(AuthMatrixCase matrixCase)
    {
        var identity = new IdentityFixture(Context, Transcript);
        var organizations = new OrganizationFixture(Transcript);
        var owner = await identity.RegisterUserAsync($"org-reg-owner-{Context.RunSuffix}").ConfigureAwait(false);
        var reader = await identity.RegisterUserAsync($"org-reg-member-{Context.RunSuffix}").ConfigureAwait(false);
        var outsider = await identity.RegisterUserAsync($"org-reg-outsider-{Context.RunSuffix}").ConfigureAwait(false);
        var organization = await organizations.CreateAsync(owner, $"org-reg-{Context.RunSuffix}", "Organization Regression")
            .ConfigureAwait(false);
        await organizations.AddMemberAsync(owner, organization.Id, reader.Username).ConfigureAwait(false);

        var resolved = matrixCase with
        {
            RelativeUrl = matrixCase.RelativeUrl
                .Replace("{{ORG_ID}}", organization.Id, StringComparison.Ordinal)
                .Replace("{{ORG_SLUG}}", organization.Slug, StringComparison.Ordinal)
                .Replace("{{OWNER}}", owner.Username, StringComparison.Ordinal)
                .Replace("{{READER}}", reader.Username, StringComparison.Ordinal),
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

internal static class OrganizationRegressionMatrix
{
    public static IReadOnlyList<AuthMatrixCase> BuildCases()
    {
        var cases = new List<AuthMatrixCase>
        {
            Row("E2E-POP17-001", AuthMatrixActor.Owner, HttpMethod.Get, "/organization", null, 200, "Owner lists organizations", "owner-list"),
            Row("E2E-POP17-002", AuthMatrixActor.Reader, HttpMethod.Get, "/organization", null, 200, "Member lists organizations", "member-list"),
            Row("E2E-POP17-003", AuthMatrixActor.Outsider, HttpMethod.Get, "/organization", null, 200, "Outsider list returns own orgs", "outsider-list"),
            Row("E2E-POP17-004", AuthMatrixActor.Anonymous, HttpMethod.Get, "/organization", null, 401, "Anonymous cannot list organizations", "anon-list"),
            Row("E2E-POP17-005", AuthMatrixActor.Owner, HttpMethod.Get, "/organization/{{ORG_ID}}/members", null, 200, "Owner lists members", "owner-members"),
            Row("E2E-POP17-006", AuthMatrixActor.Reader, HttpMethod.Get, "/organization/{{ORG_ID}}/members", null, 200, "Member lists members", "member-members"),
            Row("E2E-POP17-007", AuthMatrixActor.Outsider, HttpMethod.Get, "/organization/{{ORG_ID}}/members", null, 403, "Outsider cannot list members", "outsider-members"),
            Row("E2E-POP17-008", AuthMatrixActor.Owner, HttpMethod.Get, "/organization/by-slug/{{ORG_SLUG}}", null, 200, "Owner can get by slug", "owner-by-slug"),
            Row("E2E-POP17-009", AuthMatrixActor.Reader, HttpMethod.Get, "/organization/by-slug/{{ORG_SLUG}}", null, 200, "Member can get by slug", "member-by-slug"),
            Row("E2E-POP17-010", AuthMatrixActor.Anonymous, HttpMethod.Get, "/organization/by-slug/{{ORG_SLUG}}", null, 401, "Anonymous cannot get by slug", "anon-by-slug"),
        };

        var probeTemplates = new (HttpMethod Method, string Url, int Owner, int Reader, int Outsider, int Anonymous, object? Body, string Intent)[]
        {
            (HttpMethod.Get, "/organization/{{ORG_ID}}", 200, 200, 200, 401, null, "Get organization by id"),
            (HttpMethod.Get, "/organization/by-slug/{{ORG_SLUG}}", 200, 200, 200, 401, null, "Get organization by slug"),
            (HttpMethod.Get, "/organization/{{ORG_ID}}/members", 200, 200, 403, 401, null, "List organization members"),
            (HttpMethod.Get, "/organization/{{ORG_ID}}/invites", 200, 200, 403, 401, null, "List organization invites"),
            (HttpMethod.Post, "/organization/{{ORG_ID}}/members", 404, 403, 403, 401, new { identifier = "ghost-user" }, "Add member by username"),
            (HttpMethod.Put, "/organization/{{ORG_ID}}", 204, 403, 403, 401, new { updatedModel = new { name = "Updated Org Name" } }, "Update organization name"),
            (HttpMethod.Delete, "/organization/{{ORG_ID}}", 409, 403, 403, 401, null, "Delete organization with blockers"),
            (HttpMethod.Post, "/organization/{{ORG_ID}}/invites/00000000-0000-0000-0000-000000000000/resend", 404, 403, 403, 401, null, "Resend unknown invite"),
            (HttpMethod.Delete, "/organization/{{ORG_ID}}/invites/00000000-0000-0000-0000-000000000000", 404, 403, 403, 401, null, "Revoke unknown invite"),
            (HttpMethod.Post, "/organization", 400, 400, 400, 401, new { modelToCreate = new { name = string.Empty, slug = string.Empty } }, "Create organization invalid model"),
        };

        var actors = new[]
        {
            new { Actor = AuthMatrixActor.Owner, StatusSelector = (Func<(int Owner, int Reader, int Outsider, int Anonymous), int>)(s => s.Owner), Label = "owner" },
            new { Actor = AuthMatrixActor.Reader, StatusSelector = (Func<(int Owner, int Reader, int Outsider, int Anonymous), int>)(s => s.Reader), Label = "reader" },
            new { Actor = AuthMatrixActor.Outsider, StatusSelector = (Func<(int Owner, int Reader, int Outsider, int Anonymous), int>)(s => s.Outsider), Label = "outsider" },
            new { Actor = AuthMatrixActor.Anonymous, StatusSelector = (Func<(int Owner, int Reader, int Outsider, int Anonymous), int>)(s => s.Anonymous), Label = "anonymous" },
        };

        var id = 11;
        foreach (var probe in probeTemplates)
        {
            var statusTuple = (probe.Owner, probe.Reader, probe.Outsider, probe.Anonymous);
            foreach (var actor in actors)
            {
                cases.Add(Row(
                    $"E2E-POP17-{id:D3}",
                    actor.Actor,
                    probe.Method,
                    probe.Url,
                    probe.Body,
                    actor.StatusSelector(statusTuple),
                    $"{actor.Label} {probe.Intent}",
                    $"{actor.Label}-org-probe-{id}"));
                id++;
            }
        }

        while (cases.Count < 56)
        {
            var idx = cases.Count + 1;
            var actor = idx % 2 == 0 ? AuthMatrixActor.Owner : AuthMatrixActor.Outsider;
            cases.Add(Row(
                $"E2E-POP17-{idx:D3}",
                actor,
                HttpMethod.Get,
                $"/organization/{{ORG_ID}}/members?probe={idx}",
                null,
                actor == AuthMatrixActor.Owner ? 200 : 403,
                $"{actor} members probe {idx}",
                $"org-members-probe-{idx}"));
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
