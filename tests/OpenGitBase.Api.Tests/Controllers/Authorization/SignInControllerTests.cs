using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Tests.Base;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Api.Tests.Controllers.Authorization;

public class SignInControllerTests : ControllerTestBase
{
    public SignInControllerTests(WebApplicationFactory<ApiEntryPoint> factory)
        : base(factory) { }

    [Fact]
    public async Task Login_Success_ReturnsJwt()
    {
        await RegisterUserAsync("loginuser", "login@example.com", "Password123!");

        var response = await Client.PostAsJsonAsync(
            "/signin/login",
            new LoginDto { Username = "loginuser", Password = "Password123!" }
        );

        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadAsStringAsync();
        await VerifyJwtAsync(token);
        Assert.Contains(
            AuthCookieOptions.CookieName,
            response.Headers.GetValues("Set-Cookie").First()
        );
    }

    [Fact]
    public async Task Login_SetsCookieThatAuthenticatesProtectedEndpoint()
    {
        await RegisterUserAsync("cookieuser", "cookie@example.com", "Password123!");
        Client.DefaultRequestHeaders.Authorization = null;

        var response = await Client.PostAsJsonAsync(
            "/signin/login",
            new LoginDto { Username = "cookieuser", Password = "Password123!" }
        );

        response.EnsureSuccessStatusCode();

        var testLoginResponse = await Client.GetAsync("/signin/testlogin");
        testLoginResponse.EnsureSuccessStatusCode();
        Assert.Equal("ok", await testLoginResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SignOut_ClearsCookie()
    {
        await RegisterUserAsync("signoutuser", "signout@example.com", "Password123!");
        var loginResponse = await Client.PostAsJsonAsync(
            "/signin/login",
            new LoginDto { Username = "signoutuser", Password = "Password123!" }
        );
        loginResponse.EnsureSuccessStatusCode();

        var signOutResponse = await Client.PostAsync("/signin/signout", null);
        signOutResponse.EnsureSuccessStatusCode();
        Assert.Equal("Signed out", await signOutResponse.Content.ReadAsStringAsync());

        Client.DefaultRequestHeaders.Authorization = null;
        var testLoginResponse = await Client.GetAsync("/signin/testlogin");
        Assert.Equal(HttpStatusCode.Unauthorized, testLoginResponse.StatusCode);
    }

    [Fact]
    public async Task Login_WithNullBody_ReturnsBadRequest()
    {
        var response = await Client.PostAsJsonAsync<LoginDto?>("/signin/login", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithEmptyUsername_ReturnsBadRequest()
    {
        var response = await Client.PostAsJsonAsync(
            "/signin/login",
            new LoginDto { Username = string.Empty, Password = "Password123!" }
        );
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        await RegisterUserAsync("wrongpassuser", "wrongpass@example.com", "Password123!");

        var response = await Client.PostAsJsonAsync(
            "/signin/login",
            new LoginDto { Username = "wrongpassuser", Password = "WrongPassword!" }
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Google_WithEmptyToken_ReturnsBadRequest()
    {
        var response = await Client.PostAsJsonAsync(
            "/signin/google",
            new GoogleLoginDto { IdentityToken = string.Empty }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Expected IdentityToken.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Google_WithInvalidToken_ReturnsUnauthorized()
    {
        var response = await Client.PostAsJsonAsync(
            "/signin/google",
            new GoogleLoginDto { IdentityToken = "invalid-google-token" }
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Google_NewUser_ReturnsRedirectToken()
    {
        var identityToken = AuthTestJwtHelper.CreateGoogleIdentityToken(
            "new-google-subject",
            "newgoogle@example.com"
        );

        var response = await Client.PostAsJsonAsync(
            "/signin/google",
            new GoogleLoginDto { IdentityToken = identityToken }
        );

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("redirect", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Google_ExistingUser_ReturnsJwt()
    {
        const string subject = "existing-google-subject";
        const string internalId = "google" + subject;
        const string registrationToken = "google-existing-token";
        SeedRegistrationCache(registrationToken, internalId, "existinggoogle@example.com");

        var registerResponse = await Client.PostAsJsonAsync(
            "/register/openprovider",
            new OpenProviderRegisterDto
            {
                Username = "googleuser",
                RegistrationToken = registrationToken,
            }
        );
        registerResponse.EnsureSuccessStatusCode();

        var identityToken = AuthTestJwtHelper.CreateGoogleIdentityToken(
            subject,
            "existinggoogle@example.com"
        );
        var response = await Client.PostAsJsonAsync(
            "/signin/google",
            new GoogleLoginDto { IdentityToken = identityToken }
        );

        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadAsStringAsync();
        await VerifyJwtAsync(token);
    }

    [Fact]
    public async Task Google_ExistingUserWithMissingUser_ReturnsBadRequest()
    {
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<UserGetByInternalIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(userId);
        queryProcessor
            .RunQueryAsync(Arg.Any<UserGetByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<User>.None);

        var (client, _) = CreateClientWithQueryProcessor(queryProcessor);
        var identityToken = AuthTestJwtHelper.CreateGoogleIdentityToken(
            "orphan-subject",
            "orphan@example.com"
        );
        var response = await client.PostAsJsonAsync(
            "/signin/google",
            new GoogleLoginDto { IdentityToken = identityToken }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Apple_WithEmptyToken_ReturnsBadRequest()
    {
        var response = await Client.PostAsJsonAsync(
            "/signin/apple",
            new AppleLoginDto { IdentityToken = string.Empty }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Expected IdentityToken.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Apple_WithWrongIssuer_ReturnsUnauthorized()
    {
        var identityToken = AuthTestJwtHelper.CreateAppleIdentityToken(
            "apple-subject",
            "apple@example.com",
            issuer: "https://evil.example.com"
        );

        var response = await Client.PostAsJsonAsync(
            "/signin/apple",
            new AppleLoginDto { IdentityToken = identityToken }
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Apple_WithWrongAudience_ReturnsUnauthorized()
    {
        var identityToken = AuthTestJwtHelper.CreateAppleIdentityToken(
            "apple-subject",
            "apple@example.com",
            audience: "wrong-client-id"
        );

        var response = await Client.PostAsJsonAsync(
            "/signin/apple",
            new AppleLoginDto { IdentityToken = identityToken }
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Apple_WithExpiredToken_ReturnsUnauthorized()
    {
        var identityToken = AuthTestJwtHelper.CreateAppleIdentityToken(
            "apple-subject",
            "apple@example.com",
            expiresUtc: DateTime.UtcNow.AddHours(-1)
        );

        var response = await Client.PostAsJsonAsync(
            "/signin/apple",
            new AppleLoginDto { IdentityToken = identityToken }
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Apple_NewUser_ReturnsRedirectToken()
    {
        var identityToken = AuthTestJwtHelper.CreateAppleIdentityToken(
            "new-apple-subject",
            "newapple@example.com"
        );

        var response = await Client.PostAsJsonAsync(
            "/signin/apple",
            new AppleLoginDto { IdentityToken = identityToken }
        );

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("redirect", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apple_ExistingUser_ReturnsJwt()
    {
        const string subject = "existing-apple-subject";
        const string registrationToken = "apple-existing-token";
        SeedRegistrationCache(registrationToken, subject, "existingapple@example.com");

        var registerResponse = await Client.PostAsJsonAsync(
            "/register/openprovider",
            new OpenProviderRegisterDto
            {
                Username = "appleuser",
                RegistrationToken = registrationToken,
            }
        );
        registerResponse.EnsureSuccessStatusCode();

        var identityToken = AuthTestJwtHelper.CreateAppleIdentityToken(
            subject,
            "existingapple@example.com"
        );
        var response = await Client.PostAsJsonAsync(
            "/signin/apple",
            new AppleLoginDto { IdentityToken = identityToken }
        );

        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadAsStringAsync();
        await VerifyJwtAsync(token);
    }

    [Fact]
    public async Task RequestPasswordReset_Success_ReturnsOk()
    {
        await RegisterUserAsync("resetuser", "reset@example.com", "Password123!");

        var response = await Client.PostAsJsonAsync(
            "/signin/requestresetpassword",
            new ResetPasswordRequestDto { Username = "resetuser", Email = "reset@example.com" }
        );

        response.EnsureSuccessStatusCode();
        Assert.Equal("Email sent", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RequestPasswordReset_WithNullBody_ReturnsBadRequest()
    {
        var response = await Client.PostAsJsonAsync<ResetPasswordRequestDto?>(
            "/signin/requestresetpassword",
            null
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RequestPasswordReset_WithMissingFields_ReturnsBadRequest()
    {
        var response = await Client.PostAsJsonAsync(
            "/signin/requestresetpassword",
            new ResetPasswordRequestDto { Username = string.Empty, Email = "reset@example.com" }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RequestPasswordReset_WithUnknownUser_ReturnsBadRequest()
    {
        var response = await Client.PostAsJsonAsync(
            "/signin/requestresetpassword",
            new ResetPasswordRequestDto { Username = "missinguser", Email = "missing@example.com" }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "Could not create password reset mail or token!",
            await response.Content.ReadAsStringAsync()
        );
    }

    [Fact]
    public async Task ResetPassword_Success_ReturnsOk()
    {
        await RegisterUserAsync("newpassuser", "newpass@example.com", "Password123!");

        const string resetCode = "123-456-789";
        await using (var context = await ContextFactory.CreateDbContextAsync())
        {
            var credentials = await context
                .Set<UserCredentialsEntity>()
                .SingleAsync(x => x.Username == "newpassuser");
            credentials.PasswordResetTokenHash = PasswordHasher.HashPassword(resetCode);
            credentials.PasswordResetTokenExpireDate = DateTimeOffset.UtcNow.AddHours(2);
            await context.SaveChangesAsync();
        }

        var response = await Client.PostAsJsonAsync(
            "/signin/resetpassword",
            new ResetPasswordDto
            {
                Username = "newpassuser",
                Email = "newpass@example.com",
                ResetCode = resetCode,
                NewPassword = "NewPassword456!",
            }
        );

        response.EnsureSuccessStatusCode();
        Assert.Equal("New password set", await response.Content.ReadAsStringAsync());

        var loginResponse = await Client.PostAsJsonAsync(
            "/signin/login",
            new LoginDto { Username = "newpassuser", Password = "NewPassword456!" }
        );
        loginResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ResetPassword_WithNullBody_ReturnsBadRequest()
    {
        var response = await Client.PostAsJsonAsync<ResetPasswordDto?>(
            "/signin/resetpassword",
            null
        );
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("", "reset@example.com", "123-456-789", "NewPassword456!")]
    [InlineData("user", "", "123-456-789", "NewPassword456!")]
    [InlineData("user", "reset@example.com", "", "NewPassword456!")]
    [InlineData("user", "reset@example.com", "123-456-789", "")]
    public async Task ResetPassword_WithMissingFields_ReturnsBadRequest(
        string username,
        string email,
        string resetCode,
        string newPassword
    )
    {
        var response = await Client.PostAsJsonAsync(
            "/signin/resetpassword",
            new ResetPasswordDto
            {
                Username = username,
                Email = email,
                ResetCode = resetCode,
                NewPassword = newPassword,
            }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WithInvalidCode_ReturnsBadRequest()
    {
        await RegisterUserAsync("badcodeuser", "badcode@example.com", "Password123!");

        var response = await Client.PostAsJsonAsync(
            "/signin/resetpassword",
            new ResetPasswordDto
            {
                Username = "badcodeuser",
                Email = "badcode@example.com",
                ResetCode = "000-000-000",
                NewPassword = "NewPassword456!",
            }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Could not set new password!", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_Success_SetsAuthCookie()
    {
        await RegisterUserAsync("cookie-login", "cookie-login@example.com", "Password123!");

        var response = await Client.PostAsJsonAsync(
            "/signin/login",
            new LoginDto { Username = "cookie-login", Password = "Password123!" }
        );

        response.EnsureSuccessStatusCode();
        Assert.Contains(
            AuthCookieOptions.CookieName,
            response.Headers.GetValues("Set-Cookie").First()
        );
    }

    [Fact]
    public async Task SignOut_ClearsAuthCookie()
    {
        await RegisterUserAsync("signout-user", "signout@example.com", "Password123!");
        var loginResponse = await Client.PostAsJsonAsync(
            "/signin/login",
            new LoginDto { Username = "signout-user", Password = "Password123!" }
        );
        loginResponse.EnsureSuccessStatusCode();

        var response = await Client.PostAsync("/signin/signout", null);
        response.EnsureSuccessStatusCode();
        Assert.Contains(
            AuthCookieOptions.CookieName,
            response.Headers.GetValues("Set-Cookie").First()
        );
    }

    [Fact]
    public async Task TestLogin_WithValidJwt_ReturnsOk()
    {
        var token = await RegisterUserAsync("jwtuser", "jwt@example.com", "Password123!");
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await Client.GetAsync("/signin/testlogin");

        response.EnsureSuccessStatusCode();
        Assert.Equal("ok", await response.Content.ReadAsStringAsync());
    }
}
