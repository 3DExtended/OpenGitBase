using System.Text.Json;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.QueryHandlers;
using OpenGitBase.Features.AccountSecurity.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Tests.QueryHandlers;

public class StartTwoFactorPasskeyQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_OpenSignIn_AllowsTheUsersPasskeysAndOnlyPrefersUserVerification()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        await scope.EnableTotpAsync(userId);
        using var passkey = AccountSecurityTestScope.NewPasskey();
        await scope.RegisterPasskeyAsync(userId, passkey);

        var result = await StartAsync(scope, await BeginAsync(scope, userId));

        Assert.Equal(TwoFactorPasskeyOptionsStatus.Issued, result.Status);
        using var options = JsonDocument.Parse(result.OptionsJson!);
        Assert.Equal("preferred", options.RootElement.GetProperty("userVerification").GetString());
        var allowed = Assert.Single(options.RootElement.GetProperty("allowCredentials").EnumerateArray());
        Assert.Equal(SoftwarePasskeyAuthenticator.ToBase64Url(passkey.CredentialId), allowed.GetProperty("id").GetString());
    }

    [Fact]
    public async Task RunQueryAsync_UnknownChallenge_HasExpired()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();

        var result = await StartAsync(scope, "not-a-challenge");

        Assert.Equal(TwoFactorPasskeyOptionsStatus.ChallengeExpired, result.Status);
        Assert.Null(result.OptionsJson);
    }

    [Fact]
    public async Task RunQueryAsync_ChallengeOlderThanFiveMinutes_HasExpired()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        await scope.EnableTotpAsync(userId);
        using var passkey = AccountSecurityTestScope.NewPasskey();
        await scope.RegisterPasskeyAsync(userId, passkey);
        var token = await BeginAsync(scope, userId);
        scope.Clock.Advance(TimeSpan.FromMinutes(5));

        var result = await StartAsync(scope, token);

        Assert.Equal(TwoFactorPasskeyOptionsStatus.ChallengeExpired, result.Status);
    }

    [Fact]
    public async Task RunQueryAsync_NoPasskeys_SaysSo()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        await scope.EnableTotpAsync(userId);

        var result = await StartAsync(scope, await BeginAsync(scope, userId));

        Assert.Equal(TwoFactorPasskeyOptionsStatus.NoPasskeys, result.Status);
    }

    private static async Task<string> BeginAsync(AccountSecurityTestScope scope, UserId userId) =>
        QueryHandlerResultAssert.AssertSome(
            await scope
                .GetHandler<BeginTwoFactorSignInQueryHandler>()
                .RunQueryAsync(new BeginTwoFactorSignInQuery { UserId = userId }, CancellationToken.None)
        ).ChallengeToken!;

    private static async Task<TwoFactorPasskeyOptions> StartAsync(AccountSecurityTestScope scope, string token) =>
        QueryHandlerResultAssert.AssertSome(
            await scope
                .GetHandler<StartTwoFactorPasskeyQueryHandler>()
                .RunQueryAsync(new StartTwoFactorPasskeyQuery { ChallengeToken = token }, CancellationToken.None)
        );
}
