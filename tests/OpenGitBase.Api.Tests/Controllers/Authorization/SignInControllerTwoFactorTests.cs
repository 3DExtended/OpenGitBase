using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Tests.Base;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Api.Tests.Controllers.Authorization;

public class SignInControllerTwoFactorTests : TwoFactorControllerTestBase
{
    public SignInControllerTwoFactorTests(WebApplicationFactory<ApiEntryPoint> factory)
        : base(factory) { }

    [Fact]
    public async Task Login_WithoutTwoFactor_StillReturnsTheSessionToken()
    {
        using var browser = NewBrowser();
        await RegisterAsync(browser, "plainuser");

        var response = await LoginAsync(browser, "plainuser");

        response.EnsureSuccessStatusCode();
        Assert.StartsWith("text/plain", response.Content.Headers.ContentType?.ToString());
        Assert.True(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Login_WithTwoFactorOn_ReturnsAChallengeAndNoSession()
    {
        using var browser = NewBrowser();
        await RegisterAsync(browser, "challenged");
        await EnableTotpAsync("challenged");
        await browser.PostAsync("/signin/signout", null);

        var response = await LoginAsync(browser, "challenged");

        response.EnsureSuccessStatusCode();
        Assert.False(response.Headers.Contains("Set-Cookie"));
        var challenge = await response.Content.ReadFromJsonAsync<TwoFactorChallengeResponse>();
        Assert.NotNull(challenge);
        Assert.True(challenge.TwoFactorRequired);
        Assert.False(string.IsNullOrWhiteSpace(challenge.ChallengeToken));
        Assert.Equal(["totp"], challenge.Methods);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/signin/testlogin")).StatusCode);
    }

    [Fact]
    public async Task ChallengeToken_IsNoBearerToken()
    {
        using var browser = NewBrowser();
        await RegisterAsync(browser, "bearerprobe");
        await EnableTotpAsync("bearerprobe");

        var challenge = await LoginForChallengeAsync(NewBrowser(), "bearerprobe");

        var response = await SendWithBearerOnlyAsync(HttpMethod.Get, "/signin/testlogin", challenge.ChallengeToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TwoFactor_CorrectCode_SignsIn()
    {
        using var setup = NewBrowser();
        await RegisterAsync(setup, "secondstep");
        var secret = await EnableTotpAsync("secondstep");
        using var browser = NewBrowser();
        var challenge = await LoginForChallengeAsync(browser, "secondstep");

        var response = await CompleteTwoFactorAsync(browser, challenge.ChallengeToken, NextCode(secret));

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.Contains("Set-Cookie"));
        var token = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, (await SendWithBearerOnlyAsync(HttpMethod.Get, "/signin/testlogin", token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/signin/testlogin")).StatusCode);
    }

    [Fact]
    public async Task TwoFactor_SameCodeTwice_IsRejectedTheSecondTime()
    {
        using var setup = NewBrowser();
        await RegisterAsync(setup, "replayer");
        var secret = await EnableTotpAsync("replayer");
        var code = NextCode(secret);
        using var first = NewBrowser();
        var firstChallenge = await LoginForChallengeAsync(first, "replayer");
        (await CompleteTwoFactorAsync(first, firstChallenge.ChallengeToken, code)).EnsureSuccessStatusCode();

        using var second = NewBrowser();
        var secondChallenge = await LoginForChallengeAsync(second, "replayer");
        var response = await CompleteTwoFactorAsync(second, secondChallenge.ChallengeToken, code);

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, SecondFactorErrorResponse.InvalidCode);
    }

    [Fact]
    public async Task TwoFactor_RecoveryCode_SignsInOnlyOnce()
    {
        using var setup = NewBrowser();
        await RegisterAsync(setup, "recovering");
        var (_, codes) = await EnableTotpWithRecoveryCodesAsync("recovering");
        using var first = NewBrowser();
        var firstChallenge = await LoginForChallengeAsync(first, "recovering");

        var signedIn = await CompleteTwoFactorWithRecoveryCodeAsync(
            first,
            firstChallenge.ChallengeToken,
            codes[0].ToUpperInvariant().Replace("-", " ")
        );

        signedIn.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await first.GetAsync("/signin/testlogin")).StatusCode);
        using var second = NewBrowser();
        var secondChallenge = await LoginForChallengeAsync(second, "recovering");
        var reused = await CompleteTwoFactorWithRecoveryCodeAsync(second, secondChallenge.ChallengeToken, codes[0]);
        await AssertErrorAsync(reused, HttpStatusCode.Unauthorized, SecondFactorErrorResponse.InvalidCode);
    }

    [Theory]
    [InlineData("123456", "aaaaa-aaaaa")]
    [InlineData("", "")]
    [InlineData(" ", null)]
    public async Task TwoFactor_NotExactlyOneOfCodeAndRecoveryCode_IsBadRequest(string code, string? recoveryCode)
    {
        using var browser = NewBrowser();

        var response = await browser.PostAsJsonAsync(
            "/signin/two-factor",
            new TwoFactorSignInDto
            {
                ChallengeToken = "challenge",
                Code = code,
                RecoveryCode = recoveryCode,
            }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TwoFactor_ChallengeOlderThanFiveMinutes_IsRejected()
    {
        using var setup = NewBrowser();
        await RegisterAsync(setup, "slowtyper");
        var secret = await EnableTotpAsync("slowtyper");
        using var browser = NewBrowser();
        var challenge = await LoginForChallengeAsync(browser, "slowtyper");
        Clock.Advance(TimeSpan.FromMinutes(5));

        var response = await CompleteTwoFactorAsync(browser, challenge.ChallengeToken, NextCode(secret));

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, SecondFactorErrorResponse.ChallengeExpired);
    }

    [Fact]
    public async Task TwoFactor_AdminClaimComesOnlyWithTheSecondStep()
    {
        using var setup = NewBrowser();
        await RegisterAsync(setup, "twofactoradmin");
        var secret = await EnableTotpAsync("twofactoradmin");
        await using (var context = await ContextFactory.CreateDbContextAsync())
        {
            var user = await context.Set<UserEntity>().SingleAsync(u => u.Username == "twofactoradmin");
            user.IsAdmin = true;
            await context.SaveChangesAsync();
        }

        var token = await SignInWithCodeAsync(NewBrowser(), "twofactoradmin", secret);

        var claims = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims;
        Assert.Contains(claims, c => c.Type == "role" && c.Value == "admin");
    }

    [Fact]
    public async Task TwoFactor_FiveWrongCodes_EmailTheLockoutOnce()
    {
        using var setup = NewBrowser();
        await RegisterAsync(setup, "warned");
        var secret = await EnableTotpAsync("warned");
        using var browser = NewBrowser();
        var wrong = WrongCode(NextCode(secret));

        for (var attempt = 0; attempt < 6; attempt++)
        {
            var challenge = await LoginForChallengeAsync(browser, "warned");
            await CompleteTwoFactorAsync(browser, challenge.ChallengeToken, wrong);
        }

        var lockout = Assert.Single(
            Emails.GetByRecipient("warned@example.com"),
            message => message.Subject == "Sign-in blocked after failed second steps"
        );
        Assert.Contains("password was entered correctly", lockout.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TwoFactor_FiveWrongCodes_LockTheSecondStepFor15Minutes()
    {
        using var setup = NewBrowser();
        await RegisterAsync(setup, "guesser");
        var secret = await EnableTotpAsync("guesser");
        using var browser = NewBrowser();
        var challenge = await LoginForChallengeAsync(browser, "guesser");
        var wrong = WrongCode(NextCode(secret));

        for (var attempt = 0; attempt < 4; attempt++)
        {
            var response = await CompleteTwoFactorAsync(browser, challenge.ChallengeToken, wrong);
            await AssertErrorAsync(response, HttpStatusCode.Unauthorized, SecondFactorErrorResponse.InvalidCode);
        }

        var fifth = await CompleteTwoFactorAsync(browser, challenge.ChallengeToken, wrong);
        var lockout = await AssertErrorAsync(fifth, HttpStatusCode.TooManyRequests, SecondFactorErrorResponse.LockedOut);
        Assert.NotNull(lockout.LockedUntil);

        // The lock is on the account, not the challenge: a fresh sign-in is locked as well.
        var retry = await LoginForChallengeAsync(browser, "guesser");
        var correct = await CompleteTwoFactorAsync(browser, retry.ChallengeToken, NextCode(secret));
        await AssertErrorAsync(correct, HttpStatusCode.TooManyRequests, SecondFactorErrorResponse.LockedOut);
    }

    [Fact]
    public async Task Google_WithTwoFactorOn_ReturnsAChallenge()
    {
        const string subject = "twofactor-google-subject";
        const string registrationToken = "twofactor-google-token";
        SeedProviderRegistration(registrationToken, "google" + subject, "tfgoogle@example.com");
        using var browser = NewBrowser();
        (await browser.PostAsJsonAsync(
            "/register/openprovider",
            new OpenProviderRegisterDto { Username = "tfgoogle", RegistrationToken = registrationToken }
        )).EnsureSuccessStatusCode();
        var secret = await EnableTotpAsync("tfgoogle");
        using var signIn = NewBrowser();

        var response = await signIn.PostAsJsonAsync(
            "/signin/google",
            new GoogleLoginDto { IdentityToken = AuthTestJwtHelper.CreateGoogleIdentityToken(subject, "tfgoogle@example.com") }
        );

        response.EnsureSuccessStatusCode();
        Assert.False(response.Headers.Contains("Set-Cookie"));
        var challenge = await response.Content.ReadFromJsonAsync<TwoFactorChallengeResponse>();
        Assert.NotNull(challenge);
        (await CompleteTwoFactorAsync(signIn, challenge.ChallengeToken, NextCode(secret))).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task PasswordReset_LeavesTwoFactorOn()
    {
        using var browser = NewBrowser();
        await RegisterAsync(browser, "forgetful");
        await EnableTotpAsync("forgetful");
        const string resetCode = "123-456-789";
        await using (var context = await ContextFactory.CreateDbContextAsync())
        {
            var credentials = await context.Set<UserCredentialsEntity>().SingleAsync(c => c.Username == "forgetful");
            credentials.PasswordResetTokenHash = PasswordHasher.HashPassword(resetCode);
            credentials.PasswordResetTokenExpireDate = Clock.UtcNow.AddHours(2);
            await context.SaveChangesAsync();
        }

        (await browser.PostAsJsonAsync(
            "/signin/resetpassword",
            new ResetPasswordDto
            {
                Username = "forgetful",
                Email = "forgetful@example.com",
                ResetCode = resetCode,
                NewPassword = "NewPassword456!",
            }
        )).EnsureSuccessStatusCode();

        var response = await LoginAsync(NewBrowser(), "forgetful", "NewPassword456!");
        var challenge = await response.Content.ReadFromJsonAsync<TwoFactorChallengeResponse>();
        Assert.True(challenge?.TwoFactorRequired);
    }

    private static async Task<SecondFactorErrorResponse> AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string error
    )
    {
        Assert.Equal(status, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SecondFactorErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal(error, body.Error);
        return body;
    }
}
