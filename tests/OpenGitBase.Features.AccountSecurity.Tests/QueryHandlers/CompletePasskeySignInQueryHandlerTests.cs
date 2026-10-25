using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.QueryHandlers;
using OpenGitBase.Features.AccountSecurity.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.AccountSecurity.Tests.QueryHandlers;

public class CompletePasskeySignInQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_ValidAssertion_SignsInTheOwnerAndRecordsTheUse()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        using var passkey = AccountSecurityTestScope.NewPasskey();
        await scope.RegisterPasskeyAsync(userId, passkey);
        scope.Clock.Advance(TimeSpan.FromMinutes(1));

        var result = await SignInAsync(scope, passkey.GetAssertion(await OptionsAsync(scope)));

        Assert.Equal(PasskeySignInStatus.SignedIn, result.Status);
        Assert.Equal(userId, result.UserId);
        var stored = Assert.Single(await scope.GetPasskeysAsync(userId));
        Assert.Equal(scope.Clock.UtcNow, stored.LastUsedAt);
        Assert.Equal(passkey.SignCount, (uint)stored.SignCount);
    }

    [Fact]
    public async Task RunQueryAsync_TwoFactorOn_StillSignsInWithoutASecondStep()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        await scope.EnableTotpAsync(userId);
        using var passkey = AccountSecurityTestScope.NewPasskey();
        await scope.RegisterPasskeyAsync(userId, passkey);

        var result = await SignInAsync(scope, passkey.GetAssertion(await OptionsAsync(scope)));

        Assert.Equal(PasskeySignInStatus.SignedIn, result.Status);
        Assert.Equal(userId, result.UserId);
    }

    [Fact]
    public async Task RunQueryAsync_UnregisteredPasskey_IsRejected()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        using var registered = AccountSecurityTestScope.NewPasskey();
        await scope.RegisterPasskeyAsync(userId, registered);
        using var stranger = AccountSecurityTestScope.NewPasskey();
        stranger.CreateCredential(await RegistrationOptionsAsync(scope, userId));

        var result = await SignInAsync(scope, stranger.GetAssertion(await OptionsAsync(scope)));

        Assert.Equal(PasskeySignInStatus.Rejected, result.Status);
        Assert.Null(result.UserId);
    }

    [Theory]
    [InlineData("origin")]
    [InlineData("rpId")]
    [InlineData("userVerification")]
    public async Task RunQueryAsync_BrokenAssertion_IsRejected(string broken)
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        using var passkey = AccountSecurityTestScope.NewPasskey();
        await scope.RegisterPasskeyAsync(userId, passkey);
        switch (broken)
        {
            case "origin":
                passkey.Origin = "https://opengitbase.evil.example";
                break;
            case "rpId":
                passkey.RelyingPartyIdOverride = "evil.example";
                break;
            default:
                passkey.UserVerified = false;
                break;
        }

        var result = await SignInAsync(scope, passkey.GetAssertion(await OptionsAsync(scope)));

        Assert.Equal(PasskeySignInStatus.Rejected, result.Status);
        Assert.Null(result.UserId);
    }

    [Fact]
    public async Task RunQueryAsync_SameAssertionTwice_IsRejectedTheSecondTime()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        using var passkey = AccountSecurityTestScope.NewPasskey();
        await scope.RegisterPasskeyAsync(userId, passkey);
        var assertion = passkey.GetAssertion(await OptionsAsync(scope));

        var first = await SignInAsync(scope, assertion);
        var replayed = await SignInAsync(scope, assertion);

        Assert.Equal(PasskeySignInStatus.SignedIn, first.Status);
        Assert.Equal(PasskeySignInStatus.Rejected, replayed.Status);
    }

    [Fact]
    public async Task RunQueryAsync_ChallengeOlderThanFiveMinutes_IsRejected()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        using var passkey = AccountSecurityTestScope.NewPasskey();
        await scope.RegisterPasskeyAsync(userId, passkey);
        var options = await OptionsAsync(scope);
        scope.Clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        var result = await SignInAsync(scope, passkey.GetAssertion(options));

        Assert.Equal(PasskeySignInStatus.Rejected, result.Status);
    }

    [Fact]
    public async Task RunQueryAsync_DeletedUser_IsRejected()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        using var passkey = AccountSecurityTestScope.NewPasskey();
        await scope.RegisterPasskeyAsync(userId, passkey);
        await using (var context = await scope.CreateDbContextAsync())
        {
            var credentials = await context.Set<UserCredentialsEntity>().SingleAsync(c => c.UserId == userId.Value);
            credentials.Deleted = true;
            await context.SaveChangesAsync();
        }

        var result = await SignInAsync(scope, passkey.GetAssertion(await OptionsAsync(scope)));

        Assert.Equal(PasskeySignInStatus.Rejected, result.Status);
        Assert.Null(result.UserId);
    }

    [Fact]
    public async Task RunQueryAsync_RegressedSignatureCounter_IsRejected()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        using var passkey = AccountSecurityTestScope.NewPasskey();
        await scope.RegisterPasskeyAsync(userId, passkey);
        passkey.SignCount = 5;
        Assert.Equal(PasskeySignInStatus.SignedIn, (await SignInAsync(scope, passkey.GetAssertion(await OptionsAsync(scope)))).Status);

        // A cloned authenticator answers with a counter the server has already seen.
        passkey.SignCount = 2;
        var result = await SignInAsync(scope, passkey.GetAssertion(await OptionsAsync(scope)));

        Assert.Equal(PasskeySignInStatus.Rejected, result.Status);
        Assert.Equal(6, Assert.Single(await scope.GetPasskeysAsync(userId)).SignCount);
    }

    [Fact]
    public async Task RunQueryAsync_SyncedPasskeyWithConstantZeroCounter_SignsInEveryTime()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        using var passkey = AccountSecurityTestScope.NewPasskey();
        passkey.Synced = true;
        passkey.CountsSignatures = false;
        await scope.RegisterPasskeyAsync(userId, passkey);

        var first = await SignInAsync(scope, passkey.GetAssertion(await OptionsAsync(scope)));
        var second = await SignInAsync(scope, passkey.GetAssertion(await OptionsAsync(scope)));

        Assert.Equal(PasskeySignInStatus.SignedIn, first.Status);
        Assert.Equal(PasskeySignInStatus.SignedIn, second.Status);
        Assert.Equal(0, Assert.Single(await scope.GetPasskeysAsync(userId)).SignCount);
    }

    [Fact]
    public async Task RunQueryAsync_NotJson_IsRejected()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();

        var result = await SignInAsync(scope, "not json");

        Assert.Equal(PasskeySignInStatus.Rejected, result.Status);
    }

    private static async Task<string> OptionsAsync(AccountSecurityTestScope scope) =>
        QueryHandlerResultAssert.AssertSome(
            await scope
                .GetHandler<StartPasskeySignInQueryHandler>()
                .RunQueryAsync(new StartPasskeySignInQuery(), CancellationToken.None)
        ).OptionsJson;

    private static async Task<string> RegistrationOptionsAsync(
        AccountSecurityTestScope scope,
        UserId userId
    ) =>
        QueryHandlerResultAssert.AssertSome(
            await scope
                .GetHandler<StartPasskeyRegistrationQueryHandler>()
                .RunQueryAsync(new StartPasskeyRegistrationQuery { UserId = userId }, CancellationToken.None)
        ).OptionsJson;

    private static async Task<PasskeySignInResult> SignInAsync(AccountSecurityTestScope scope, string assertionJson) =>
        QueryHandlerResultAssert.AssertSome(
            await scope
                .GetHandler<CompletePasskeySignInQueryHandler>()
                .RunQueryAsync(
                    new CompletePasskeySignInQuery { AssertionResponseJson = assertionJson },
                    CancellationToken.None
                )
        );
}
