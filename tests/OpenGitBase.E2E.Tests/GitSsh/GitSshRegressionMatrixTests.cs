using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.GitSsh;

[Collection("Compose")]
[Trait("Category", "GitSsh")]
[Trait("RequiresCompose", "true")]
[E2eTier(2)]
public class GitSshRegressionMatrixTests : AuthMatrixTheoryBase
{
    public static IEnumerable<object[]> GitSshMatrixCases() =>
        GitSshRegressionMatrix.BuildCases().Select(c => new object[] { c });

    [RequiresComposeTheory]
    [MemberData(nameof(GitSshMatrixCases))]
    [Trait("Tag", "Regression")]
    public async Task GitSshEndpointsMatrix(AuthMatrixCase matrixCase)
    {
        var identity = new IdentityFixture(Context, Transcript);
        var owner = await identity.RegisterUserAsync($"ssh-reg-owner-{Context.RunSuffix}").ConfigureAwait(false);
        var outsider = await identity.RegisterUserAsync($"ssh-reg-outsider-{Context.RunSuffix}").ConfigureAwait(false);

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

internal static class GitSshRegressionMatrix
{
    public static IReadOnlyList<AuthMatrixCase> BuildCases()
    {
        var sshAvailable = IsSshProfileEnabled();
        var skipReason = "SSH profile is not enabled in fast compose runs.";
        var cases = new List<AuthMatrixCase>
        {
            Row("E2E-POP28-001", AuthMatrixActor.Owner, HttpMethod.Get, "/public-git-ssh-key", null, 200, "Owner can list SSH keys", "owner-list-ssh-keys"),
            Row("E2E-POP28-002", AuthMatrixActor.Anonymous, HttpMethod.Get, "/public-git-ssh-key", null, 401, "Anonymous cannot list SSH keys", "anon-list-ssh-keys"),
            Row("E2E-POP28-003", AuthMatrixActor.Owner, HttpMethod.Post, "/public-git-ssh-key", InvalidSshBody(), 400, "Owner invalid SSH key rejected", "owner-invalid-ssh-key"),
            Row("E2E-POP28-004", AuthMatrixActor.Outsider, HttpMethod.Get, "/public-git-ssh-key", null, 200, "Outsider can list own SSH keys", "outsider-list-own-keys"),
            Row("E2E-POP28-005", AuthMatrixActor.Anonymous, HttpMethod.Get, "/api/v1/ssh-authentication/by-fingerprint?fingerprint=SHA256:test", null, 404, "Anonymous ssh-auth unknown fingerprint returns 404", "anon-ssh-auth-unknown"),
        };

        for (var i = 6; i <= 35; i++)
        {
            var actor = (i % 3) switch
            {
                0 => AuthMatrixActor.Owner,
                1 => AuthMatrixActor.Outsider,
                _ => AuthMatrixActor.Anonymous,
            };
            var endpoint = i % 2 == 0
                ? "/public-git-ssh-key"
                : $"/api/v1/ssh-authentication/by-fingerprint?fingerprint=SHA256:matrix{i}";
            var status = endpoint.StartsWith("/public-git-ssh-key", StringComparison.Ordinal)
                ? actor == AuthMatrixActor.Anonymous ? 401 : 200
                : 404;

            cases.Add(new AuthMatrixCase(
                $"E2E-POP28-{i:D3}",
                actor,
                HttpMethod.Get,
                endpoint,
                null,
                status,
                $"{actor} git ssh probe {i}",
                $"git-ssh-probe-{i}",
                NotApplicable: !sshAvailable,
                SkipReason: !sshAvailable ? skipReason : null));
        }

        return cases;
    }

    private static bool IsSshProfileEnabled()
    {
        var profile = Environment.GetEnvironmentVariable(ComposeFullHaGate.ProfileEnvironmentVariable);
        return string.Equals(profile, nameof(ComposeProfile.FullHa), StringComparison.OrdinalIgnoreCase)
            || string.Equals(profile, "full-ha", StringComparison.OrdinalIgnoreCase);
    }

    private static object InvalidSshBody() => new
    {
        modelToCreate = new
        {
            name = "invalid",
            publicSSHKey = "ssh-ed25519 invalid",
            fingerprint = "SHA256:invalid",
        },
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
