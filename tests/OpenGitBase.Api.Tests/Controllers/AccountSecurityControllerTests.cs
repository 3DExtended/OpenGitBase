using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Tests.Base;

namespace OpenGitBase.Api.Tests.Controllers;

public class AccountSecurityControllerTests : TwoFactorControllerTestBase
{
    public AccountSecurityControllerTests(WebApplicationFactory<ApiEntryPoint> factory)
        : base(factory) { }

    [Fact]
    public async Task GetSecurity_NewAccount_ReportsTwoFactorOffAndAvailable()
    {
        using var browser = NewBrowser();
        await RegisterAsync(browser, "fresh");

        var status = await browser.GetFromJsonAsync<AccountSecurityResponse>("/account/security");

        Assert.NotNull(status);
        Assert.True(status.TwoFactorAvailable);
        Assert.False(status.TwoFactorEnabled);
        Assert.False(status.TotpConfigured);
        Assert.True(status.HasPassword);
    }

    [Fact]
    public async Task GetSecurity_Anonymous_IsUnauthorized()
    {
        var response = await NewBrowser().GetAsync("/account/security");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Setup_WithoutReauthentication_IsForbidden()
    {
        using var browser = NewBrowser();
        await RegisterAsync(browser, "hasty");

        var response = await browser.PostAsync("/account/two-factor/totp/setup", null);

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, SecondFactorErrorResponse.ReauthenticationRequired);
    }

    [Fact]
    public async Task Reauthenticate_WrongPassword_IsUnauthorized()
    {
        using var browser = NewBrowser();
        await RegisterAsync(browser, "typo");

        var response = await ReauthenticateAsync(browser, "wrong");

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, SecondFactorErrorResponse.InvalidCredentials);
    }

    [Fact]
    public async Task EnableFlow_TurnsTwoFactorOn_RevokesOtherSessionsAndKeepsTheCallerSignedIn()
    {
        using var browser = NewBrowser();
        await RegisterAsync(browser, "enabler");
        using var otherDevice = NewBrowser();
        var otherSession = await (await LoginAsync(otherDevice, "enabler")).Content.ReadAsStringAsync();
        Clock.Advance(TimeSpan.FromMilliseconds(5));
        (await ReauthenticateAsync(browser)).EnsureSuccessStatusCode();

        var setupResponse = await browser.PostAsync("/account/two-factor/totp/setup", null);
        setupResponse.EnsureSuccessStatusCode();
        var setup = await setupResponse.Content.ReadFromJsonAsync<TotpSetupResponse>();
        Assert.NotNull(setup);
        Assert.StartsWith("otpauth://totp/OpenGitBase:enabler?", setup.OtpauthUri);
        var confirm = await browser.PostAsJsonAsync(
            "/account/two-factor/totp/confirm",
            new ConfirmTotpSetupDto { Code = NextCode(setup.Secret) }
        );

        confirm.EnsureSuccessStatusCode();
        Assert.True(confirm.Headers.Contains("Set-Cookie"));
        var me = await browser.GetFromJsonAsync<AccountMeResponse>("/account/me");
        Assert.True(me?.TwoFactorEnabled);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await SendWithBearerOnlyAsync(HttpMethod.Get, "/signin/testlogin", otherSession)).StatusCode
        );
        var challenge = await LoginForChallengeAsync(NewBrowser(), "enabler");
        Assert.Equal(["totp"], challenge.Methods);
    }

    [Fact]
    public async Task Confirm_WrongCode_KeepsTwoFactorOff()
    {
        using var browser = NewBrowser();
        await RegisterAsync(browser, "mistyped");
        (await ReauthenticateAsync(browser)).EnsureSuccessStatusCode();
        var setup = await (await browser.PostAsync("/account/two-factor/totp/setup", null))
            .Content.ReadFromJsonAsync<TotpSetupResponse>();

        var confirm = await browser.PostAsJsonAsync(
            "/account/two-factor/totp/confirm",
            new ConfirmTotpSetupDto { Code = WrongCode(NextCode(setup!.Secret)) }
        );

        await AssertErrorAsync(confirm, HttpStatusCode.BadRequest, SecondFactorErrorResponse.InvalidCode);
        var status = await browser.GetFromJsonAsync<AccountSecurityResponse>("/account/security");
        Assert.False(status?.TwoFactorEnabled);
    }

    [Fact]
    public async Task Reauthentication_InOneSession_DoesNotCountInAnother()
    {
        using var first = NewBrowser();
        await RegisterAsync(first, "twodevices");
        using var second = NewBrowser();
        (await LoginAsync(second, "twodevices")).EnsureSuccessStatusCode();

        (await ReauthenticateAsync(first)).EnsureSuccessStatusCode();
        var response = await second.PostAsync("/account/two-factor/totp/setup", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Reauthenticate_WithTwoFactorOn_NeedsTheCode()
    {
        using var setup = NewBrowser();
        await RegisterAsync(setup, "needscode");
        var secret = await EnableTotpAsync("needscode");
        using var browser = NewBrowser();
        await SignInWithCodeAsync(browser, "needscode", secret);

        var withoutCode = await ReauthenticateAsync(browser);
        var withCode = await ReauthenticateAsync(browser, code: NextCode(secret));

        await AssertErrorAsync(withoutCode, HttpStatusCode.Unauthorized, SecondFactorErrorResponse.SecondFactorRequired);
        withCode.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Disable_TurnsTwoFactorOff_RevokesOtherSessionsAndKeepsTheCallerSignedIn()
    {
        using var setup = NewBrowser();
        await RegisterAsync(setup, "disabler");
        var secret = await EnableTotpAsync("disabler");
        using var otherDevice = NewBrowser();
        var otherSession = await SignInWithCodeAsync(otherDevice, "disabler", secret);
        using var browser = NewBrowser();
        await SignInWithCodeAsync(browser, "disabler", secret);
        (await ReauthenticateAsync(browser, code: NextCode(secret))).EnsureSuccessStatusCode();

        var response = await browser.PostAsync("/account/two-factor/disable", null);

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.Contains("Set-Cookie"));
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/signin/testlogin")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await SendWithBearerOnlyAsync(HttpMethod.Get, "/signin/testlogin", otherSession)).StatusCode
        );
        var login = await LoginAsync(NewBrowser(), "disabler");
        Assert.StartsWith("text/plain", login.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task Disable_WithoutReauthentication_IsForbidden()
    {
        using var setup = NewBrowser();
        await RegisterAsync(setup, "stolensession");
        var secret = await EnableTotpAsync("stolensession");
        using var browser = NewBrowser();
        await SignInWithCodeAsync(browser, "stolensession", secret);

        var response = await browser.PostAsync("/account/two-factor/disable", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotNull((await GetCredentialsAsync("stolensession")).TwoFactorEnabledAt);
    }

    [Fact]
    public async Task SetupAgain_ReplacesTheAuthenticatorOnlyOnceConfirmed()
    {
        using var setup = NewBrowser();
        await RegisterAsync(setup, "newphone");
        var oldSecret = await EnableTotpAsync("newphone");
        using var browser = NewBrowser();
        await SignInWithCodeAsync(browser, "newphone", oldSecret);
        (await ReauthenticateAsync(browser, code: NextCode(oldSecret))).EnsureSuccessStatusCode();

        var restart = await (await browser.PostAsync("/account/two-factor/totp/setup", null))
            .Content.ReadFromJsonAsync<TotpSetupResponse>();

        Assert.NotEqual(oldSecret, restart!.Secret);
        // The new phone is not confirmed yet, so the old one still signs in.
        await SignInWithCodeAsync(NewBrowser(), "newphone", oldSecret);
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string error)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SecondFactorErrorResponse>();
        Assert.Equal(error, body?.Error);
    }
}
