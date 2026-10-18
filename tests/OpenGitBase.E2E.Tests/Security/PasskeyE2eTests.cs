using System.Text.Json;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.E2E.Core;

namespace OpenGitBase.E2E.Tests.Security;

/// <summary>
/// mfa-03 on the real stack (docker-compose.e2e.yml sets <c>WebAuthn:RelyingPartyId</c> to <c>localhost</c>):
/// register a software passkey, confirm a later session with it, and remove it, through both API replicas
/// and Postgres.
/// </summary>
[Collection("Compose")]
[Trait("Category", "Auth")]
[Trait("RequiresCompose", "true")]
[E2eTier(1)]
public class PasskeyE2eTests : E2eTestBase
{
    private const string Password = "Password123!";

    [RequiresComposeFact]
    public async Task RegisterPasskey_ReauthenticateWithIt_ThenRemoveIt()
    {
        BeginScenario();
        var username = $"mfa-passkey-{Context.RunSuffix}";
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

        Transcript.Describe("Re-authenticate with the password, then register a passkey");
        Assert.Equal(204, (await session.PostAsync("/account/reauthenticate", new { password = Password }).ConfigureAwait(false)).StatusCode);
        var options = await session.PostAsync("/account/passkeys/registration-options").ConfigureAwait(false);
        Assert.Equal(200, options.StatusCode);
        using (var optionsJson = JsonDocument.Parse(options.Body))
        {
            Assert.Equal("localhost", optionsJson.RootElement.GetProperty("rp").GetProperty("id").GetString());
            Assert.Equal(
                "required",
                optionsJson.RootElement.GetProperty("authenticatorSelection").GetProperty("residentKey").GetString()
            );
        }

        var created = await session.PostAsync("/account/passkeys", new
        {
            name = "E2E laptop",
            attestationResponse = JsonDocument.Parse(passkey.CreateCredential(options.Body)).RootElement,
        }).ConfigureAwait(false);
        Assert.Equal(201, created.StatusCode);
        using var createdJson = JsonDocument.Parse(created.Body);
        var passkeyId = createdJson.RootElement.GetProperty("id").GetString()!;
        var security = await session.GetAsync("/account/security").ConfigureAwait(false);
        Assert.Contains("\"passkeysAvailable\":true", security.Body, StringComparison.Ordinal);

        Transcript.Describe("A new session must re-authenticate; the passkey replaces the password");
        var login = await anon.PostAsync("/signin/login", new { username, password = Password }).ConfigureAwait(false);
        Assert.Equal(200, login.StatusCode);
        using var later = new E2eApiClient(Transcript, Context.Normalizer, E2eScenarioHelpers.ParseJwtToken(login));
        Assert.Equal(403, (await later.PostAsync("/account/passkeys/registration-options").ConfigureAwait(false)).StatusCode);
        var requestOptions = await later.PostAsync("/account/reauthenticate/passkey-options").ConfigureAwait(false);
        Assert.Equal(200, requestOptions.StatusCode);
        var assertion = JsonDocument.Parse(passkey.GetAssertion(requestOptions.Body)).RootElement;
        var reauthenticated = await later.PostAsync("/account/reauthenticate", new { passkeyAssertion = assertion }).ConfigureAwait(false);
        Assert.Equal(204, reauthenticated.StatusCode);

        Transcript.Describe("The same assertion does not work twice");
        var replay = await later.PostAsync("/account/reauthenticate", new { passkeyAssertion = assertion }).ConfigureAwait(false);
        Assert.Equal(401, replay.StatusCode);

        Transcript.Describe("Remove the passkey with the passkey-confirmed session");
        var list = await later.GetAsync("/account/passkeys").ConfigureAwait(false);
        Assert.Contains("\"lastUsedAt\":\"", list.Body, StringComparison.Ordinal);
        var removed = await later.SendAsync(HttpMethod.Delete, $"/account/passkeys/{passkeyId}").ConfigureAwait(false);
        Assert.Equal(204, removed.StatusCode);
        Assert.Equal("[]", (await later.GetAsync("/account/passkeys").ConfigureAwait(false)).Body);
    }
}
