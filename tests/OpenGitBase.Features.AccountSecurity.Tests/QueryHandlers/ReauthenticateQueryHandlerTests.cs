using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.QueryHandlers;
using OpenGitBase.Features.AccountSecurity.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Tests.QueryHandlers;

public class ReauthenticateQueryHandlerTests
{
    private const string SessionId = "session-a";

    [Fact]
    public async Task RunQueryAsync_CorrectPassword_ReauthenticatesThisSessionForTenMinutes()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();

        var result = await ReauthenticateAsync(scope, userId, AccountSecurityTestScope.Password);

        Assert.Equal(ReauthenticationStatus.Succeeded, result.Status);
        Assert.True(await IsRecentAsync(scope, userId, SessionId));
        Assert.False(await IsRecentAsync(scope, userId, "session-b"));
        scope.Clock.Advance(TimeSpan.FromMinutes(10));
        Assert.False(await IsRecentAsync(scope, userId, SessionId));
    }

    [Fact]
    public async Task RunQueryAsync_AgainInTheSameSession_RenewsTheWindow()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        await ReauthenticateAsync(scope, userId, AccountSecurityTestScope.Password);
        scope.Clock.Advance(TimeSpan.FromMinutes(8));

        var again = await ReauthenticateAsync(scope, userId, AccountSecurityTestScope.Password);
        scope.Clock.Advance(TimeSpan.FromMinutes(8));

        Assert.Equal(ReauthenticationStatus.Succeeded, again.Status);
        Assert.True(await IsRecentAsync(scope, userId, SessionId));
    }

    [Fact]
    public async Task RunQueryAsync_WrongPassword_IsRejected()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();

        var result = await ReauthenticateAsync(scope, userId, "wrong");

        Assert.Equal(ReauthenticationStatus.InvalidCredentials, result.Status);
        Assert.False(await IsRecentAsync(scope, userId, SessionId));
    }

    [Fact]
    public async Task RunQueryAsync_WithTwoFactorOn_AlsoNeedsACode()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var secret = await scope.EnableTotpAsync(userId);

        var withoutCode = await ReauthenticateAsync(scope, userId, AccountSecurityTestScope.Password);
        var wrongCode = await ReauthenticateAsync(
            scope,
            userId,
            AccountSecurityTestScope.Password,
            AccountSecurityTestScope.WrongCode(scope.CurrentCode(secret))
        );
        var failuresAfterWrongCode = (await scope.GetCredentialsAsync(userId)).SecondFactorFailedAttempts;
        var withCode = await ReauthenticateAsync(scope, userId, AccountSecurityTestScope.Password, scope.CurrentCode(secret));

        Assert.Equal(ReauthenticationStatus.SecondFactorRequired, withoutCode.Status);
        Assert.Equal(ReauthenticationStatus.InvalidCode, wrongCode.Status);
        Assert.Equal(1, failuresAfterWrongCode);
        Assert.Equal(ReauthenticationStatus.Succeeded, withCode.Status);
        Assert.Equal(0, (await scope.GetCredentialsAsync(userId)).SecondFactorFailedAttempts);
        Assert.True(await IsRecentAsync(scope, userId, SessionId));
    }

    [Fact]
    public async Task RunQueryAsync_WrongCodesCountTowardTheSignInLockout()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var secret = await scope.EnableTotpAsync(userId);
        var wrong = AccountSecurityTestScope.WrongCode(scope.CurrentCode(secret));

        ReauthenticationResult last = null!;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            last = await ReauthenticateAsync(scope, userId, AccountSecurityTestScope.Password, wrong);
        }

        Assert.Equal(ReauthenticationStatus.LockedOut, last.Status);
        Assert.Equal(scope.Clock.UtcNow.AddMinutes(15), last.LockedUntil);
        var correct = await ReauthenticateAsync(scope, userId, AccountSecurityTestScope.Password, scope.CurrentCode(secret));
        Assert.Equal(ReauthenticationStatus.LockedOut, correct.Status);
    }

    [Fact]
    public async Task RunQueryAsync_RecoveryCodeInsteadOfTheAppCode_ReauthenticatesAndSpendsTheCode()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var codes = await scope.EnableTotpWithRecoveryCodesAsync(userId);

        var first = await ReauthenticateAsync(scope, userId, AccountSecurityTestScope.Password, recoveryCode: codes[0]);
        var reused = await ReauthenticateAsync(scope, userId, AccountSecurityTestScope.Password, recoveryCode: codes[0]);

        Assert.Equal(ReauthenticationStatus.Succeeded, first.Status);
        Assert.Equal(ReauthenticationStatus.InvalidCode, reused.Status);
        Assert.Equal(9, (await scope.GetRecoveryCodesAsync(userId)).Count(code => code.UsedAt is null));
    }

    [Fact]
    public async Task RunQueryAsync_WrongRecoveryCode_CountsTowardTheLockout()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        await scope.EnableTotpWithRecoveryCodesAsync(userId);

        var result = await ReauthenticateAsync(scope, userId, AccountSecurityTestScope.Password, recoveryCode: "aaaaa-aaaaa");

        Assert.Equal(ReauthenticationStatus.InvalidCode, result.Status);
        Assert.Equal(1, (await scope.GetCredentialsAsync(userId)).SecondFactorFailedAttempts);
    }

    [Fact]
    public async Task RunQueryAsync_AccountWithoutPasswordOrTwoFactor_CannotReauthenticate()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync(withPassword: false);

        var result = await ReauthenticateAsync(scope, userId, string.Empty);

        Assert.Equal(ReauthenticationStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task RunQueryAsync_AccountWithoutPassword_ReauthenticatesWithItsCode()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync(withPassword: false);
        var secret = await scope.EnableTotpAsync(userId);

        var result = await ReauthenticateAsync(scope, userId, string.Empty, scope.CurrentCode(secret));

        Assert.Equal(ReauthenticationStatus.Succeeded, result.Status);
    }

    private static async Task<ReauthenticationResult> ReauthenticateAsync(
        AccountSecurityTestScope scope,
        UserId userId,
        string password,
        string? code = null,
        string? recoveryCode = null
    ) =>
        QueryHandlerResultAssert.AssertSome(
            await scope
                .GetHandler<ReauthenticateQueryHandler>()
                .RunQueryAsync(
                    new ReauthenticateQuery
                    {
                        UserId = userId,
                        SessionId = SessionId,
                        Password = password,
                        Code = code,
                        RecoveryCode = recoveryCode,
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
