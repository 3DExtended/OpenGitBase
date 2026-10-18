using System.Text.Json;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.E2E.Core;

namespace OpenGitBase.E2E.Tests.Security;

/// <summary>
/// mfa-05 on the real stack: an account turns 2FA on with its passkey alone, then signs in with the
/// password plus a security-key touch, and cannot delete its last second factor.
/// </summary>
[Collection("Compose")]
[Trait("Category", "Auth")]
[Trait("RequiresCompose", "true")]
[E2eTier(1)]
public class PasskeyTwoFactorE2eTests : E2eTestBase
{
    private const string Password = "Password123!";

    [RequiresComposeFact]
    public async Task PasskeyOnlyTwoFactor_PasswordPlusPasskey_SignsIn()
    {
        BeginScenario();
        var username = $"mfa-passkey-2fa-{Context.RunSuffix}";
        using var securityKey = new SoftwarePasskeyAuthenticator($"http://localhost:{E2eEnvironment.GitHttpPort}");
        var anon = new E2eApiClient(Transcript, Context.Normalizer);
        var register = await anon.PostAsync("/register/register", new
        {
            username,
            email = $"{username}@example.com",
            password = Password,
        }).ConfigureAwait(false);
        Assert.True(register.StatusCode is 200 or 201, register.Body);
        var registrationToken = E2eScenarioHelpers.ParseJwtToken(register);
        using var session = new E2eApiClient(Transcript, Context.Normalizer, registrationToken);

        Transcript.Describe("Add a passkey, then turn 2FA on with it alone");
        Assert.Equal(204, (await session.PostAsync("/account/reauthenticate", new { password = Password }).ConfigureAwait(false)).StatusCode);
        var creationOptions = await session.PostAsync("/account/passkeys/registration-options").ConfigureAwait(false);
        Assert.Equal(200, creationOptions.StatusCode);
        var created = await session.PostAsync("/account/passkeys", new
        {
            name = "E2E security key",
            attestationResponse = JsonDocument.Parse(securityKey.CreateCredential(creationOptions.Body)).RootElement,
        }).ConfigureAwait(false);
        Assert.Equal(201, created.StatusCode);
        var enabled = await session.PostAsync("/account/two-factor/enable").ConfigureAwait(false);
        Assert.Equal(200, enabled.StatusCode);
        using (var enabledJson = JsonDocument.Parse(enabled.Body))
        {
            Assert.True(enabledJson.RootElement.GetProperty("twoFactorEnabled").GetBoolean());
            Assert.Equal(10, enabledJson.RootElement.GetProperty("recoveryCodes").GetArrayLength());
        }

        // A fresh client: the enabling one already holds the re-issued session cookie, which wins over the header.
        Transcript.Describe("Turning 2FA on revoked the registration session");
        using var revoked = new E2eApiClient(Transcript, Context.Normalizer, registrationToken);
        Assert.Equal(401, (await revoked.GetAsync("/account/me").ConfigureAwait(false)).StatusCode);

        Transcript.Describe("The password yields a challenge that offers the passkey and recovery codes");
        var login = await anon.PostAsync("/signin/login", new { username, password = Password }).ConfigureAwait(false);
        Assert.Equal(200, login.StatusCode);
        using var challengeJson = JsonDocument.Parse(login.Body);
        Assert.True(challengeJson.RootElement.GetProperty("twoFactorRequired").GetBoolean());
        Assert.Equal(
            ["passkey", "recovery"],
            challengeJson.RootElement.GetProperty("methods").EnumerateArray().Select(method => method.GetString()).ToArray()
        );
        var challengeToken = challengeJson.RootElement.GetProperty("challengeToken").GetString()!;

        Transcript.Describe("Answer the second step with a touch of the security key");
        var requestOptions = await anon.PostAsync("/signin/two-factor/passkey-options", new { challengeToken }).ConfigureAwait(false);
        Assert.Equal(200, requestOptions.StatusCode);
        using (var optionsJson = JsonDocument.Parse(requestOptions.Body))
        {
            Assert.Equal("preferred", optionsJson.RootElement.GetProperty("userVerification").GetString());
        }

        securityKey.UserVerified = false;
        var signedIn = await anon.PostAsync("/signin/two-factor", new
        {
            challengeToken,
            passkeyAssertion = JsonDocument.Parse(securityKey.GetAssertion(requestOptions.Body)).RootElement,
        }).ConfigureAwait(false);
        Assert.Equal(200, signedIn.StatusCode);
        using var twoStepSession = new E2eApiClient(Transcript, Context.Normalizer, E2eScenarioHelpers.ParseJwtToken(signedIn));
        var me = await twoStepSession.GetAsync("/account/me").ConfigureAwait(false);
        Assert.Equal(200, me.StatusCode);
        Assert.Contains("\"twoFactorEnabled\":true", me.Body, StringComparison.Ordinal);

        Transcript.Describe("Password plus passkey confirms the session, but the last passkey stays");
        var reauthOptions = await twoStepSession.PostAsync("/account/reauthenticate/passkey-options").ConfigureAwait(false);
        Assert.Equal(200, reauthOptions.StatusCode);
        var reauthenticated = await twoStepSession.PostAsync("/account/reauthenticate", new
        {
            password = Password,
            passkeyAssertion = JsonDocument.Parse(securityKey.GetAssertion(reauthOptions.Body)).RootElement,
        }).ConfigureAwait(false);
        Assert.Equal(204, reauthenticated.StatusCode);
        var passkeys = await twoStepSession.GetAsync("/account/passkeys").ConfigureAwait(false);
        using var passkeysJson = JsonDocument.Parse(passkeys.Body);
        var passkeyId = Assert.Single(passkeysJson.RootElement.EnumerateArray()).GetProperty("id").GetString();
        var deleted = await twoStepSession.SendAsync(HttpMethod.Delete, $"/account/passkeys/{passkeyId}").ConfigureAwait(false);
        Assert.Equal(409, deleted.StatusCode);
        Assert.Contains("last_second_factor", deleted.Body, StringComparison.Ordinal);
    }
}
