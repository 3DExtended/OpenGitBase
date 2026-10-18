using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.QueryHandlers;
using OpenGitBase.Features.AccountSecurity.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Tests.QueryHandlers;

public class ReauthenticateWithPasskeyQueryHandlerTests
{
    private const string SessionId = "session-1";

    [Fact]
    public async Task RunQueryAsync_ValidAssertion_ReauthenticatesThisSessionAndRecordsTheUse()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        using var passkey = AccountSecurityTestScope.NewPasskey();
        await scope.RegisterPasskeyAsync(userId, passkey);
        scope.Clock.Advance(TimeSpan.FromMinutes(1));

        var result = await ReauthenticateAsync(scope, userId, passkey.GetAssertion(await OptionsAsync(scope, userId)));

        Assert.Equal(ReauthenticationStatus.Succeeded, result.Status);
        Assert.True(await IsRecentAsync(scope, userId, SessionId));
        Assert.False(await IsRecentAsync(scope, userId, "another-session"));
        var stored = Assert.Single(await scope.GetPasskeysAsync(userId));
        Assert.Equal(scope.Clock.UtcNow, stored.LastUsedAt);
        Assert.Equal(passkey.SignCount, (uint)stored.SignCount);
    }

    [Fact]
    public async Task RunQueryAsync_AnotherUsersPasskey_IsRejected()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var alice = await scope.SeedUserAsync("alice");
        var mallory = await scope.SeedUserAsync("mallory");
        using var alicesPasskey = AccountSecurityTestScope.NewPasskey();
        using var mallorysPasskey = AccountSecurityTestScope.NewPasskey();
        await scope.RegisterPasskeyAsync(alice, alicesPasskey);
        await scope.RegisterPasskeyAsync(mallory, mallorysPasskey);

        var result = await ReauthenticateAsync(scope, alice, mallorysPasskey.GetAssertion(await OptionsAsync(scope, alice)));

        Assert.Equal(ReauthenticationStatus.PasskeyRejected, result.Status);
        Assert.False(await IsRecentAsync(scope, alice, SessionId));
    }

    [Fact]
    public async Task RunQueryAsync_WithoutUserVerification_IsRejected()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        using var passkey = AccountSecurityTestScope.NewPasskey();
        await scope.RegisterPasskeyAsync(userId, passkey);
        passkey.UserVerified = false;

        var result = await ReauthenticateAsync(scope, userId, passkey.GetAssertion(await OptionsAsync(scope, userId)));

        Assert.Equal(ReauthenticationStatus.PasskeyRejected, result.Status);
        Assert.False(await IsRecentAsync(scope, userId, SessionId));
    }

    [Fact]
    public async Task RunQueryAsync_SameAssertionTwice_IsRejectedTheSecondTime()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        using var passkey = AccountSecurityTestScope.NewPasskey();
        await scope.RegisterPasskeyAsync(userId, passkey);
        var assertion = passkey.GetAssertion(await OptionsAsync(scope, userId));

        var first = await ReauthenticateAsync(scope, userId, assertion);
        var replayed = await ReauthenticateAsync(scope, userId, assertion);

        Assert.Equal(ReauthenticationStatus.Succeeded, first.Status);
        Assert.Equal(ReauthenticationStatus.ChallengeExpired, replayed.Status);
    }

    [Fact]
    public async Task RunQueryAsync_ChallengeOlderThanFiveMinutes_IsRejected()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        using var passkey = AccountSecurityTestScope.NewPasskey();
        await scope.RegisterPasskeyAsync(userId, passkey);
        var options = await OptionsAsync(scope, userId);
        scope.Clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        var result = await ReauthenticateAsync(scope, userId, passkey.GetAssertion(options));

        Assert.Equal(ReauthenticationStatus.ChallengeExpired, result.Status);
    }

    private static async Task<string> OptionsAsync(AccountSecurityTestScope scope, UserId userId) =>
        QueryHandlerResultAssert.AssertSome(
            await scope
                .GetHandler<StartPasskeyReauthenticationQueryHandler>()
                .RunQueryAsync(new StartPasskeyReauthenticationQuery { UserId = userId }, CancellationToken.None)
        ).OptionsJson;

    private static async Task<ReauthenticationResult> ReauthenticateAsync(
        AccountSecurityTestScope scope,
        UserId userId,
        string assertionJson
    ) =>
        QueryHandlerResultAssert.AssertSome(
            await scope
                .GetHandler<ReauthenticateWithPasskeyQueryHandler>()
                .RunQueryAsync(
                    new ReauthenticateWithPasskeyQuery
                    {
                        UserId = userId,
                        SessionId = SessionId,
                        AssertionResponseJson = assertionJson,
                    },
                    CancellationToken.None
                )
        );

    private static async Task<bool> IsRecentAsync(AccountSecurityTestScope scope, UserId userId, string sessionId) =>
        QueryHandlerResultAssert.AssertSome(
            await scope
                .GetHandler<HasRecentReauthenticationQueryHandler>()
                .RunQueryAsync(
                    new HasRecentReauthenticationQuery
                    {
                        UserId = userId,
                        SessionId = sessionId,
                        SessionIssuedAt = scope.Clock.UtcNow.AddHours(-1),
                    },
                    CancellationToken.None
                )
        );
}
