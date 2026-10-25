using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Tests.Base;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Api.Tests.Controllers.Authorization;

public class SignInControllerPasskeyTests : TwoFactorControllerTestBase
{
    public SignInControllerPasskeyTests(WebApplicationFactory<ApiEntryPoint> factory)
        : base(factory) { }

    [Fact]
    public async Task PasskeySignIn_IssuesTheSessionTokenAndCookie()
    {
        using var passkey = NewPasskey();
        await RegisterWithPasskeyAsync("passkeyuser", passkey);
        using var browser = NewBrowser();

        var response = await SignInWithPasskeyAsync(browser, passkey);

        response.EnsureSuccessStatusCode();
        Assert.StartsWith("text/plain", response.Content.Headers.ContentType?.ToString());
        Assert.True(response.Headers.Contains("Set-Cookie"));
        var token = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, (await SendWithBearerOnlyAsync(HttpMethod.Get, "/signin/testlogin", token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/signin/testlogin")).StatusCode);
    }

    [Fact]
    public async Task PasskeySignIn_TwoFactorOn_NeedsNoSecondStep()
    {
        using var passkey = NewPasskey();
        await RegisterWithPasskeyAsync("passkeyandtotp", passkey);
        await EnableTotpAsync("passkeyandtotp");
        using var browser = NewBrowser();

        var response = await SignInWithPasskeyAsync(browser, passkey);

        response.EnsureSuccessStatusCode();
        Assert.StartsWith("text/plain", response.Content.Headers.ContentType?.ToString());
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/signin/testlogin")).StatusCode);
    }

    [Fact]
    public async Task PasskeySignIn_Admin_GetsTheAdminClaim()
    {
        using var passkey = NewPasskey();
        await RegisterWithPasskeyAsync("passkeyadmin", passkey);
        await using (var context = await ContextFactory.CreateDbContextAsync())
        {
            var user = await context.Set<UserEntity>().SingleAsync(u => u.Username == "passkeyadmin");
            user.IsAdmin = true;
            await context.SaveChangesAsync();
        }

        var response = await SignInWithPasskeyAsync(NewBrowser(), passkey);

        response.EnsureSuccessStatusCode();
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(await response.Content.ReadAsStringAsync()).Claims;
        Assert.Contains(claims, c => c.Type == "role" && c.Value == "admin");
    }

    [Fact]
    public async Task PasskeyOptions_ListNoCredentialAndRequireUserVerification()
    {
        using var passkey = NewPasskey();
        await RegisterWithPasskeyAsync("hiddenowner", passkey);

        var response = await NewBrowser().PostAsync("/signin/passkey/options", null);

        response.EnsureSuccessStatusCode();
        Assert.StartsWith("application/json", response.Content.Headers.ContentType?.ToString());
        using var options = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("required", options.RootElement.GetProperty("userVerification").GetString());
        Assert.False(
            options.RootElement.TryGetProperty("allowCredentials", out var allowed) && allowed.GetArrayLength() > 0
        );
        Assert.DoesNotContain("hiddenowner", options.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PasskeySignIn_UnknownCredential_IsRejected()
    {
        using var setup = NewBrowser();
        await RegisterAsync(setup, "someoneelse");
        using var stranger = NewPasskey();

        var response = await SignInWithPasskeyAsync(NewBrowser(), stranger);

        await AssertRejectedAsync(response);
    }

    [Fact]
    public async Task PasskeySignIn_ReplayedAssertion_IsRejected()
    {
        using var passkey = NewPasskey();
        await RegisterWithPasskeyAsync("replayed", passkey);
        using var browser = NewBrowser();
        var assertion = passkey.GetAssertion(await OptionsAsync(browser));

        var first = await PostAssertionAsync(browser, assertion);
        var replayed = await PostAssertionAsync(NewBrowser(), assertion);

        first.EnsureSuccessStatusCode();
        await AssertRejectedAsync(replayed);
    }

    [Fact]
    public async Task PasskeySignIn_ExpiredChallenge_IsRejected()
    {
        using var passkey = NewPasskey();
        await RegisterWithPasskeyAsync("slowpasskey", passkey);
        using var browser = NewBrowser();
        var options = await OptionsAsync(browser);
        Clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        var response = await PostAssertionAsync(browser, passkey.GetAssertion(options));

        await AssertRejectedAsync(response);
    }

    [Fact]
    public async Task PasskeySignIn_DeletedUser_IsRejected()
    {
        using var passkey = NewPasskey();
        await RegisterWithPasskeyAsync("goneuser", passkey);
        await using (var context = await ContextFactory.CreateDbContextAsync())
        {
            var credentials = await context.Set<UserCredentialsEntity>().SingleAsync(c => c.Username == "goneuser");
            credentials.Deleted = true;
            await context.SaveChangesAsync();
        }

        var response = await SignInWithPasskeyAsync(NewBrowser(), passkey);

        await AssertRejectedAsync(response);
    }

    [Fact]
    public async Task PasskeySignIn_Failures_LeaveTheSecondFactorCounterAlone()
    {
        using var passkey = NewPasskey();
        await RegisterWithPasskeyAsync("counted", passkey);
        await EnableTotpAsync("counted");
        passkey.UserVerified = false;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await AssertRejectedAsync(await SignInWithPasskeyAsync(NewBrowser(), passkey));
        }

        var credentials = await GetCredentialsAsync("counted");
        Assert.Equal(0, credentials.SecondFactorFailedAttempts);
        Assert.Null(credentials.SecondFactorLockedUntil);
    }

    [Fact]
    public async Task PasskeySignIn_NoAssertion_IsBadRequest()
    {
        var response = await NewBrowser().PostAsJsonAsync("/signin/passkey", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<string> OptionsAsync(HttpClient browser)
    {
        var response = await browser.PostAsync("/signin/passkey/options", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private static Task<HttpResponseMessage> PostAssertionAsync(HttpClient browser, string assertionJson) =>
        browser.PostAsJsonAsync(
            "/signin/passkey",
            new PasskeySignInDto { Assertion = JsonDocument.Parse(assertionJson).RootElement }
        );

    private static async Task<HttpResponseMessage> SignInWithPasskeyAsync(
        HttpClient browser,
        SoftwarePasskeyAuthenticator passkey
    ) => await PostAssertionAsync(browser, passkey.GetAssertion(await OptionsAsync(browser)));

    private static async Task AssertRejectedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        var body = await response.Content.ReadFromJsonAsync<SecondFactorErrorResponse>();
        Assert.Equal(SecondFactorErrorResponse.PasskeyRejected, body?.Error);
    }

    /// <summary>Registers <paramref name="username"/> and adds <paramref name="passkey"/> to the account.</summary>
    private async Task RegisterWithPasskeyAsync(string username, SoftwarePasskeyAuthenticator passkey)
    {
        using var browser = NewBrowser();
        await RegisterAsync(browser, username);
        (await ReauthenticateAsync(browser)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Created, (await RegisterPasskeyAsync(browser, passkey)).StatusCode);
    }
}
