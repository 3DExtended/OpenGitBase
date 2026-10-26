using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Tests.Base;
using OpenGitBase.Common.Tests.Testing;

namespace OpenGitBase.Api.Tests.Controllers;

public class PasskeysControllerTests : TwoFactorControllerTestBase
{
    public PasskeysControllerTests(WebApplicationFactory<ApiEntryPoint> factory)
        : base(factory) { }

    [Fact]
    public async Task Register_WithReauthentication_StoresAndListsThePasskey()
    {
        using var browser = NewBrowser();
        await RegisterAsync(browser, "keyholder");
        (await ReauthenticateAsync(browser)).EnsureSuccessStatusCode();
        using var passkey = NewPasskey();

        var response = await RegisterPasskeyAsync(browser, passkey, "MacBook");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<PasskeyResponse>();
        Assert.Equal("MacBook", created?.Name);
        var list = await browser.GetFromJsonAsync<List<PasskeyResponse>>("/account/passkeys");
        Assert.Equal(created!.Id, Assert.Single(list!).Id);
        var status = await browser.GetFromJsonAsync<AccountSecurityResponse>("/account/security");
        Assert.True(status?.PasskeysAvailable);
    }

    [Fact]
    public async Task RegistrationOptions_ExcludeTheExistingPasskeys()
    {
        using var browser = NewBrowser();
        await RegisterAsync(browser, "excluder");
        (await ReauthenticateAsync(browser)).EnsureSuccessStatusCode();
        using var passkey = NewPasskey();
        (await RegisterPasskeyAsync(browser, passkey)).EnsureSuccessStatusCode();

        var options = await browser.PostAsync("/account/passkeys/registration-options", null);

        using var json = JsonDocument.Parse(await options.Content.ReadAsStringAsync());
        var excluded = Assert.Single(json.RootElement.GetProperty("excludeCredentials").EnumerateArray());
        Assert.Equal(SoftwarePasskeyAuthenticator.ToBase64Url(passkey.CredentialId), excluded.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Registration_WithoutReauthentication_IsForbidden()
    {
        using var browser = NewBrowser();
        await RegisterAsync(browser, "hasty");
        using var otherSession = NewBrowser();
        (await LoginAsync(otherSession, "hasty")).EnsureSuccessStatusCode();
        (await ReauthenticateAsync(browser)).EnsureSuccessStatusCode();
        var options = await (await browser.PostAsync("/account/passkeys/registration-options", null)).Content.ReadAsStringAsync();
        using var passkey = NewPasskey();

        var optionsWithout = await otherSession.PostAsync("/account/passkeys/registration-options", null);
        var registerWithout = await PostAttestationAsync(otherSession, passkey.CreateCredential(options));

        await AssertErrorAsync(optionsWithout, HttpStatusCode.Forbidden, SecondFactorErrorResponse.ReauthenticationRequired);
        await AssertErrorAsync(registerWithout, HttpStatusCode.Forbidden, SecondFactorErrorResponse.ReauthenticationRequired);
    }

    [Fact]
    public async Task Register_WrongOrigin_IsRejected()
    {
        using var browser = NewBrowser();
        await RegisterAsync(browser, "phished");
        (await ReauthenticateAsync(browser)).EnsureSuccessStatusCode();
        using var passkey = NewPasskey();
        passkey.Origin = "https://opengitbase.evil.example";

        var response = await RegisterPasskeyAsync(browser, passkey);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, SecondFactorErrorResponse.PasskeyRejected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a name that is far too long for anyone to read in a list of passkeys, really")]
    public async Task Register_InvalidName_IsBadRequest(string name)
    {
        using var browser = NewBrowser();
        await RegisterAsync(browser, "unnamed" + name.Length);
        (await ReauthenticateAsync(browser)).EnsureSuccessStatusCode();
        using var passkey = NewPasskey();

        var response = await RegisterPasskeyAsync(browser, passkey, name);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, SecondFactorErrorResponse.InvalidName);
    }

    [Fact]
    public async Task Rename_NeedsOnlyASession()
    {
        using var setup = NewBrowser();
        await RegisterAsync(setup, "renamer");
        (await ReauthenticateAsync(setup)).EnsureSuccessStatusCode();
        using var passkey = NewPasskey();
        var created = await (await RegisterPasskeyAsync(setup, passkey)).Content.ReadFromJsonAsync<PasskeyResponse>();
        using var browser = NewBrowser();
        (await LoginAsync(browser, "renamer")).EnsureSuccessStatusCode();

        var response = await browser.PatchAsJsonAsync($"/account/passkeys/{created!.Id}", new RenamePasskeyDto { Name = "Phone" });

        response.EnsureSuccessStatusCode();
        Assert.Equal("Phone", (await response.Content.ReadFromJsonAsync<PasskeyResponse>())?.Name);
    }

    [Fact]
    public async Task Delete_NeedsReauthentication()
    {
        using var setup = NewBrowser();
        await RegisterAsync(setup, "deleter");
        (await ReauthenticateAsync(setup)).EnsureSuccessStatusCode();
        using var passkey = NewPasskey();
        var created = await (await RegisterPasskeyAsync(setup, passkey)).Content.ReadFromJsonAsync<PasskeyResponse>();
        using var browser = NewBrowser();
        (await LoginAsync(browser, "deleter")).EnsureSuccessStatusCode();

        var withoutReauthentication = await browser.DeleteAsync($"/account/passkeys/{created!.Id}");
        (await ReauthenticateAsync(browser)).EnsureSuccessStatusCode();
        var withReauthentication = await browser.DeleteAsync($"/account/passkeys/{created.Id}");

        await AssertErrorAsync(withoutReauthentication, HttpStatusCode.Forbidden, SecondFactorErrorResponse.ReauthenticationRequired);
        Assert.Equal(HttpStatusCode.NoContent, withReauthentication.StatusCode);
        Assert.Empty((await browser.GetFromJsonAsync<List<PasskeyResponse>>("/account/passkeys"))!);
    }

    [Fact]
    public async Task AnotherUsersPasskey_CannotBeSeenRenamedOrDeleted()
    {
        using var alice = NewBrowser();
        await RegisterAsync(alice, "alicekeys");
        (await ReauthenticateAsync(alice)).EnsureSuccessStatusCode();
        using var passkey = NewPasskey();
        var created = await (await RegisterPasskeyAsync(alice, passkey)).Content.ReadFromJsonAsync<PasskeyResponse>();
        using var mallory = NewBrowser();
        await RegisterAsync(mallory, "mallorykeys");
        (await ReauthenticateAsync(mallory)).EnsureSuccessStatusCode();

        var list = await mallory.GetFromJsonAsync<List<PasskeyResponse>>("/account/passkeys");
        var rename = await mallory.PatchAsJsonAsync($"/account/passkeys/{created!.Id}", new RenamePasskeyDto { Name = "Mine" });
        var delete = await mallory.DeleteAsync($"/account/passkeys/{created.Id}");

        Assert.Empty(list!);
        Assert.Equal(HttpStatusCode.NotFound, rename.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Single((await alice.GetFromJsonAsync<List<PasskeyResponse>>("/account/passkeys"))!);
    }

    [Fact]
    public async Task ReauthenticateWithPasskey_UnlocksSensitiveActions()
    {
        using var setup = NewBrowser();
        await RegisterAsync(setup, "passkeyreauth");
        (await ReauthenticateAsync(setup)).EnsureSuccessStatusCode();
        using var passkey = NewPasskey();
        (await RegisterPasskeyAsync(setup, passkey)).EnsureSuccessStatusCode();
        using var browser = NewBrowser();
        (await LoginAsync(browser, "passkeyreauth")).EnsureSuccessStatusCode();
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await browser.PostAsync("/account/passkeys/registration-options", null)).StatusCode
        );

        var reauthenticated = await ReauthenticateWithPasskeyAsync(browser, passkey);

        Assert.Equal(HttpStatusCode.NoContent, reauthenticated.StatusCode);
        (await browser.PostAsync("/account/passkeys/registration-options", null)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ReauthenticateWithAnotherUsersPasskey_IsRejected()
    {
        using var alice = NewBrowser();
        await RegisterAsync(alice, "aliceassert");
        (await ReauthenticateAsync(alice)).EnsureSuccessStatusCode();
        using var alicesPasskey = NewPasskey();
        (await RegisterPasskeyAsync(alice, alicesPasskey)).EnsureSuccessStatusCode();
        using var mallory = NewBrowser();
        await RegisterAsync(mallory, "malloryassert");
        (await ReauthenticateAsync(mallory)).EnsureSuccessStatusCode();
        using var mallorysPasskey = NewPasskey();
        (await RegisterPasskeyAsync(mallory, mallorysPasskey)).EnsureSuccessStatusCode();
        using var aliceLater = NewBrowser();
        (await LoginAsync(aliceLater, "aliceassert")).EnsureSuccessStatusCode();

        var response = await ReauthenticateWithPasskeyAsync(aliceLater, mallorysPasskey);

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, SecondFactorErrorResponse.PasskeyRejected);
    }

    [Fact]
    public async Task PasskeyOptionsForReauthentication_WithoutPasskeys_IsBadRequest()
    {
        using var browser = NewBrowser();
        await RegisterAsync(browser, "nokeys");

        var response = await browser.PostAsync("/account/reauthenticate/passkey-options", null);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, SecondFactorErrorResponse.NoPasskeys);
    }

    [Fact]
    public async Task GoogleOnlyAccount_RegistersAPasskeyRightAfterSigningIn()
    {
        const string registrationToken = "passkey-google-token";
        SeedProviderRegistration(registrationToken, "google" + "passkey-google-subject", "pkgoogle@example.com");
        using var browser = NewBrowser();
        (await browser.PostAsJsonAsync(
            "/register/openprovider",
            new OpenProviderRegisterDto { Username = "pkgoogle", RegistrationToken = registrationToken }
        )).EnsureSuccessStatusCode();
        using var passkey = NewPasskey();

        var response = await RegisterPasskeyAsync(browser, passkey);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string error)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SecondFactorErrorResponse>();
        Assert.Equal(error, body?.Error);
    }
}
