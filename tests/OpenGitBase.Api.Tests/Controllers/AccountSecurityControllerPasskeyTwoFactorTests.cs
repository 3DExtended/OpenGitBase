using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Tests.Base;
using OpenGitBase.Common.Tests.Testing;

namespace OpenGitBase.Api.Tests.Controllers;

/// <summary>2FA with passkeys as the only second factor: turning it on, signing in, and keeping a factor.</summary>
public class AccountSecurityControllerPasskeyTwoFactorTests : TwoFactorControllerTestBase
{
    public AccountSecurityControllerPasskeyTwoFactorTests(WebApplicationFactory<ApiEntryPoint> factory)
        : base(factory) { }

    [Fact]
    public async Task Enable_WithAPasskey_TurnsTwoFactorOn_RevokesOtherSessionsAndKeepsTheCallerSignedIn()
    {
        using var browser = NewBrowser();
        await RegisterAsync(browser, "passkeyonly");
        using var otherDevice = NewBrowser();
        var otherSession = await (await LoginAsync(otherDevice, "passkeyonly")).Content.ReadAsStringAsync();
        Clock.Advance(TimeSpan.FromMilliseconds(5));
        (await ReauthenticateAsync(browser)).EnsureSuccessStatusCode();
        using var passkey = NewPasskey();
        (await RegisterPasskeyAsync(browser, passkey)).EnsureSuccessStatusCode();

        var response = await browser.PostAsync("/account/two-factor/enable", null);

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.Contains("Set-Cookie"));
        var body = await response.Content.ReadFromJsonAsync<TwoFactorStatusResponse>();
        Assert.True(body?.TwoFactorEnabled);
        Assert.Equal(10, body?.RecoveryCodes?.Distinct().Count());
        var status = await browser.GetFromJsonAsync<AccountSecurityResponse>("/account/security");
        Assert.True(status?.TwoFactorEnabled);
        Assert.False(status?.TotpConfigured);
        Assert.Equal(10, status?.RecoveryCodesRemaining);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await SendWithBearerOnlyAsync(HttpMethod.Get, "/signin/testlogin", otherSession)).StatusCode
        );
    }

    [Fact]
    public async Task Enable_WithoutReauthentication_IsForbidden()
    {
        using var setup = NewBrowser();
        await RegisterAsync(setup, "enablestale");
        (await ReauthenticateAsync(setup)).EnsureSuccessStatusCode();
        using var passkey = NewPasskey();
        (await RegisterPasskeyAsync(setup, passkey)).EnsureSuccessStatusCode();
        using var browser = NewBrowser();
        (await LoginAsync(browser, "enablestale")).EnsureSuccessStatusCode();

        var response = await browser.PostAsync("/account/two-factor/enable", null);

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, SecondFactorErrorResponse.ReauthenticationRequired);
        Assert.Null((await GetCredentialsAsync("enablestale")).TwoFactorEnabledAt);
    }

    [Fact]
    public async Task Enable_WithoutPasskeys_IsBadRequest()
    {
        using var browser = NewBrowser();
        await RegisterAsync(browser, "enablenokeys");
        (await ReauthenticateAsync(browser)).EnsureSuccessStatusCode();

        var response = await browser.PostAsync("/account/two-factor/enable", null);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, SecondFactorErrorResponse.NoPasskeys);
    }

    [Fact]
    public async Task Enable_TwoFactorAlreadyOn_IsConflict()
    {
        using var browser = NewBrowser();
        await RegisterAsync(browser, "enabletwice");
        (await ReauthenticateAsync(browser)).EnsureSuccessStatusCode();
        using var passkey = NewPasskey();
        (await RegisterPasskeyAsync(browser, passkey)).EnsureSuccessStatusCode();
        (await browser.PostAsync("/account/two-factor/enable", null)).EnsureSuccessStatusCode();
        // Turning 2FA on issued a new session, which has to confirm itself again.
        (await ReauthenticateWithPasskeyAsync(browser, passkey)).EnsureSuccessStatusCode();

        var response = await browser.PostAsync("/account/two-factor/enable", null);

        await AssertErrorAsync(response, HttpStatusCode.Conflict, SecondFactorErrorResponse.TwoFactorAlreadyEnabled);
    }

    [Fact]
    public async Task PasskeyOnlyTwoFactor_SignInOffersPasskeyAndRecoveryAndCompletesWithThePasskey()
    {
        using var passkey = NewPasskey();
        await RegisterWithPasskeyOnlyTwoFactorAsync("pkonlysignin", passkey);
        using var browser = NewBrowser();

        var challenge = await LoginForChallengeAsync(browser, "pkonlysignin");
        var response = await CompleteTwoFactorWithPasskeyAsync(browser, challenge.ChallengeToken, passkey);

        Assert.Equal(["passkey", "recovery"], challenge.Methods);
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/signin/testlogin")).StatusCode);
    }

    [Fact]
    public async Task Disable_AfterPasswordPlusPasskey_TurnsTwoFactorOffAndThePasskeyStillSignsIn()
    {
        using var passkey = NewPasskey();
        await RegisterWithPasskeyOnlyTwoFactorAsync("pkonlydisable", passkey);
        using var browser = NewBrowser();
        var challenge = await LoginForChallengeAsync(browser, "pkonlydisable");
        (await CompleteTwoFactorWithPasskeyAsync(browser, challenge.ChallengeToken, passkey)).EnsureSuccessStatusCode();
        // A security key only proves presence; with the password that is enough.
        passkey.UserVerified = false;

        var reauthenticated = await ReauthenticateWithPasswordAndPasskeyAsync(browser, passkey);
        var disabled = await browser.PostAsync("/account/two-factor/disable", null);

        Assert.Equal(HttpStatusCode.NoContent, reauthenticated.StatusCode);
        disabled.EnsureSuccessStatusCode();
        Assert.Null((await GetCredentialsAsync("pkonlydisable")).TwoFactorEnabledAt);
        passkey.UserVerified = true;
        var options = await NewBrowser().PostAsync("/signin/passkey/options", null);
        var signIn = await NewBrowser().PostAsJsonAsync(
            "/signin/passkey",
            new PasskeySignInDto
            {
                Assertion = JsonDocument.Parse(passkey.GetAssertion(await options.Content.ReadAsStringAsync())).RootElement,
            }
        );
        signIn.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Reauthenticate_WrongPasswordPlusPasskey_IsUnauthorized()
    {
        using var passkey = NewPasskey();
        await RegisterWithPasskeyOnlyTwoFactorAsync("pkonlywrongpw", passkey);
        using var browser = NewBrowser();
        var challenge = await LoginForChallengeAsync(browser, "pkonlywrongpw");
        (await CompleteTwoFactorWithPasskeyAsync(browser, challenge.ChallengeToken, passkey)).EnsureSuccessStatusCode();

        var response = await ReauthenticateWithPasswordAndPasskeyAsync(browser, passkey, "not the password");

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, SecondFactorErrorResponse.InvalidCredentials);
    }

    [Fact]
    public async Task DeletePasskey_LastSecondFactor_IsConflictAndKeepsIt()
    {
        using var passkey = NewPasskey();
        await RegisterWithPasskeyOnlyTwoFactorAsync("pkonlydelete", passkey);
        using var browser = NewBrowser();
        var challenge = await LoginForChallengeAsync(browser, "pkonlydelete");
        (await CompleteTwoFactorWithPasskeyAsync(browser, challenge.ChallengeToken, passkey)).EnsureSuccessStatusCode();
        (await ReauthenticateWithPasskeyAsync(browser, passkey)).EnsureSuccessStatusCode();
        var listed = await browser.GetFromJsonAsync<List<PasskeyResponse>>("/account/passkeys");

        var response = await browser.DeleteAsync($"/account/passkeys/{Assert.Single(listed!).Id}");

        await AssertErrorAsync(response, HttpStatusCode.Conflict, SecondFactorErrorResponse.LastSecondFactor);
        Assert.Single((await browser.GetFromJsonAsync<List<PasskeyResponse>>("/account/passkeys"))!);
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string error)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SecondFactorErrorResponse>();
        Assert.Equal(error, body?.Error);
    }

    /// <summary>Registers <paramref name="username"/> with <paramref name="passkey"/> and turns 2FA on with passkeys only.</summary>
    private async Task RegisterWithPasskeyOnlyTwoFactorAsync(string username, SoftwarePasskeyAuthenticator passkey)
    {
        using var browser = NewBrowser();
        await RegisterAsync(browser, username);
        (await ReauthenticateAsync(browser)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Created, (await RegisterPasskeyAsync(browser, passkey)).StatusCode);
        (await browser.PostAsync("/account/two-factor/enable", null)).EnsureSuccessStatusCode();
    }
}
