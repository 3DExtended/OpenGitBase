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
    // Stable ed25519 fixture from SshKeyServiceTests
    private const string ValidEd25519Key =
        "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIBsqn0bnF2207g75WsuF6spyWRQ0sN4T10bzcgk43r4=";

    public static IReadOnlyList<AuthMatrixCase> BuildCases()
    {
        var sshAvailable = IsSshProfileEnabled();
        var skipReason = "SSH git transport profile is not enabled in fast compose runs.";

        var cases = new List<AuthMatrixCase>
        {
            Row("E2E-POP28-001", AuthMatrixActor.Owner, HttpMethod.Get, "/public-git-ssh-key", null, 200, "Owner can list SSH keys", "owner-list-ssh-keys"),
            Row("E2E-POP28-002", AuthMatrixActor.Outsider, HttpMethod.Get, "/public-git-ssh-key", null, 200, "Outsider can list own SSH keys", "outsider-list-own-keys"),
            Row("E2E-POP28-003", AuthMatrixActor.Anonymous, HttpMethod.Get, "/public-git-ssh-key", null, 401, "Anonymous cannot list SSH keys", "anon-list-ssh-keys"),
            Row("E2E-POP28-004", AuthMatrixActor.Owner, HttpMethod.Post, "/public-git-ssh-key", InvalidSshBody(), 400, "Owner invalid SSH key rejected", "owner-invalid-ssh-key"),
            Row("E2E-POP28-005", AuthMatrixActor.Outsider, HttpMethod.Post, "/public-git-ssh-key", InvalidSshBody(), 400, "Outsider invalid SSH key rejected", "outsider-invalid-ssh-key"),
            Row("E2E-POP28-006", AuthMatrixActor.Anonymous, HttpMethod.Post, "/public-git-ssh-key", InvalidSshBody(), 401, "Anonymous cannot create SSH key", "anon-create-ssh-key"),
            Row("E2E-POP28-007", AuthMatrixActor.Owner, HttpMethod.Get, "/public-git-ssh-key/00000000-0000-0000-0000-000000000000", null, 404, "Owner get missing SSH key returns 404", "owner-get-missing-key"),
            Row("E2E-POP28-008", AuthMatrixActor.Owner, HttpMethod.Post, "/public-git-ssh-key", EmptyNameBody(), 400, "Owner create rejects empty name", "owner-empty-name"),
            Row("E2E-POP28-009", AuthMatrixActor.Owner, HttpMethod.Post, "/public-git-ssh-key", MismatchedFingerprintBody(), 400, "Owner create rejects mismatched fingerprint", "owner-mismatch-fp"),
            Row("E2E-POP28-010", AuthMatrixActor.Anonymous, HttpMethod.Get, "/api/v1/ssh-authentication/by-fingerprint?fingerprint=SHA256:test", null, 404, "Unknown fingerprint returns 404", "anon-ssh-auth-unknown"),
        };

        var probes = new (HttpMethod Method, string Url, int Owner, int Outsider, int Anonymous, object? Body, string Intent)[]
        {
            (HttpMethod.Get, "/public-git-ssh-key", 200, 200, 401, null, "list SSH keys"),
            (HttpMethod.Get, "/public-git-ssh-key/00000000-0000-0000-0000-000000000000", 404, 404, 401, null, "get missing SSH key"),
            (HttpMethod.Delete, "/public-git-ssh-key/00000000-0000-0000-0000-000000000000", 404, 404, 401, null, "delete missing SSH key"),
            (HttpMethod.Post, "/public-git-ssh-key", 400, 400, 401, InvalidSshBody(), "create invalid SSH key"),
            (HttpMethod.Post, "/public-git-ssh-key", 400, 400, 401, EmptyNameBody(), "create empty name SSH key"),
            (HttpMethod.Post, "/public-git-ssh-key", 400, 400, 401, MismatchedFingerprintBody(), "create mismatched fingerprint"),
            (HttpMethod.Post, "/public-git-ssh-key", 400, 400, 401, MissingKeyBody(), "create missing public key"),
            (HttpMethod.Get, "/api/v1/ssh-authentication/by-fingerprint?fingerprint=SHA256:missing", 404, 404, 404, null, "fingerprint lookup missing"),
            (HttpMethod.Get, "/api/v1/ssh-authentication/by-fingerprint?fingerprint=", 400, 400, 400, null, "fingerprint lookup empty"),
            (HttpMethod.Get, "/api/v1/ssh-authentication/by-fingerprint?fingerprint=not-a-fingerprint", 404, 404, 404, null, "fingerprint lookup malformed"),
            (HttpMethod.Get, "/api/v1/ssh-authentication/by-fingerprint?fingerprint=SHA256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", 404, 404, 404, null, "fingerprint lookup padded unknown"),
            (HttpMethod.Post, "/public-git-ssh-key", 400, 400, 401, RsaMalformedBody(), "create malformed rsa key"),
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
                    $"E2E-POP28-{id:D3}",
                    actor.Actor,
                    probe.Method,
                    probe.Url,
                    probe.Body,
                    actor.Status(statuses),
                    $"{actor.Label} {probe.Intent}",
                    $"{actor.Label}-ssh-probe-{id}"));
                id++;
            }
        }

        // Transport-level scenarios: distinct intents, skip when SSH profile inactive
        var transport = new (string Intent, string Key)[]
        {
            ("SSH clone public repository succeeds with enrolled key", "ssh-clone-public"),
            ("SSH push to owned repository succeeds with enrolled key", "ssh-push-owned"),
            ("SSH clone private repository denied for outsider key", "ssh-clone-private-denied"),
            ("SSH push rejected after key revocation", "ssh-push-revoked"),
            ("SSH push to protected branch denied", "ssh-push-protected"),
            ("SSH clone with unknown key rejected by dispatcher", "ssh-clone-unknown-key"),
            ("SSH clone as reader succeeds on private repo", "ssh-clone-reader"),
            ("SSH push as reader denied on private repo", "ssh-push-reader-denied"),
            ("SSH org-namespace clone path resolves", "ssh-clone-org"),
            ("SSH key rotation: second key works after first revoked", "ssh-key-rotation"),
        };

        foreach (var item in transport)
        {
            cases.Add(new AuthMatrixCase(
                $"E2E-POP28-{id:D3}",
                AuthMatrixActor.Owner,
                HttpMethod.Get,
                "/public-git-ssh-key",
                null,
                200,
                item.Intent,
                item.Key,
                NotApplicable: !sshAvailable,
                SkipReason: !sshAvailable ? skipReason : null));
            id++;
        }

        return cases;
    }

    private static bool IsSshProfileEnabled()
    {
        var profile = Environment.GetEnvironmentVariable(ComposeFullHaGate.ProfileEnvironmentVariable);
        return string.Equals(profile, nameof(ComposeProfile.FullHa), StringComparison.OrdinalIgnoreCase)
            || string.Equals(profile, "full-ha", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Environment.GetEnvironmentVariable("OGB_E2E_SSH_PROFILE"), "1", StringComparison.Ordinal);
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

    private static object EmptyNameBody() => new
    {
        modelToCreate = new
        {
            name = string.Empty,
            publicSSHKey = ValidEd25519Key,
            fingerprint = string.Empty,
        },
    };

    private static object MismatchedFingerprintBody() => new
    {
        modelToCreate = new
        {
            name = "mismatch",
            publicSSHKey = ValidEd25519Key,
            fingerprint = "SHA256:definitely-wrong-fingerprint",
        },
    };

    private static object MissingKeyBody() => new
    {
        modelToCreate = new
        {
            name = "missing-key",
            publicSSHKey = string.Empty,
            fingerprint = string.Empty,
        },
    };

    private static object RsaMalformedBody() => new
    {
        modelToCreate = new
        {
            name = "bad-rsa",
            publicSSHKey = "ssh-rsa not-valid-base64!!!",
            fingerprint = string.Empty,
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
