using System.Text.RegularExpressions;
using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.Auth;

[Collection("Compose")]
[Trait("Category", "Auth")]
[Trait("Tag", "Smoke")]
[Trait("RequiresCompose", "true")]
[E2eTier(1)]
public class AuthSmokeTests : AuthMatrixTheoryBase
{
    private const string Password = "Password123!";

    public static IEnumerable<object[]> AnonymousSmokeCases() =>
    [
        new object[]
        {
            new AuthMatrixCase(
                "E2E-AUTH-SMOKE-001",
                AuthMatrixActor.Anonymous,
                HttpMethod.Post,
                "/signin/login",
                new { username = "missing-user", password = "wrong-password" },
                401,
                "Wrong credentials return 401 on login",
                "wrong-password-401"),
        },
        new object[]
        {
            new AuthMatrixCase(
                "E2E-AUTH-SMOKE-002",
                AuthMatrixActor.Anonymous,
                HttpMethod.Get,
                "/account/me",
                null,
                401,
                "Anonymous account/me returns 401",
                "account-me-anon-401"),
        },
    ];

    [RequiresComposeTheory]
    [MemberData(nameof(AnonymousSmokeCases))]
    public Task AnonymousAuthMatrix(AuthMatrixCase matrixCase) =>
        RunMatrixCaseAsync(matrixCase, new AuthMatrixContext
        {
            Anonymous = new E2eApiClient(Transcript, Context.Normalizer),
        });

    [RequiresComposeFact]
    public async Task UnverifiedUserRepositoryCreateIsForbidden()
    {
        BeginScenario();
        var anon = new E2eApiClient(Transcript, Context.Normalizer);
        var username = $"auth-unverified-{Context.RunSuffix}";
        var email = $"{username}@example.com";
        var register = await anon.PostAsync("/register/register", new { username, email, password = Password }).ConfigureAwait(false);
        Assert.True(register.StatusCode is 200 or 201, register.Body);
        var jwt = E2eScenarioHelpers.ParseJwtToken(register);
        var unverified = new E2eApiClient(Transcript, Context.Normalizer, jwt);

        Transcript.Describe("Unverified user cannot create repository");
        var create = await unverified.PostAsync($"/repository/blocked-{Context.RunSuffix}", new
        {
            repositoryName = "blocked",
            isPrivate = false,
        }).ConfigureAwait(false);

        Assert.Equal(403, create.StatusCode);
        await Baselines.CaptureApiAsync("unverified-create-repository", create).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task PasswordResetFlowWorks()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var user = await identity.RegisterUserAsync($"auth-reset-{Context.RunSuffix}").ConfigureAwait(false);
        var email = $"{user.Username}@example.com";
        var anon = new E2eApiClient(Transcript, Context.Normalizer);
        const string newPassword = "Password456!";

        await EmailCapture.ClearAsync().ConfigureAwait(false);

        Transcript.Describe("Request password reset and parse reset code from captured email");
        var requestReset = await anon.PostAsync("/signin/requestresetpassword", new
        {
            username = user.Username,
            email,
        }).ConfigureAwait(false);
        Assert.Equal(200, requestReset.StatusCode);
        await Baselines.CaptureApiAsync("request-reset", requestReset).ConfigureAwait(false);

        var emails = await EmailCapture.ListAsync().ConfigureAwait(false);
        var resetMail = emails.LastOrDefault(m => m.To.Contains(email, StringComparison.OrdinalIgnoreCase)
            && m.Subject.Contains("Password reset", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(resetMail);
        var resetCode = ParseThreeSegmentCode(resetMail!.HtmlBody);

        Transcript.Describe("Reset password with captured reset code");
        var reset = await anon.PostAsync("/signin/resetpassword", new
        {
            username = user.Username,
            email,
            resetCode,
            newPassword,
        }).ConfigureAwait(false);
        Assert.Equal(200, reset.StatusCode);
        await Baselines.CaptureApiAsync("reset-password", reset).ConfigureAwait(false);

        var relogin = await anon.PostAsync("/signin/login", new { username = user.Username, password = newPassword })
            .ConfigureAwait(false);
        Assert.Equal(200, relogin.StatusCode);
        await Baselines.CaptureApiAsync("login-new-password", relogin).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task ChangePasswordFlowWorks()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var user = await identity.RegisterUserAsync($"auth-change-{Context.RunSuffix}").ConfigureAwait(false);
        var anon = new E2eApiClient(Transcript, Context.Normalizer);
        const string newPassword = "Password789!";

        Transcript.Describe("Change account password with current password");
        var change = await user.Client.PostAsync("/account/change-password", new
        {
            currentPassword = Password,
            newPassword,
        }).ConfigureAwait(false);
        Assert.Equal(200, change.StatusCode);
        await Baselines.CaptureApiAsync("change-password", change).ConfigureAwait(false);

        var oldLogin = await anon.PostAsync("/signin/login", new { username = user.Username, password = Password })
            .ConfigureAwait(false);
        Assert.Equal(401, oldLogin.StatusCode);
        await Baselines.CaptureApiAsync("old-password-rejected", oldLogin).ConfigureAwait(false);

        var newLogin = await anon.PostAsync("/signin/login", new { username = user.Username, password = newPassword })
            .ConfigureAwait(false);
        Assert.Equal(200, newLogin.StatusCode);
        await Baselines.CaptureApiAsync("new-password-login", newLogin).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task ResendVerificationSendsEmailAndVerifyWorks()
    {
        BeginScenario();
        var anon = new E2eApiClient(Transcript, Context.Normalizer);
        var username = $"auth-resend-{Context.RunSuffix}";
        var email = $"{username}@example.com";
        var register = await anon.PostAsync("/register/register", new { username, email, password = Password }).ConfigureAwait(false);
        Assert.True(register.StatusCode is 200 or 201, register.Body);
        var jwt = E2eScenarioHelpers.ParseJwtToken(register);
        var client = new E2eApiClient(Transcript, Context.Normalizer, jwt);

        await EmailCapture.ClearAsync().ConfigureAwait(false);

        Transcript.Describe("Resend verification then verify via captured code");
        var resend = await client.PostAsync("/account/resend-verification", null).ConfigureAwait(false);
        Assert.Equal(200, resend.StatusCode);
        await Baselines.CaptureApiAsync("resend-verification", resend).ConfigureAwait(false);

        var emails = await anon.GetCapturedEmailsAsync(email).ConfigureAwait(false);
        Assert.NotEmpty(emails);

        var codeResponse = await client.PostAsync("/account/debug/verification-code", null).ConfigureAwait(false);
        Assert.Equal(200, codeResponse.StatusCode);
        var verificationCode = System.Text.Json.JsonDocument.Parse(codeResponse.Body).RootElement.GetProperty("code").GetString();
        Assert.False(string.IsNullOrWhiteSpace(verificationCode));

        var verify = await anon.PostAsync("/account/verify-email", new
        {
            username,
            verificationToken = verificationCode,
        }).ConfigureAwait(false);
        Assert.Equal(200, verify.StatusCode);
        await Baselines.CaptureApiAsync("verify-email-captured-code", verify).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task SignOutReturnsOk()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var user = await identity.RegisterUserAsync($"auth-signout-{Context.RunSuffix}").ConfigureAwait(false);

        Transcript.Describe("Sign out clears auth session cookie");
        var signout = await user.Client.PostAsync("/signin/signout", null).ConfigureAwait(false);
        Assert.Equal(200, signout.StatusCode);
        await Baselines.CaptureApiAsync("signout", signout).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task VerifyEmailThenRepositoryCreateSucceeds()
    {
        BeginScenario();
        var anon = new E2eApiClient(Transcript, Context.Normalizer);
        var username = $"auth-verify-{Context.RunSuffix}";
        var email = $"{username}@example.com";
        var register = await anon.PostAsync("/register/register", new { username, email, password = Password }).ConfigureAwait(false);
        Assert.True(register.StatusCode is 200 or 201, register.Body);
        var jwt = E2eScenarioHelpers.ParseJwtToken(register);
        var client = new E2eApiClient(Transcript, Context.Normalizer, jwt);

        Transcript.Describe("Use debug endpoint to verify email, then create repository");
        var debugVerify = await client.PostAsync("/account/debug/verify-email", null).ConfigureAwait(false);
        Assert.Equal(200, debugVerify.StatusCode);
        await Baselines.CaptureApiAsync("debug-verify-email", debugVerify).ConfigureAwait(false);

        var create = await client.PostAsync($"/repository/auth-verified-{Context.RunSuffix}", new
        {
            repositoryName = "auth verified repo",
            isPrivate = false,
        }).ConfigureAwait(false);
        Assert.Equal(201, create.StatusCode);
        await Baselines.CaptureApiAsync("create-after-verify", create).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task ResetPasswordWithInvalidCodeReturnsBadRequest()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var user = await identity.RegisterUserAsync($"auth-bad-reset-{Context.RunSuffix}").ConfigureAwait(false);
        var anon = new E2eApiClient(Transcript, Context.Normalizer);
        var email = $"{user.Username}@example.com";

        Transcript.Describe("Password reset with invalid code returns 400");
        var reset = await anon.PostAsync("/signin/resetpassword", new
        {
            username = user.Username,
            email,
            resetCode = "000-000-000",
            newPassword = "DoesNotMatter123!",
        }).ConfigureAwait(false);
        Assert.Equal(400, reset.StatusCode);
        await Baselines.CaptureApiAsync("reset-invalid-code", reset).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task RequestPasswordResetUnknownUserReturnsBadRequest()
    {
        BeginScenario();
        var anon = new E2eApiClient(Transcript, Context.Normalizer);

        Transcript.Describe("Password reset request for unknown user returns 400");
        var requestReset = await anon.PostAsync("/signin/requestresetpassword", new
        {
            username = $"missing-{Context.RunSuffix}",
            email = $"missing-{Context.RunSuffix}@example.com",
        }).ConfigureAwait(false);
        Assert.Equal(400, requestReset.StatusCode);
        await Baselines.CaptureApiAsync("request-reset-unknown-user", requestReset).ConfigureAwait(false);
    }

    private static string ParseThreeSegmentCode(string htmlBody)
    {
        var match = Regex.Match(htmlBody, @"<strong>(\d{3}-\d{3}-\d{3})</strong>", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            throw new InvalidOperationException("Could not parse three-segment code from captured email.");
        }

        return match.Groups[1].Value;
    }
}
