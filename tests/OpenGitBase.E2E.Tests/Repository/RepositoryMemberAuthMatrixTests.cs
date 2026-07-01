using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.Repository;

[Collection("Compose")]
[Trait("Category", "RepositoryMember")]
[Trait("RequiresCompose", "true")]
[E2eTier(5)]
public class RepositoryMemberAuthMatrixTests : AuthMatrixTheoryBase
{
    public static IEnumerable<object[]> MemberMatrixCases() =>
        RepositoryMemberMatrix.BuildCases().Select(c => new object[] { c });

    [RequiresComposeTheory]
    [MemberData(nameof(MemberMatrixCases))]
    [Trait("Tag", "Smoke")]
    [Trait("Tag", "Regression")]
    public async Task RepositoryMemberAccessMatrix(AuthMatrixCase matrixCase)
    {
        var identity = new IdentityFixture(Context, Transcript);
        var repos = new RepositoryFixture(Transcript, Context.Normalizer);
        var owner = await identity.RegisterUserAsync($"rm-owner-{Context.RunSuffix}").ConfigureAwait(false);
        var reader = await identity.RegisterUserAsync($"rm-reader-{Context.RunSuffix}").ConfigureAwait(false);
        var outsider = await identity.RegisterUserAsync($"rm-outsider-{Context.RunSuffix}").ConfigureAwait(false);
        var slug = $"rm-private-{Context.RunSuffix}";
        var repository = await repos.CreateAsync(owner, slug, "Member Matrix", isPrivate: true).ConfigureAwait(false);
        await repos.AddMemberAsync(owner, repository.RepositoryId, reader.UserId, role: 1).ConfigureAwait(false);

        var resolved = matrixCase with
        {
            RelativeUrl = matrixCase.RelativeUrl
                .Replace("{{OWNER}}", owner.Username, StringComparison.Ordinal)
                .Replace("{{SLUG}}", slug, StringComparison.Ordinal)
                .Replace("{{REPO_ID}}", repository.RepositoryId, StringComparison.Ordinal)
                .Replace("{{READER_ID}}", reader.UserId, StringComparison.Ordinal),
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

internal static class RepositoryMemberMatrix
{
    public static IReadOnlyList<AuthMatrixCase> BuildCases()
    {
        var cases = new List<AuthMatrixCase>
        {
            Row("E2E-F04-001", AuthMatrixActor.Outsider, HttpMethod.Get, "/repository-member?repositoryId={{REPO_ID}}", null, 403, "Outsider cannot list members", "outsider-list-members"),
            Row("E2E-F04-002", AuthMatrixActor.Reader, HttpMethod.Get, "/repository-member?repositoryId={{REPO_ID}}", null, 200, "Reader can list members", "reader-list-members"),
            Row("E2E-F04-003", AuthMatrixActor.Owner, HttpMethod.Get, "/repository-member?repositoryId={{REPO_ID}}", null, 200, "Owner can list members", "owner-list-members"),
            Row("E2E-F04-004", AuthMatrixActor.Outsider, HttpMethod.Post, "/repository-member", MemberBody("{{REPO_ID}}", "{{READER_ID}}", 1), 403, "Outsider cannot add member", "outsider-add-member"),
            Row("E2E-F04-005", AuthMatrixActor.Owner, HttpMethod.Post, "/repository-member", MemberBody("{{REPO_ID}}", "{{READER_ID}}", 2), 200, "Owner can promote member", "owner-promote-member"),
            Row("E2E-F04-006", AuthMatrixActor.Reader, HttpMethod.Post, "/repository-member", MemberBody("{{REPO_ID}}", "{{READER_ID}}", 2), 403, "Reader cannot promote member", "reader-promote-denied"),
            Row("E2E-F04-007", AuthMatrixActor.Anonymous, HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/content/refs", null, 404, "Anonymous private refs 404", "anon-private-refs"),
            Row("E2E-F04-008", AuthMatrixActor.Reader, HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/content/refs", null, 200, "Reader private refs 200", "reader-private-refs"),
            Row("E2E-F04-009", AuthMatrixActor.Outsider, HttpMethod.Get, "/repository/by-slug/{{OWNER}}/{{SLUG}}/content/refs", null, 404, "Outsider private refs denied", "outsider-private-refs"),
            Row("E2E-F04-010", AuthMatrixActor.Owner, HttpMethod.Delete, "/repository-member", MemberDeleteBody("{{REPO_ID}}", "{{READER_ID}}"), 200, "Owner can remove member", "owner-remove-member"),
        };

        for (var i = 11; i <= 25; i++)
        {
            var actor = (i % 3) switch
            {
                0 => AuthMatrixActor.Outsider,
                1 => AuthMatrixActor.Reader,
                _ => AuthMatrixActor.Owner,
            };
            var expected = actor == AuthMatrixActor.Owner ? 200 : actor == AuthMatrixActor.Reader ? 200 : 404;
            cases.Add(Row(
                $"E2E-F04-{i:D3}",
                actor,
                HttpMethod.Get,
                "/repository/by-slug/{{OWNER}}/{{SLUG}}/content/tree?refName=main&path=",
                null,
                expected == 200 && actor == AuthMatrixActor.Outsider ? 404 : expected,
                $"{actor} browse tree probe {i}",
                $"tree-probe-{i}"));
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

    private static object MemberBody(string repoId, string userId, int role) => new
    {
        modelToCreate = new
        {
            repositoryId = new { value = repoId },
            userId = new { value = userId },
            role,
        },
    };

    private static object MemberDeleteBody(string repoId, string userId) => new
    {
        repositoryId = repoId,
        userId,
    };
}
