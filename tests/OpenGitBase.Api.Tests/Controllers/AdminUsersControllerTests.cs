using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Api.Tests.Base;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Api.Tests.Controllers;

/// <summary>The administrator's way out for a user who lost every second factor.</summary>
public class AdminUsersControllerTests : TwoFactorControllerTestBase
{
    public AdminUsersControllerTests(WebApplicationFactory<ApiEntryPoint> factory)
        : base(factory) { }

    [Fact]
    public async Task ResetTwoFactor_AsAdmin_RemovesTheFactorsRevokesSessionsAndEmailsTheUser()
    {
        using var userBrowser = NewBrowser();
        await RegisterAsync(userBrowser, "lostphone");
        var secret = await EnableTotpAsync("lostphone");
        var userSession = await SignInWithCodeAsync(userBrowser, "lostphone", secret);
        using var admin = await NewAdminBrowserAsync("resetadmin");
        Clock.Advance(TimeSpan.FromMilliseconds(5));

        var response = await admin.PostAsync("/admin/users/lostphone/two-factor/reset", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null((await GetCredentialsAsync("lostphone")).TwoFactorEnabledAt);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await SendWithBearerOnlyAsync(HttpMethod.Get, "/account/me", userSession)).StatusCode
        );
        var email = Assert.Single(
            Emails.GetByRecipient("lostphone@example.com"),
            message => message.Subject == "Two-factor authentication reset by an administrator"
        );
        Assert.Contains("set it up again", email.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResetTwoFactor_Afterwards_ThePasswordSignsInAndTwoFactorCanBeSetUpAgain()
    {
        using var setup = NewBrowser();
        await RegisterAsync(setup, "freshstart");
        await EnableTotpAsync("freshstart");
        using var admin = await NewAdminBrowserAsync("resetadmin2");
        (await admin.PostAsync("/admin/users/freshstart/two-factor/reset", null)).EnsureSuccessStatusCode();
        Clock.Advance(TimeSpan.FromMilliseconds(5));

        using var browser = NewBrowser();
        var login = await LoginAsync(browser, "freshstart");

        login.EnsureSuccessStatusCode();
        Assert.DoesNotContain("twoFactorRequired", await login.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        (await ReauthenticateAsync(browser)).EnsureSuccessStatusCode();
        var totpSetup = await browser.PostAsync("/account/two-factor/totp/setup", null);
        Assert.Equal(HttpStatusCode.OK, totpSetup.StatusCode);
    }

    [Fact]
    public async Task ResetTwoFactor_NonAdmin_IsForbidden()
    {
        using var target = NewBrowser();
        await RegisterAsync(target, "victim");
        await EnableTotpAsync("victim");
        using var browser = NewBrowser();
        await RegisterAsync(browser, "notanadmin");

        var response = await browser.PostAsync("/admin/users/victim/two-factor/reset", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotNull((await GetCredentialsAsync("victim")).TwoFactorEnabledAt);
    }

    [Fact]
    public async Task ResetTwoFactor_Anonymous_IsUnauthorized()
    {
        using var browser = NewBrowser();

        var response = await browser.PostAsync("/admin/users/anyone/two-factor/reset", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ResetTwoFactor_UnknownUser_IsNotFound()
    {
        using var admin = await NewAdminBrowserAsync("resetadmin3");

        var response = await admin.PostAsync("/admin/users/nosuchuser/two-factor/reset", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Registers <paramref name="username"/>, makes it an admin and signs it in again for the claim.</summary>
    private async Task<HttpClient> NewAdminBrowserAsync(string username)
    {
        using (var registration = NewBrowser())
        {
            await RegisterAsync(registration, username);
        }

        await using (var context = await ContextFactory.CreateDbContextAsync())
        {
            var user = await context.Set<UserEntity>().SingleAsync(u => u.Username == username);
            user.IsAdmin = true;
            await context.SaveChangesAsync();
        }

        var browser = NewBrowser();
        (await LoginAsync(browser, username)).EnsureSuccessStatusCode();
        return browser;
    }
}
