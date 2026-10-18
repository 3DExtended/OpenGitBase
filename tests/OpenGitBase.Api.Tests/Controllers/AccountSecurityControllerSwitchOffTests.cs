using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Tests.Base;

namespace OpenGitBase.Api.Tests.Controllers;

/// <summary><c>TwoFactor:Enabled=false</c>: nobody can turn 2FA on, and nobody who has it loses it.</summary>
public class AccountSecurityControllerSwitchOffTests : TwoFactorControllerTestBase
{
    public AccountSecurityControllerSwitchOffTests(WebApplicationFactory<ApiEntryPoint> factory)
        : base(factory, twoFactorEnabled: false) { }

    [Fact]
    public async Task SetupAndConfirm_AreNotFound()
    {
        using var browser = NewBrowser();
        await RegisterAsync(browser, "earlybird");
        (await ReauthenticateAsync(browser)).EnsureSuccessStatusCode();

        using var passkey = NewPasskey();
        (await RegisterPasskeyAsync(browser, passkey)).EnsureSuccessStatusCode();

        var setup = await browser.PostAsync("/account/two-factor/totp/setup", null);
        var confirm = await browser.PostAsJsonAsync(
            "/account/two-factor/totp/confirm",
            new ConfirmTotpSetupDto { Code = "123456" }
        );
        var enableWithPasskeys = await browser.PostAsync("/account/two-factor/enable", null);

        Assert.Equal(HttpStatusCode.NotFound, setup.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, confirm.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, enableWithPasskeys.StatusCode);
        Assert.Null((await GetCredentialsAsync("earlybird")).TwoFactorEnabledAt);
        var status = await browser.GetFromJsonAsync<AccountSecurityResponse>("/account/security");
        Assert.False(status?.TwoFactorAvailable);
    }

    [Fact]
    public async Task AccountWithTwoFactor_KeepsTheSecondStepAndCanStillDisableIt()
    {
        using var setup = NewBrowser();
        await RegisterAsync(setup, "keepsit");
        var secret = await EnableTotpAsync("keepsit");
        using var browser = NewBrowser();

        await SignInWithCodeAsync(browser, "keepsit", secret);
        (await ReauthenticateAsync(browser, code: NextCode(secret))).EnsureSuccessStatusCode();
        var disable = await browser.PostAsync("/account/two-factor/disable", null);

        disable.EnsureSuccessStatusCode();
        Assert.Null((await GetCredentialsAsync("keepsit")).TwoFactorEnabledAt);
    }

    [Fact]
    public async Task AccountWithTwoFactor_CanStillGetAndUseRecoveryCodes()
    {
        using var setup = NewBrowser();
        await RegisterAsync(setup, "stillrecovers");

        var (_, codes) = await EnableTotpWithRecoveryCodesAsync("stillrecovers");

        Assert.Equal(10, codes.Count);
        using var browser = NewBrowser();
        var challenge = await LoginForChallengeAsync(browser, "stillrecovers");
        (await CompleteTwoFactorWithRecoveryCodeAsync(browser, challenge.ChallengeToken, codes[0])).EnsureSuccessStatusCode();
    }
}
