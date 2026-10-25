using System.Text.Json;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.E2E.Core;

namespace OpenGitBase.E2E.Tests.Security;

/// <summary>
/// mfa-04 on the real stack: an account with a passkey and 2FA on signs in with the passkey alone,
/// without a username and without a second step, through both API replicas and Postgres.
/// </summary>
[Collection("Compose")]
[Trait("Category", "Auth")]
[Trait("RequiresCompose", "true")]
[E2eTier(1)]
public class PasskeySignInE2eTests : E2eTestBase
{
    private const string Password = "Password123!";

    [RequiresComposeFact]
    public async Task SignInWithPasskey_TwoFactorOn_NoSecondStep()
    {
        BeginScenario();
        var username = $"mfa-passkey-signin-{Context.RunSuffix}";
        using var passkey = new SoftwarePasskeyAuthenticator($"http://localhost:{E2eEnvironment.GitHttpPort}");
        var anon = new E2eApiClient(Transcript, Context.Normalizer);
        var register = await anon.PostAsync("/register/register", new
        {
            username,
            email = $"{username}@example.com",
            password = Password,
        }).ConfigureAwait(false);
        Assert.True(register.StatusCode is 200 or 201, register.Body);
        using var session = new E2eApiClient(Transcript, Context.Normalizer, E2eScenarioHelpers.ParseJwtToken(register));

        Transcript.Describe("Add a passkey, then turn on the authenticator app");
        Assert.Equal(204, (await session.PostAsync("/account/reauthenticate", new { password = Password }).ConfigureAwait(false)).StatusCode);
        var creationOptions = await session.PostAsync("/account/passkeys/registration-options").ConfigureAwait(false);
        Assert.Equal(200, creationOptions.StatusCode);
        var created = await session.PostAsync("/account/passkeys", new
        {
            name = "E2E laptop",
            attestationResponse = JsonDocument.Parse(passkey.CreateCredential(creationOptions.Body)).RootElement,
        }).ConfigureAwait(false);
        Assert.Equal(201, created.StatusCode);
        var setup = await session.PostAsync("/account/two-factor/totp/setup").ConfigureAwait(false);
        Assert.Equal(200, setup.StatusCode);
        using var setupJson = JsonDocument.Parse(setup.Body);
        var secret = setupJson.RootElement.GetProperty("secret").GetString()!;
        var confirm = await session.PostAsync(
            "/account/two-factor/totp/confirm",
            new { code = TotpCodes.ForStep(secret, TotpCodes.StepAt(DateTimeOffset.UtcNow)) }
        ).ConfigureAwait(false);
        Assert.Equal(200, confirm.StatusCode);

        Transcript.Describe("The password alone now only yields a challenge");
        var login = await anon.PostAsync("/signin/login", new { username, password = Password }).ConfigureAwait(false);
        Assert.Contains("\"twoFactorRequired\":true", login.Body, StringComparison.Ordinal);

        Transcript.Describe("Sign in with the passkey: no username, no second step");
        var requestOptions = await anon.PostAsync("/signin/passkey/options").ConfigureAwait(false);
        Assert.Equal(200, requestOptions.StatusCode);
        using (var optionsJson = JsonDocument.Parse(requestOptions.Body))
        {
            Assert.Equal("required", optionsJson.RootElement.GetProperty("userVerification").GetString());
            Assert.DoesNotContain(username, requestOptions.Body, StringComparison.Ordinal);
        }

        var assertion = JsonDocument.Parse(passkey.GetAssertion(requestOptions.Body)).RootElement;
        var signedIn = await anon.PostAsync("/signin/passkey", new { assertion }).ConfigureAwait(false);
        Assert.Equal(200, signedIn.StatusCode);
        using var passkeySession = new E2eApiClient(Transcript, Context.Normalizer, E2eScenarioHelpers.ParseJwtToken(signedIn));
        var me = await passkeySession.GetAsync("/account/me").ConfigureAwait(false);
        Assert.Equal(200, me.StatusCode);
        Assert.Contains("\"twoFactorEnabled\":true", me.Body, StringComparison.Ordinal);
        Assert.Contains($"\"username\":\"{username}\"", me.Body, StringComparison.Ordinal);

        Transcript.Describe("The same assertion does not sign in twice");
        var replay = await anon.PostAsync("/signin/passkey", new { assertion }).ConfigureAwait(false);
        Assert.Equal(401, replay.StatusCode);
        Assert.Contains("passkey_rejected", replay.Body, StringComparison.Ordinal);
    }
}
