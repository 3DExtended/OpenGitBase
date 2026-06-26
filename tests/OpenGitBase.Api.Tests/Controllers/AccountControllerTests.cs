using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Tests.Base;
using OpenGitBase.Common.Options;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Api.Tests.Controllers;

public class AccountControllerTests : ControllerTestBase
{
    public AccountControllerTests(WebApplicationFactory<ApiEntryPoint> factory)
        : base(factory) { }

    [Fact]
    public async Task Me_WhenAuthenticated_ReturnsUsernameAndEmailVerified()
    {
        await RegisterUserAsync("account-me-user", "account-me@example.com", "Password123!");
        var token = await LoginAsync("account-me-user", "Password123!");
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await Client.GetAsync("/account/me");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AccountMeResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.UserId);
        Assert.Equal("account-me-user", body.Username);
        Assert.False(body.EmailVerified);
        Assert.False(body.IsAdmin);
    }

    [Fact]
    public async Task VerifyEmail_WhenValid_SetsEmailVerified()
    {
        await RegisterUserAsync("verify-user", "verify@example.com", "Password123!");

        const string verificationCode = "123-456-789";
        await using (var context = await ContextFactory.CreateDbContextAsync())
        {
            var credentials = await context
                .Set<UserCredentialsEntity>()
                .SingleAsync(x => x.Username == "verify-user");
            credentials.EmailVerificationTokenHash = PasswordHasher.HashPassword(
                verificationCode
            );
            credentials.EmailVerificationTokenExpireDate = DateTimeOffset.UtcNow.AddHours(24);
            await context.SaveChangesAsync();
        }

        var response = await Client.PostAsJsonAsync(
            "/account/verify-email",
            new VerifyEmailDto
            {
                Username = "verify-user",
                VerificationToken = verificationCode,
            }
        );

        response.EnsureSuccessStatusCode();
        Assert.Equal("Email verified", await response.Content.ReadAsStringAsync());

        await using (var context = await ContextFactory.CreateDbContextAsync())
        {
            var credentials = await context
                .Set<UserCredentialsEntity>()
                .SingleAsync(x => x.Username == "verify-user");
            Assert.True(credentials.EmailVerified);
        }
    }

    [Fact]
    public async Task VerifyEmail_WithInvalidToken_ReturnsBadRequest()
    {
        await RegisterUserAsync("bad-verify-user", "bad-verify@example.com", "Password123!");

        var response = await Client.PostAsJsonAsync(
            "/account/verify-email",
            new VerifyEmailDto
            {
                Username = "bad-verify-user",
                VerificationToken = "000-000-000",
            }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResendVerification_WhenAuthenticated_SendsEmail()
    {
        await RegisterUserAsync("resend-user", "resend@example.com", "Password123!");
        var token = await LoginAsync("resend-user", "Password123!");
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await Client.PostAsync("/account/resend-verification", null);

        response.EnsureSuccessStatusCode();
        Assert.Equal("Verification email sent", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ChangePassword_WhenValid_ReturnsOk()
    {
        await RegisterUserAsync("changepass-user", "changepass@example.com", "Password123!");
        var token = await LoginAsync("changepass-user", "Password123!");
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await Client.PostAsJsonAsync(
            "/account/change-password",
            new ChangePasswordDto
            {
                CurrentPassword = "Password123!",
                NewPassword = "NewPassword456!",
            }
        );

        response.EnsureSuccessStatusCode();
        Assert.Equal("Password changed", await response.Content.ReadAsStringAsync());

        Client.DefaultRequestHeaders.Authorization = null;
        var loginResponse = await Client.PostAsJsonAsync(
            "/signin/login",
            new LoginDto { Username = "changepass-user", Password = "NewPassword456!" }
        );
        loginResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_ReturnsBadRequest()
    {
        await RegisterUserAsync("wrongpass-user", "wrongpass@example.com", "Password123!");
        var token = await LoginAsync("wrongpass-user", "Password123!");
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await Client.PostAsJsonAsync(
            "/account/change-password",
            new ChangePasswordDto
            {
                CurrentPassword = "WrongPassword!",
                NewPassword = "NewPassword456!",
            }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_WithWrongPassword_ReturnsBadRequest()
    {
        await RegisterUserAsync("delete-wrongpass", "delete-wrongpass@example.com", "Password123!");
        var token = await LoginAsync("delete-wrongpass", "Password123!");
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await Client.PostAsJsonAsync(
            "/account/delete",
            new DeleteAccountDto { Password = "WrongPassword!" }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DebugVerifyEmail_WhenFlagDisabled_ReturnsNotFound()
    {
        await RegisterUserAsync("debug-off-user", "debug-off@example.com", "Password123!");
        using var client = CreateClientWithDebugEmailVerification(enabled: false);
        await LoginOnClientAsync(client, "debug-off-user", "Password123!");

        var response = await client.PostAsync("/account/debug/verify-email", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DebugVerifyEmail_WhenFlagEnabled_VerifiesCurrentUser()
    {
        await RegisterUserAsync("debug-on-user", "debug-on@example.com", "Password123!");
        using var client = CreateClientWithDebugEmailVerification(enabled: true);
        await LoginOnClientAsync(client, "debug-on-user", "Password123!");

        var response = await client.PostAsync("/account/debug/verify-email", null);

        response.EnsureSuccessStatusCode();
        Assert.Equal("Email verified", await response.Content.ReadAsStringAsync());

        var me = await client.GetFromJsonAsync<AccountMeResponse>("/account/me");
        Assert.NotNull(me);
        Assert.True(me.EmailVerified);
        Assert.NotNull(me.Debug);
        Assert.True(me.Debug!.EmailVerification);
    }

    [Fact]
    public async Task Me_WhenDebugFlagEnabled_IncludesDebugFeatures()
    {
        await RegisterUserAsync("debug-me-user", "debug-me@example.com", "Password123!");
        using var client = CreateClientWithDebugEmailVerification(enabled: true);
        await LoginOnClientAsync(client, "debug-me-user", "Password123!");

        var me = await client.GetFromJsonAsync<AccountMeResponse>("/account/me");

        Assert.NotNull(me);
        Assert.NotNull(me.Debug);
        Assert.True(me.Debug.EmailVerification);
    }

    [Fact]
    public async Task DebugVerificationCode_WhenFlagEnabled_ReturnsPlainCode()
    {
        await RegisterUserAsync("debug-code-user", "debug-code@example.com", "Password123!");
        using var client = CreateClientWithDebugEmailVerification(enabled: true);
        await LoginOnClientAsync(client, "debug-code-user", "Password123!");

        var response = await client.PostAsync("/account/debug/verification-code", null);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<DebugVerificationCodeResponse>();
        Assert.NotNull(body);
        Assert.Matches(@"^\d{3}-\d{3}-\d{3}$", body.Code);
        Assert.True(body.ExpiresAt > DateTimeOffset.UtcNow);
    }

    private static async Task LoginOnClientAsync(
        HttpClient client,
        string username,
        string password
    )
    {
        var response = await client.PostAsJsonAsync(
            "/signin/login",
            new LoginDto { Username = username, Password = password }
        );
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadAsStringAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token
        );
    }

    private async Task<string> LoginAsync(string username, string password)
    {
        var response = await Client.PostAsJsonAsync(
            "/signin/login",
            new LoginDto { Username = username, Password = password }
        );
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
