using OpenGitBase.E2E.Core;

namespace OpenGitBase.E2E.Tests.Security;

[Collection("Compose")]
[Trait("Category", "Security")]
[Trait("RequiresCompose", "true")]
[E2eTier(3)]
public class AuthMatrixTests : AuthMatrixTheoryBase
{
    public static IEnumerable<object[]> AnonymousMutationCases() =>
        AnonymousMutationMatrix.Cases.Select(c => new object[] { c });

    [RequiresComposeTheory]
    [MemberData(nameof(AnonymousMutationCases))]
    [Trait("Tag", "Smoke")]
    [Trait("Tag", "Regression")]
    public Task AnonymousCannotMutate(AuthMatrixCase matrixCase) =>
        RunMatrixCaseAsync(matrixCase, new AuthMatrixContext
        {
            Anonymous = new E2eApiClient(Transcript, Context.Normalizer),
        });
}

internal static class AnonymousMutationMatrix
{
    public static IReadOnlyList<AuthMatrixCase> Cases { get; } =
    [
        new(
            "E2E-SEC-001",
            AuthMatrixActor.Anonymous,
            HttpMethod.Post,
            "/repository/protected-slug",
            new { repositoryName = "x", isPrivate = true },
            401,
            "Anonymous POST /repository should return 401",
            "anon-post-repository"),
        new(
            "E2E-SEC-002",
            AuthMatrixActor.Anonymous,
            HttpMethod.Post,
            "/organization",
            new { modelToCreate = new { name = "x", slug = "x" } },
            401,
            "Anonymous POST /organization should return 401",
            "anon-post-organization"),
        new(
            "E2E-SEC-003",
            AuthMatrixActor.Anonymous,
            HttpMethod.Get,
            "/account/me",
            null,
            401,
            "Anonymous GET /account/me should return 401",
            "anon-get-account-me"),
        new(
            "E2E-SEC-004",
            AuthMatrixActor.Anonymous,
            HttpMethod.Post,
            "/git-access-token",
            new { name = "x", scope = "read" },
            401,
            "Anonymous POST /git-access-token should return 401",
            "anon-post-pat"),
        new(
            "E2E-SEC-005",
            AuthMatrixActor.Anonymous,
            HttpMethod.Delete,
            "/repository/some-id",
            null,
            401,
            "Anonymous DELETE /repository should return 401",
            "anon-delete-repository"),
    ];
}
