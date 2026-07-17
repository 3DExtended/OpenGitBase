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
                .Replace("{{OWNER_ID}}", owner.UserId, StringComparison.Ordinal)
                .Replace("{{READER_ID}}", reader.UserId, StringComparison.Ordinal)
                .Replace("{{OWNER}}", owner.Username, StringComparison.Ordinal)
                .Replace("{{READER}}", reader.Username, StringComparison.Ordinal),
            Body = OrganizationRegressionMatrix.ResolveOrgBody(matrixCase.Body, owner.Username, reader.Username, organization.Slug),
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
            (HttpMethod.Delete, "/organization/{{ORG_ID}}", 204, 403, 403, 401, null, "Delete organization without blockers"),
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

        // Distinct invite / member edge cases (no query-param filler)
        cases.Add(Row(
            $"E2E-POP17-{id++:D3}",
            AuthMatrixActor.Owner,
            HttpMethod.Post,
            "/organization/{{ORG_ID}}/members",
            new { identifier = "{{READER}}", role = 0 },
            409,
            "Owner cannot re-add existing member",
            "owner-readd-member"));
        cases.Add(Row(
            $"E2E-POP17-{id++:D3}",
            AuthMatrixActor.Owner,
            HttpMethod.Delete,
            "/organization/{{ORG_ID}}/members/00000000-0000-0000-0000-000000000000",
            null,
            404,
            "Owner remove unknown member returns 404",
            "owner-remove-unknown-member"));
        cases.Add(Row(
            $"E2E-POP17-{id++:D3}",
            AuthMatrixActor.Reader,
            HttpMethod.Delete,
            "/organization/{{ORG_ID}}/members/{{OWNER_ID}}",
            null,
            403,
            "Member cannot remove owner",
            "member-remove-owner-denied"));
        cases.Add(Row(
            $"E2E-POP17-{id++:D3}",
            AuthMatrixActor.Outsider,
            HttpMethod.Put,
            "/organization/{{ORG_ID}}/members/{{READER_ID}}",
            new { role = 1 },
            403,
            "Outsider cannot promote member",
            "outsider-promote-denied"));
        cases.Add(Row(
            $"E2E-POP17-{id++:D3}",
            AuthMatrixActor.Anonymous,
            HttpMethod.Post,
            "/organization",
            new { modelToCreate = new { name = "Anon Org", slug = "anon-org-{{ORG_SLUG}}" } },
            401,
            "Anonymous cannot create organization",
            "anon-create-org"));
        cases.Add(Row(
            $"E2E-POP17-{id:D3}",
            AuthMatrixActor.Owner,
            HttpMethod.Get,
            "/organization/{{ORG_ID}}/storage/settings",
            null,
            200,
            "Owner can read org storage settings",
            "owner-org-storage-settings"));

        return cases;
    }

    public static object? ResolveOrgBody(object? body, string ownerUsername, string readerUsername, string orgSlug)
    {
        if (body is null)
        {
            return null;
        }

        var json = System.Text.Json.JsonSerializer.Serialize(body)
            .Replace("{{OWNER}}", ownerUsername, StringComparison.Ordinal)
            .Replace("{{READER}}", readerUsername, StringComparison.Ordinal)
            .Replace("{{ORG_SLUG}}", orgSlug, StringComparison.Ordinal);
        return System.Text.Json.JsonSerializer.Deserialize<object>(json);
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
