using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenGitBase.Api.Models;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Features.AccountSecurity.Entities;
using OpenGitBase.Features.AccountSecurity.Totp;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Api.Tests.Base;

/// <summary>
/// An API host whose clock the test moves forward, so every authenticator code a test computes
/// belongs to a fresh 30-second step and replay protection never trips by accident.
/// </summary>
public abstract class TwoFactorControllerTestBase : ControllerTestBase
{
    public const string Password = "Password123!";

    protected TwoFactorControllerTestBase(WebApplicationFactory<ApiEntryPoint> factory, bool twoFactorEnabled = true)
        : base(factory)
    {
        TwoFactorFactory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISystemClock>();
                services.AddSingleton<ISystemClock>(Clock);
                services.RemoveAll<TwoFactorOptions>();
                services.AddSingleton(new TwoFactorOptions { Enabled = twoFactorEnabled });
            })
        );
    }

    protected ShiftableClock Clock { get; } = new();

    protected WebApplicationFactory<ApiEntryPoint> TwoFactorFactory { get; }

    /// <summary>Registers <paramref name="username"/> in <paramref name="browser"/>, which is then signed in.</summary>
    protected static async Task<string> RegisterAsync(HttpClient browser, string username)
    {
        var response = await browser.PostAsJsonAsync(
            "/register/register",
            new RegisterDto
            {
                Username = username,
                Email = username + "@example.com",
                Password = Password,
            }
        );
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    protected static Task<HttpResponseMessage> LoginAsync(HttpClient browser, string username, string password = Password) =>
        browser.PostAsJsonAsync("/signin/login", new LoginDto { Username = username, Password = password });

    protected static async Task<TwoFactorChallengeResponse> LoginForChallengeAsync(HttpClient browser, string username)
    {
        var response = await LoginAsync(browser, username);
        response.EnsureSuccessStatusCode();
        var challenge = await response.Content.ReadFromJsonAsync<TwoFactorChallengeResponse>();
        Assert.NotNull(challenge);
        Assert.True(challenge.TwoFactorRequired);
        return challenge;
    }

    protected static Task<HttpResponseMessage> CompleteTwoFactorAsync(
        HttpClient browser,
        string challengeToken,
        string code
    ) =>
        browser.PostAsJsonAsync("/signin/two-factor", new TwoFactorSignInDto { ChallengeToken = challengeToken, Code = code });

    protected static Task<HttpResponseMessage> CompleteTwoFactorWithRecoveryCodeAsync(
        HttpClient browser,
        string challengeToken,
        string recoveryCode
    ) =>
        browser.PostAsJsonAsync(
            "/signin/two-factor",
            new TwoFactorSignInDto { ChallengeToken = challengeToken, RecoveryCode = recoveryCode }
        );

    protected static Task<HttpResponseMessage> ReauthenticateAsync(
        HttpClient browser,
        string password = Password,
        string? code = null,
        string? recoveryCode = null
    ) =>
        browser.PostAsJsonAsync(
            "/account/reauthenticate",
            new ReauthenticateDto
            {
                Password = password,
                Code = code,
                RecoveryCode = recoveryCode,
            }
        );

    /// <summary>Asks for a new set of recovery codes; the browser must have re-authenticated.</summary>
    protected static async Task<IReadOnlyList<string>> RegenerateRecoveryCodesAsync(HttpClient browser)
    {
        var response = await browser.PostAsync("/account/two-factor/recovery-codes", null);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<RecoveryCodesResponse>();
        Assert.NotNull(body);
        return body.RecoveryCodes;
    }

    protected static string WrongCode(string code) => code == "000000" ? "111111" : "000000";

    /// <summary>A browser: a client with its own cookie jar.</summary>
    protected HttpClient NewBrowser() => TwoFactorFactory.CreateClient();

    /// <summary>Signs a 2FA account in through both steps; returns the session token.</summary>
    protected async Task<string> SignInWithCodeAsync(HttpClient browser, string username, string secret)
    {
        var challenge = await LoginForChallengeAsync(browser, username);
        var response = await CompleteTwoFactorAsync(browser, challenge.ChallengeToken, NextCode(secret));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>What <c>/signin/google</c> caches for a new user, on this host's own cache.</summary>
    protected void SeedProviderRegistration(string registrationToken, string internalId, string email) =>
        TwoFactorFactory.Services.GetRequiredService<IMemoryCache>().Set(
            "registrationapikey" + registrationToken,
            new Dictionary<string, string> { { "sub", internalId }, { "email", email } },
            TimeSpan.FromHours(2)
        );

    /// <summary>Turns 2FA on for an existing account as a confirmed setup would; returns the app's secret.</summary>
    protected async Task<string> EnableTotpAsync(string username)
    {
        var secret = TotpAlgorithm.GenerateSecret();
        var protection = TwoFactorFactory.Services.GetRequiredService<IEmailProtectionService>();
        await using var context = await ContextFactory.CreateDbContextAsync();
        var credentials = await context.Set<UserCredentialsEntity>().SingleAsync(c => c.Username == username);
        context.Set<UserTotpAuthenticatorEntity>().Add(
            new UserTotpAuthenticatorEntity
            {
                UserId = credentials.UserId,
                SecretProtected = protection.EncryptSecret(secret),
                ConfirmedAt = Clock.UtcNow,
                CreatedAt = Clock.UtcNow,
            }
        );
        credentials.TwoFactorEnabledAt = Clock.UtcNow;
        await context.SaveChangesAsync();
        return secret;
    }

    /// <summary>
    /// Turns 2FA on for an existing account and asks the API for its recovery codes, from a browser
    /// that signs in and re-authenticates for that alone.
    /// </summary>
    protected async Task<(string Secret, IReadOnlyList<string> RecoveryCodes)> EnableTotpWithRecoveryCodesAsync(
        string username
    )
    {
        var secret = await EnableTotpAsync(username);
        using var browser = NewBrowser();
        await SignInWithCodeAsync(browser, username, secret);
        (await ReauthenticateAsync(browser, code: NextCode(secret))).EnsureSuccessStatusCode();
        return (secret, await RegenerateRecoveryCodesAsync(browser));
    }

    /// <summary>Moves to the next 30-second step and returns the authenticator code shown there.</summary>
    protected string NextCode(string secret)
    {
        Clock.Advance(TimeSpan.FromSeconds(30));
        return TotpAlgorithm.ComputeCode(TotpAlgorithm.DecodeSecret(secret), Clock.UtcNow.ToUnixTimeSeconds() / 30);
    }

    protected async Task<UserCredentialsEntity> GetCredentialsAsync(string username)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<UserCredentialsEntity>().AsNoTracking().SingleAsync(c => c.Username == username);
    }

    /// <summary>One request authenticated by <paramref name="jwt"/> alone, on the shifted clock.</summary>
    protected async Task<HttpResponseMessage> SendWithBearerOnlyAsync(HttpMethod method, string path, string jwt)
    {
        using var client = TwoFactorFactory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        return await client.SendAsync(request);
    }

    protected sealed class ShiftableClock : ISystemClock
    {
        private TimeSpan _offset;

        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow + _offset;

        public void Advance(TimeSpan by) => _offset += by;
    }
}
