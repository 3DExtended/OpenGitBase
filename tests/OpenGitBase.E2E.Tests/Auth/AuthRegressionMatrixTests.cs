using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.Auth;

[Collection("Compose")]
[Trait("Category", "Auth")]
[Trait("RequiresCompose", "true")]
[E2eTier(1)]
public class AuthRegressionMatrixTests : AuthMatrixTheoryBase
{
    public static IEnumerable<object[]> AuthMatrixCases() =>
        AuthRegressionMatrix.BuildCases().Select(c => new object[] { c });

    [RequiresComposeTheory]
    [MemberData(nameof(AuthMatrixCases))]
    [Trait("Tag", "Regression")]
    public async Task AuthRegisterAndAccountMatrix(AuthMatrixCase matrixCase)
    {
        var identity = new IdentityFixture(Context, Transcript);
        var owner = await identity.RegisterUserAsync($"auth-owner-{Context.RunSuffix}").ConfigureAwait(false);
        var outsider = await identity.RegisterUserAsync($"auth-outsider-{Context.RunSuffix}").ConfigureAwait(false);

        var resolved = matrixCase with
        {
            RelativeUrl = matrixCase.RelativeUrl
                .Replace("{{OWNER}}", owner.Username, StringComparison.Ordinal)
                .Replace("{{OUTSIDER}}", outsider.Username, StringComparison.Ordinal),
            Body = AuthRegressionMatrix.ResolveBody(matrixCase.Body, owner.Username, outsider.Username, Context.RunSuffix),
        };

        await RunMatrixCaseAsync(
            resolved,
            new AuthMatrixContext
            {
                Anonymous = new E2eApiClient(Transcript, Context.Normalizer),
                Owner = owner,
                Outsider = outsider,
            }).ConfigureAwait(false);
    }
}

internal static class AuthRegressionMatrix
{
    public static IReadOnlyList<AuthMatrixCase> BuildCases()
    {
        var cases = new List<AuthMatrixCase>
        {
            Row("E2E-POP15-001", AuthMatrixActor.Anonymous, HttpMethod.Post, "/register/register", RegisterBody(string.Empty), 400, "Register rejects empty username", "register-empty-username"),
            Row("E2E-POP15-002", AuthMatrixActor.Anonymous, HttpMethod.Post, "/register/register", RegisterBody("ab"), 400, "Register rejects short username", "register-short-username"),
            Row("E2E-POP15-003", AuthMatrixActor.Anonymous, HttpMethod.Post, "/register/register", RegisterBody("{{OWNER}}"), 409, "Register rejects duplicate username", "register-duplicate-username"),
            Row("E2E-POP15-004", AuthMatrixActor.Anonymous, HttpMethod.Post, "/register/register", new { username = "dup-mail", email = "{{OWNER}}@example.com", password = "Password123!" }, 409, "Register rejects duplicate email", "register-duplicate-email"),
            Row("E2E-POP15-005", AuthMatrixActor.Anonymous, HttpMethod.Post, "/signin/login", new { username = "{{OWNER}}", password = "Password123!" }, 200, "Login succeeds for registered user", "login-owner"),
            Row("E2E-POP15-006", AuthMatrixActor.Anonymous, HttpMethod.Post, "/signin/login", new { username = "{{OWNER}}", password = "wrong-password" }, 404, "Login fails with wrong password", "login-wrong-password"),
            Row("E2E-POP15-007", AuthMatrixActor.Anonymous, HttpMethod.Get, "/account/me", null, 401, "Anonymous cannot read account me", "anon-account-me"),
            Row("E2E-POP15-008", AuthMatrixActor.Owner, HttpMethod.Get, "/account/me", null, 200, "Owner can read account me", "owner-account-me"),
            Row("E2E-POP15-009", AuthMatrixActor.Owner, HttpMethod.Post, "/account/resend-verification", null, 200, "Owner can resend verification", "owner-resend-verification"),
            Row("E2E-POP15-010", AuthMatrixActor.Anonymous, HttpMethod.Post, "/account/resend-verification", null, 401, "Anonymous cannot resend verification", "anon-resend-verification"),
        };

        var actorCases = new[]
        {
            new { Actor = AuthMatrixActor.Anonymous, Label = "anonymous", IsAuthed = false },
            new { Actor = AuthMatrixActor.Owner, Label = "owner", IsAuthed = true },
            new { Actor = AuthMatrixActor.Outsider, Label = "outsider", IsAuthed = true },
        };

        var accountProbes = new (HttpMethod Method, string Url, int Authed, int Anonymous, string Intent)[]
        {
            (HttpMethod.Get, "/account/me", 200, 401, "Get account me"),
            (HttpMethod.Post, "/account/resend-verification", 200, 401, "Resend verification"),
            (HttpMethod.Post, "/account/change-password", 400, 401, "Change password invalid body"),
            (HttpMethod.Post, "/account/delete", 400, 401, "Delete account invalid body"),
            (HttpMethod.Post, "/account/debug/verification-code", 200, 401, "Get verification debug code"),
            (HttpMethod.Post, "/account/debug/verify-email", 200, 401, "Debug verify email"),
            (HttpMethod.Post, "/signin/logout", 200, 200, "Logout endpoint"),
        };

        var id = 11;
        foreach (var probe in accountProbes)
        {
            foreach (var actor in actorCases)
            {
                var status = actor.IsAuthed ? probe.Authed : probe.Anonymous;
                object? body = null;
                if (probe.Url.Contains("change-password", StringComparison.Ordinal))
                {
                    body = new { currentPassword = "wrong", newPassword = "Password123!" };
                }
                else if (probe.Url.EndsWith("/delete", StringComparison.Ordinal))
                {
                    body = new { password = "wrong" };
                }

                cases.Add(Row(
                    $"E2E-POP15-{id:D3}",
                    actor.Actor,
                    probe.Method,
                    probe.Url,
                    body,
                    status,
                    $"{actor.Label} {probe.Intent}",
                    $"{actor.Label}-account-probe-{id}"));
                id++;
            }
        }

        for (; id <= 60; id++)
        {
            var actor = (id % 2 == 0) ? AuthMatrixActor.Owner : AuthMatrixActor.Anonymous;
            var isOwner = actor == AuthMatrixActor.Owner;
            cases.Add(Row(
                $"E2E-POP15-{id:D3}",
                actor,
                HttpMethod.Get,
                $"/account/me?probe={id}",
                null,
                isOwner ? 200 : 401,
                $"{actor} account/me probe {id}",
                $"account-me-probe-{id}"));
        }

        return cases;
    }

    public static object? ResolveBody(object? body, string ownerUsername, string outsiderUsername, string runSuffix)
    {
        if (body is null)
        {
            return null;
        }

        if (body is string marker)
        {
            return marker
                .Replace("{{OWNER}}", ownerUsername, StringComparison.Ordinal)
                .Replace("{{OUTSIDER}}", outsiderUsername, StringComparison.Ordinal)
                .Replace("{{RUN}}", runSuffix, StringComparison.Ordinal);
        }

        var json = System.Text.Json.JsonSerializer.Serialize(body)
            .Replace("{{OWNER}}", ownerUsername, StringComparison.Ordinal)
            .Replace("{{OUTSIDER}}", outsiderUsername, StringComparison.Ordinal)
            .Replace("{{RUN}}", runSuffix, StringComparison.Ordinal);
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

    private static object RegisterBody(string username) => new
    {
        username,
        email = $"{username}@example.com",
        password = "Password123!",
    };
}
