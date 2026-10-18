using System.Text.Json;
using OpenGitBase.E2E.Core;

namespace OpenGitBase.E2E.Tests.Security;

/// <summary>
/// mfa-01 on the real stack (docker-compose.e2e.yml switches <c>TwoFactor:Enabled</c> on): turn on an
/// authenticator app, then sign in with password plus a computed code through both API replicas.
/// </summary>
[Collection("Compose")]
[Trait("Category", "Auth")]
[Trait("RequiresCompose", "true")]
[E2eTier(1)]
public class TwoFactorTotpE2eTests : E2eTestBase
{
    private const string Password = "Password123!";

    [RequiresComposeFact]
    public async Task EnableTotp_SignOut_SignInWithComputedCode()
    {
        BeginScenario();
        var username = $"mfa-totp-{Context.RunSuffix}";
        var anon = new E2eApiClient(Transcript, Context.Normalizer);
        var register = await anon.PostAsync("/register/register", new
        {
            username,
            email = $"{username}@example.com",
            password = Password,
        }).ConfigureAwait(false);
        Assert.True(register.StatusCode is 200 or 201, register.Body);
        using var session = new E2eApiClient(Transcript, Context.Normalizer, E2eScenarioHelpers.ParseJwtToken(register));

        Transcript.Describe("Re-authenticate, then set up the authenticator app");
        var reauth = await session.PostAsync("/account/reauthenticate", new { password = Password }).ConfigureAwait(false);
        Assert.Equal(204, reauth.StatusCode);
        var setup = await session.PostAsync("/account/two-factor/totp/setup").ConfigureAwait(false);
        Assert.Equal(200, setup.StatusCode);
        using var setupJson = JsonDocument.Parse(setup.Body);
        var secret = setupJson.RootElement.GetProperty("secret").GetString()!;
        Assert.StartsWith("otpauth://totp/", setupJson.RootElement.GetProperty("otpauthUri").GetString());

        // The API accepts one step either side; the next step's code is valid now and newer than this one.
        var step = TotpCodes.StepAt(DateTimeOffset.UtcNow);
        var confirm = await session.PostAsync(
            "/account/two-factor/totp/confirm",
            new { code = TotpCodes.ForStep(secret, step) }
        ).ConfigureAwait(false);
        Assert.Equal(200, confirm.StatusCode);

        Transcript.Describe("Sign in again: the password alone only yields a challenge");
        var login = await anon.PostAsync("/signin/login", new { username, password = Password }).ConfigureAwait(false);
        Assert.Equal(200, login.StatusCode);
        using var challenge = JsonDocument.Parse(login.Body);
        Assert.True(challenge.RootElement.GetProperty("twoFactorRequired").GetBoolean());
        var challengeToken = challenge.RootElement.GetProperty("challengeToken").GetString()!;

        var nextCode = TotpCodes.ForStep(secret, step + 1);
        var secondStep = await anon.PostAsync("/signin/two-factor", new { challengeToken, code = nextCode }).ConfigureAwait(false);
        Assert.Equal(200, secondStep.StatusCode);
        using var signedIn = new E2eApiClient(Transcript, Context.Normalizer, E2eScenarioHelpers.ParseJwtToken(secondStep));
        var me = await signedIn.GetAsync("/account/me").ConfigureAwait(false);
        Assert.Equal(200, me.StatusCode);
        Assert.Contains("\"twoFactorEnabled\":true", me.Body, StringComparison.Ordinal);

        Transcript.Describe("The same code does not work twice");
        var relogin = await anon.PostAsync("/signin/login", new { username, password = Password }).ConfigureAwait(false);
        using var secondChallenge = JsonDocument.Parse(relogin.Body);
        var replay = await anon.PostAsync("/signin/two-factor", new
        {
            challengeToken = secondChallenge.RootElement.GetProperty("challengeToken").GetString(),
            code = nextCode,
        }).ConfigureAwait(false);
        Assert.Equal(401, replay.StatusCode);
    }
}
