using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Tests.Base;
using OpenGitBase.Common.Tests.Testing;

namespace OpenGitBase.Api.Tests.Controllers.Authorization;

/// <summary>A passkey answering the second sign-in step after the password.</summary>
public class SignInControllerTwoFactorPasskeyTests : TwoFactorControllerTestBase
{
    public SignInControllerTwoFactorPasskeyTests(WebApplicationFactory<ApiEntryPoint> factory)
        : base(factory) { }

    [Fact]
    public async Task Login_UserWithAPasskey_OffersPasskeyAsSecondFactor()
    {
        using var passkey = NewPasskey();
        await RegisterWithTotpAndPasskeyAsync("offerspasskey", passkey);

        var challenge = await LoginForChallengeAsync(NewBrowser(), "offerspasskey");

        Assert.Equal(["totp", "passkey"], challenge.Methods);
    }

    [Fact]
    public async Task PasskeyOptions_ListTheUsersPasskeysAndOnlyPreferUserVerification()
    {
        using var passkey = NewPasskey();
        await RegisterWithTotpAndPasskeyAsync("secondoptions", passkey);
        using var browser = NewBrowser();
        var challenge = await LoginForChallengeAsync(browser, "secondoptions");

        var response = await PostOptionsAsync(browser, challenge.ChallengeToken);

        response.EnsureSuccessStatusCode();
        Assert.StartsWith("application/json", response.Content.Headers.ContentType?.ToString());
        using var options = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("preferred", options.RootElement.GetProperty("userVerification").GetString());
        var allowed = Assert.Single(options.RootElement.GetProperty("allowCredentials").EnumerateArray());
        Assert.Equal(SoftwarePasskeyAuthenticator.ToBase64Url(passkey.CredentialId), allowed.GetProperty("id").GetString());
    }

    [Fact]
    public async Task SecondStep_SecurityKeyTouchWithoutUserVerification_SignsIn()
    {
        using var securityKey = NewPasskey();
        await RegisterWithTotpAndPasskeyAsync("securitykey", securityKey);
        securityKey.UserVerified = false;
        using var browser = NewBrowser();
        var challenge = await LoginForChallengeAsync(browser, "securitykey");

        var response = await CompleteTwoFactorWithPasskeyAsync(browser, challenge.ChallengeToken, securityKey);

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.Contains("Set-Cookie"));
        var token = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, (await SendWithBearerOnlyAsync(HttpMethod.Get, "/signin/testlogin", token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/signin/testlogin")).StatusCode);
    }

    [Fact]
    public async Task SecondStep_AnotherUsersPasskey_IsRejectedAndCounted()
    {
        using var alicesPasskey = NewPasskey();
        using var mallorysPasskey = NewPasskey();
        await RegisterWithTotpAndPasskeyAsync("alicesecond", alicesPasskey);
        await RegisterWithTotpAndPasskeyAsync("mallorysecond", mallorysPasskey);
        using var browser = NewBrowser();
        var challenge = await LoginForChallengeAsync(browser, "alicesecond");

        var response = await CompleteTwoFactorWithPasskeyAsync(browser, challenge.ChallengeToken, mallorysPasskey);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        var body = await response.Content.ReadFromJsonAsync<SecondFactorErrorResponse>();
        Assert.Equal(SecondFactorErrorResponse.PasskeyRejected, body?.Error);
        Assert.Equal(1, (await GetCredentialsAsync("alicesecond")).SecondFactorFailedAttempts);
    }

    [Fact]
    public async Task SecondStep_FiveRejectedPasskeys_LockIt()
    {
        using var passkey = NewPasskey();
        await RegisterWithTotpAndPasskeyAsync("lockspasskey", passkey);
        using var browser = NewBrowser();
        var challenge = await LoginForChallengeAsync(browser, "lockspasskey");
        passkey.Origin = "https://opengitbase.evil.example";

        var responses = new List<HttpResponseMessage>();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            responses.Add(await CompleteTwoFactorWithPasskeyAsync(browser, challenge.ChallengeToken, passkey));
        }

        Assert.All(responses.Take(4), response => Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode));
        Assert.Equal(HttpStatusCode.TooManyRequests, responses[4].StatusCode);
        var body = await responses[4].Content.ReadFromJsonAsync<SecondFactorErrorResponse>();
        Assert.Equal(SecondFactorErrorResponse.LockedOut, body?.Error);
    }

    [Fact]
    public async Task PasskeyOptions_UnknownChallenge_HasExpired()
    {
        var response = await PostOptionsAsync(NewBrowser(), "not-a-challenge");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SecondFactorErrorResponse>();
        Assert.Equal(SecondFactorErrorResponse.ChallengeExpired, body?.Error);
    }

    [Fact]
    public async Task PasskeyOptions_UserWithoutPasskeys_IsBadRequest()
    {
        using (var setup = NewBrowser())
        {
            await RegisterAsync(setup, "nosecondkey");
        }

        await EnableTotpAsync("nosecondkey");
        using var browser = NewBrowser();
        var challenge = await LoginForChallengeAsync(browser, "nosecondkey");

        var response = await PostOptionsAsync(browser, challenge.ChallengeToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SecondFactorErrorResponse>();
        Assert.Equal(SecondFactorErrorResponse.NoPasskeys, body?.Error);
    }

    [Fact]
    public async Task SecondStep_CodeAndPasskeyTogether_IsBadRequest()
    {
        var response = await NewBrowser().PostAsJsonAsync(
            "/signin/two-factor",
            new { challengeToken = "token", code = "123456", passkeyAssertion = new { } }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SecondStep_PasskeyAssertionThatIsNoObject_IsBadRequest()
    {
        var response = await NewBrowser().PostAsJsonAsync(
            "/signin/two-factor",
            new { challengeToken = "token", passkeyAssertion = "not-json" }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static Task<HttpResponseMessage> PostOptionsAsync(HttpClient browser, string challengeToken) =>
        browser.PostAsJsonAsync(
            "/signin/two-factor/passkey-options",
            new TwoFactorPasskeyOptionsDto { ChallengeToken = challengeToken }
        );

    /// <summary>Registers <paramref name="username"/>, adds <paramref name="passkey"/> and turns on 2FA with an authenticator.</summary>
    private async Task RegisterWithTotpAndPasskeyAsync(string username, SoftwarePasskeyAuthenticator passkey)
    {
        using (var browser = NewBrowser())
        {
            await RegisterAsync(browser, username);
            (await ReauthenticateAsync(browser)).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Created, (await RegisterPasskeyAsync(browser, passkey)).StatusCode);
        }

        await EnableTotpAsync(username);
    }
}
