using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.QueryHandlers;
using OpenGitBase.Features.AccountSecurity.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.AccountSecurity.Tests.QueryHandlers;

public class ResetTwoFactorQueryHandlerTests
{
    private static readonly UserId Admin = UserId.From(Guid.NewGuid());

    [Fact]
    public async Task RunQueryAsync_RemovesEverySecondFactorAndPasskey_AndRevokesSessions()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync("alice");
        await scope.EnableTotpWithRecoveryCodesAsync(userId);
        using var passkey = AccountSecurityTestScope.NewPasskey();
        await scope.RegisterPasskeyAsync(userId, passkey);
        scope.Clock.Advance(TimeSpan.FromMinutes(1));

        var result = await ResetAsync(scope, "alice");

        QueryHandlerResultAssert.AssertSome(result);
        Assert.Null(await scope.GetAuthenticatorAsync(userId));
        Assert.Empty(await scope.GetRecoveryCodesAsync(userId));
        Assert.Empty(await scope.GetPasskeysAsync(userId));
        var credentials = await scope.GetCredentialsAsync(userId);
        Assert.Null(credentials.TwoFactorEnabledAt);
        Assert.Equal(scope.Clock.UtcNow, credentials.SessionsValidAfter);
    }

    [Fact]
    public async Task RunQueryAsync_ClearsALockout()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync("alice");
        await scope.EnableTotpAsync(userId);
        await using (var context = await scope.CreateDbContextAsync())
        {
            var credentials = await context.Set<UserCredentialsEntity>().SingleAsync(c => c.UserId == userId.Value);
            credentials.SecondFactorFailedAttempts = 3;
            credentials.SecondFactorLockedUntil = scope.Clock.UtcNow.AddMinutes(10);
            await context.SaveChangesAsync();
        }

        await ResetAsync(scope, "alice");

        var reset = await scope.GetCredentialsAsync(userId);
        Assert.Equal(0, reset.SecondFactorFailedAttempts);
        Assert.Null(reset.SecondFactorLockedUntil);
    }

    [Fact]
    public async Task RunQueryAsync_EmailsTheUserOnce_AndLogsTheAdmin()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync("alice");
        await scope.EnableTotpAsync(userId);

        await ResetAsync(scope, "alice");

        var email = Assert.Single(scope.Emails.Sent);
        Assert.Equal("Two-factor authentication reset by an administrator", email.Subject);
        Assert.Equal("alice@example.com", email.To.Email);
        Assert.DoesNotContain("203.0.113.7", email.HtmlBody, StringComparison.Ordinal);
        var logged = Assert.Single(scope.Logs.Named("security.two_factor.admin_reset"));
        Assert.Equal(userId.Value, logged.Properties["UserId"]);
        Assert.Equal("203.0.113.7", logged.Properties["RemoteIp"]);
        Assert.Equal($"admin={Admin.Value}", logged.Properties["Detail"]);
    }

    [Fact]
    public async Task RunQueryAsync_Afterwards_SignInNeedsOnlyThePassword_And2FaCanBeSetUpAgain()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync("alice");
        await scope.EnableTotpWithRecoveryCodesAsync(userId);

        await ResetAsync(scope, "alice");

        var start = await scope
            .GetHandler<BeginTwoFactorSignInQueryHandler>()
            .RunQueryAsync(new BeginTwoFactorSignInQuery { UserId = userId }, CancellationToken.None);
        Assert.False(QueryHandlerResultAssert.AssertSome(start).Required);
        scope.Clock.Advance(TimeSpan.FromSeconds(30));
        var codes = await scope.EnableTotpWithRecoveryCodesAsync(userId);
        Assert.Equal(10, codes.Count);
        Assert.NotNull((await scope.GetCredentialsAsync(userId)).TwoFactorEnabledAt);
    }

    [Fact]
    public async Task RunQueryAsync_MatchesTheUsernameInAnyCase()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync("alice");
        await scope.EnableTotpAsync(userId);

        var result = await ResetAsync(scope, "Alice");

        QueryHandlerResultAssert.AssertSome(result);
        Assert.Null(await scope.GetAuthenticatorAsync(userId));
    }

    [Fact]
    public async Task RunQueryAsync_LeavesOtherUsersAlone()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var alice = await scope.SeedUserAsync("alice");
        var bob = await scope.SeedUserAsync("bob");
        await scope.EnableTotpAsync(alice);
        await scope.EnableTotpWithRecoveryCodesAsync(bob);

        await ResetAsync(scope, "alice");

        Assert.NotNull(await scope.GetAuthenticatorAsync(bob));
        Assert.Equal(10, (await scope.GetRecoveryCodesAsync(bob)).Count);
        Assert.NotNull((await scope.GetCredentialsAsync(bob)).TwoFactorEnabledAt);
    }

    [Fact]
    public async Task RunQueryAsync_UnknownUser_ReturnsNone()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();

        var result = await ResetAsync(scope, "nobody");

        Assert.True(result.IsNone);
        Assert.Empty(scope.Emails.Sent);
    }

    [Fact]
    public async Task RunQueryAsync_DeletedUser_ReturnsNone()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync("alice");
        await using (var context = await scope.CreateDbContextAsync())
        {
            var credentials = await context.Set<UserCredentialsEntity>().SingleAsync(c => c.UserId == userId.Value);
            credentials.Deleted = true;
            await context.SaveChangesAsync();
        }

        var result = await ResetAsync(scope, "alice");

        Assert.True(result.IsNone);
    }

    private static Task<Option<Unit>> ResetAsync(AccountSecurityTestScope scope, string username) =>
        scope
            .GetHandler<ResetTwoFactorQueryHandler>()
            .RunQueryAsync(new ResetTwoFactorQuery { Username = username, AdminUserId = Admin }, CancellationToken.None);
}
