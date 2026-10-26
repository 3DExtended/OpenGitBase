using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.QueryHandlers;
using OpenGitBase.Features.AccountSecurity.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Tests.QueryHandlers;

public class CompleteTwoFactorSignInQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_CorrectCode_SignsInOnce()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var secret = await scope.EnableTotpAsync(userId);
        var token = await BeginAsync(scope, userId);

        var first = await CompleteAsync(scope, token, scope.CurrentCode(secret));
        var again = await CompleteAsync(scope, token, scope.CurrentCode(secret));

        Assert.Equal(TwoFactorSignInStatus.Succeeded, first.Status);
        Assert.Equal(userId, first.UserId);
        Assert.Equal(TwoFactorSignInStatus.ChallengeExpired, again.Status);
    }

    [Fact]
    public async Task RunQueryAsync_ACodeIsAcceptedOnlyOnce_EvenWithAFreshChallenge()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var secret = await scope.EnableTotpAsync(userId);
        var code = scope.CurrentCode(secret);
        await CompleteAsync(scope, await BeginAsync(scope, userId), code);

        var replay = await CompleteAsync(scope, await BeginAsync(scope, userId), code);

        Assert.Equal(TwoFactorSignInStatus.InvalidCode, replay.Status);
    }

    [Fact]
    public async Task RunQueryAsync_WrongCode_IsRejectedAndCounted()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var secret = await scope.EnableTotpAsync(userId);
        var token = await BeginAsync(scope, userId);

        var result = await CompleteAsync(scope, token, AccountSecurityTestScope.WrongCode(scope.CurrentCode(secret)));

        Assert.Equal(TwoFactorSignInStatus.InvalidCode, result.Status);
        Assert.Equal(1, (await scope.GetCredentialsAsync(userId)).SecondFactorFailedAttempts);
        Assert.Equal(TwoFactorSignInStatus.Succeeded, (await CompleteAsync(scope, token, scope.CurrentCode(secret))).Status);
        Assert.Equal(0, (await scope.GetCredentialsAsync(userId)).SecondFactorFailedAttempts);
    }

    [Fact]
    public async Task RunQueryAsync_ChallengeOlderThanFiveMinutes_HasExpired()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var secret = await scope.EnableTotpAsync(userId);
        var token = await BeginAsync(scope, userId);
        scope.Clock.Advance(TimeSpan.FromMinutes(5));

        var result = await CompleteAsync(scope, token, scope.CurrentCode(secret));

        Assert.Equal(TwoFactorSignInStatus.ChallengeExpired, result.Status);
    }

    [Fact]
    public async Task RunQueryAsync_UnknownChallenge_HasExpired()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();

        var result = await CompleteAsync(scope, "not-a-challenge", "123456");

        Assert.Equal(TwoFactorSignInStatus.ChallengeExpired, result.Status);
    }

    [Fact]
    public async Task RunQueryAsync_FifthConsecutiveFailure_LocksTheSecondStepForFifteenMinutes()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var secret = await scope.EnableTotpAsync(userId);
        var wrong = AccountSecurityTestScope.WrongCode(scope.CurrentCode(secret));

        // Failures count across challenges, so starting over with the password gains nothing.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await CompleteAsync(scope, await BeginAsync(scope, userId), wrong);
        }

        var token = await BeginAsync(scope, userId);
        await CompleteAsync(scope, token, wrong);
        await CompleteAsync(scope, token, wrong);
        var fifth = await CompleteAsync(scope, token, wrong);

        Assert.Equal(TwoFactorSignInStatus.LockedOut, fifth.Status);
        Assert.Equal(scope.Clock.UtcNow.AddMinutes(15), fifth.LockedUntil);
        // The lockout also consumed the challenge.
        Assert.Equal(TwoFactorSignInStatus.ChallengeExpired, (await CompleteAsync(scope, token, scope.CurrentCode(secret))).Status);
    }

    [Fact]
    public async Task RunQueryAsync_WhileLocked_RejectsEvenTheCorrectCode_UntilTheLockEnds()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var secret = await scope.EnableTotpAsync(userId);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await CompleteAsync(scope, await BeginAsync(scope, userId), AccountSecurityTestScope.WrongCode(scope.CurrentCode(secret)));
        }

        var locked = await CompleteAsync(scope, await BeginAsync(scope, userId), scope.CurrentCode(secret));
        scope.Clock.Advance(TimeSpan.FromMinutes(15));
        var afterLock = await CompleteAsync(scope, await BeginAsync(scope, userId), scope.CurrentCode(secret));

        Assert.Equal(TwoFactorSignInStatus.LockedOut, locked.Status);
        Assert.Equal(TwoFactorSignInStatus.Succeeded, afterLock.Status);
    }

    [Fact]
    public async Task RunQueryAsync_RecoveryCode_SignsInOnceAndIsSpent()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var codes = await scope.EnableTotpWithRecoveryCodesAsync(userId);

        var first = await CompleteWithRecoveryCodeAsync(scope, await BeginAsync(scope, userId), codes[3]);
        var reuse = await CompleteWithRecoveryCodeAsync(scope, await BeginAsync(scope, userId), codes[3]);

        Assert.Equal(TwoFactorSignInStatus.Succeeded, first.Status);
        Assert.Equal(userId, first.UserId);
        Assert.Equal(TwoFactorSignInStatus.InvalidCode, reuse.Status);
        var rows = await scope.GetRecoveryCodesAsync(userId);
        Assert.Single(rows, row => row.UsedAt == scope.Clock.UtcNow);
        Assert.Equal(9, rows.Count(row => row.UsedAt is null));
    }

    [Theory]
    [InlineData("upper case")]
    [InlineData("without dash")]
    [InlineData("with spaces")]
    public async Task RunQueryAsync_RecoveryCode_IsAcceptedInAnyCaseWithOrWithoutDashOrSpaces(string form)
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var code = (await scope.EnableTotpWithRecoveryCodesAsync(userId))[0];
        var typed = form switch
        {
            "upper case" => code.ToUpperInvariant(),
            "without dash" => code.Replace("-", string.Empty, StringComparison.Ordinal),
            _ => " " + code.Replace("-", " ", StringComparison.Ordinal) + " ",
        };

        var result = await CompleteWithRecoveryCodeAsync(scope, await BeginAsync(scope, userId), typed);

        Assert.Equal(TwoFactorSignInStatus.Succeeded, result.Status);
    }

    [Fact]
    public async Task RunQueryAsync_WrongRecoveryCodes_CountTowardTheLockout()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var codes = await scope.EnableTotpWithRecoveryCodesAsync(userId);
        var token = await BeginAsync(scope, userId);

        var results = new List<TwoFactorSignInResult>();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            results.Add(await CompleteWithRecoveryCodeAsync(scope, token, "aaaaa-aaaaa"));
        }

        Assert.All(results.Take(4), result => Assert.Equal(TwoFactorSignInStatus.InvalidCode, result.Status));
        Assert.Equal(TwoFactorSignInStatus.LockedOut, results[4].Status);
        var afterLockout = await CompleteWithRecoveryCodeAsync(scope, await BeginAsync(scope, userId), codes[0]);
        Assert.Equal(TwoFactorSignInStatus.LockedOut, afterLockout.Status);
        Assert.All(await scope.GetRecoveryCodesAsync(userId), row => Assert.Null(row.UsedAt));
    }

    [Fact]
    public async Task RunQueryAsync_PasskeyAssertion_SignsInOnceWithoutUserVerification()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        await scope.EnableTotpAsync(userId);
        using var securityKey = AccountSecurityTestScope.NewPasskey();
        await scope.RegisterPasskeyAsync(userId, securityKey);
        // After the password, a touch of a security key is enough.
        securityKey.UserVerified = false;
        var token = await BeginAsync(scope, userId);

        var result = await CompleteWithPasskeyAsync(scope, token, securityKey);

        Assert.Equal(TwoFactorSignInStatus.Succeeded, result.Status);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(scope.Clock.UtcNow, Assert.Single(await scope.GetPasskeysAsync(userId)).LastUsedAt);
        Assert.Equal(TwoFactorSignInStatus.ChallengeExpired, (await CompleteWithPasskeyAsync(scope, token, securityKey)).Status);
    }

    [Fact]
    public async Task RunQueryAsync_PasskeyOnlyTwoFactor_SignsInWithThePasskey()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        using var passkey = AccountSecurityTestScope.NewPasskey();
        await scope.RegisterPasskeyAsync(userId, passkey);
        await scope.EnableTwoFactorWithPasskeysAsync(userId);

        var result = await CompleteWithPasskeyAsync(scope, await BeginAsync(scope, userId), passkey);

        Assert.Equal(TwoFactorSignInStatus.Succeeded, result.Status);
    }

    [Fact]
    public async Task RunQueryAsync_AnotherUsersPasskey_IsRejectedAndCounted()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var alice = await scope.SeedUserAsync("alice");
        var mallory = await scope.SeedUserAsync("mallory");
        await scope.EnableTotpAsync(alice);
        using var alicesPasskey = AccountSecurityTestScope.NewPasskey();
        using var mallorysPasskey = AccountSecurityTestScope.NewPasskey();
        await scope.RegisterPasskeyAsync(alice, alicesPasskey);
        await scope.RegisterPasskeyAsync(mallory, mallorysPasskey);

        var result = await CompleteWithPasskeyAsync(scope, await BeginAsync(scope, alice), mallorysPasskey);

        Assert.Equal(TwoFactorSignInStatus.PasskeyRejected, result.Status);
        Assert.Null(result.UserId);
        Assert.Equal(1, (await scope.GetCredentialsAsync(alice)).SecondFactorFailedAttempts);
    }

    [Fact]
    public async Task RunQueryAsync_FiveRejectedPasskeys_LockTheSecondStep()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        await scope.EnableTotpAsync(userId);
        using var passkey = AccountSecurityTestScope.NewPasskey();
        await scope.RegisterPasskeyAsync(userId, passkey);
        var token = await BeginAsync(scope, userId);
        passkey.Origin = "https://opengitbase.evil.example";

        var results = new List<TwoFactorSignInResult>();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            results.Add(await CompleteWithPasskeyAsync(scope, token, passkey));
        }

        Assert.All(results.Take(4), result => Assert.Equal(TwoFactorSignInStatus.PasskeyRejected, result.Status));
        Assert.Equal(TwoFactorSignInStatus.LockedOut, results[4].Status);
        passkey.Origin = AccountSecurityTestScope.WebAuthnOrigin;
        var afterLockout = await CompleteWithPasskeyAsync(scope, await BeginAsync(scope, userId), passkey);
        Assert.Equal(TwoFactorSignInStatus.LockedOut, afterLockout.Status);
    }

    private static async Task<string> BeginAsync(AccountSecurityTestScope scope, UserId userId)
    {
        var start = QueryHandlerResultAssert.AssertSome(
            await scope
                .GetHandler<BeginTwoFactorSignInQueryHandler>()
                .RunQueryAsync(new BeginTwoFactorSignInQuery { UserId = userId }, CancellationToken.None)
        );
        return start.ChallengeToken!;
    }

    private static async Task<TwoFactorSignInResult> CompleteAsync(AccountSecurityTestScope scope, string token, string code)
    {
        Option<TwoFactorSignInResult> result = await scope
            .GetHandler<CompleteTwoFactorSignInQueryHandler>()
            .RunQueryAsync(new CompleteTwoFactorSignInQuery { ChallengeToken = token, Code = code }, CancellationToken.None);
        return QueryHandlerResultAssert.AssertSome(result);
    }

    /// <summary>Asks for passkey options with the challenge token, then answers with <paramref name="passkey"/>.</summary>
    private static async Task<TwoFactorSignInResult> CompleteWithPasskeyAsync(
        AccountSecurityTestScope scope,
        string token,
        SoftwarePasskeyAuthenticator passkey
    )
    {
        var options = QueryHandlerResultAssert.AssertSome(
            await scope
                .GetHandler<StartTwoFactorPasskeyQueryHandler>()
                .RunQueryAsync(new StartTwoFactorPasskeyQuery { ChallengeToken = token }, CancellationToken.None)
        );
        if (options.OptionsJson is null)
        {
            return new TwoFactorSignInResult(TwoFactorSignInStatus.ChallengeExpired);
        }

        Option<TwoFactorSignInResult> result = await scope
            .GetHandler<CompleteTwoFactorSignInQueryHandler>()
            .RunQueryAsync(
                new CompleteTwoFactorSignInQuery
                {
                    ChallengeToken = token,
                    PasskeyAssertionJson = passkey.GetAssertion(options.OptionsJson),
                },
                CancellationToken.None
            );
        return QueryHandlerResultAssert.AssertSome(result);
    }

    private static async Task<TwoFactorSignInResult> CompleteWithRecoveryCodeAsync(
        AccountSecurityTestScope scope,
        string token,
        string recoveryCode
    )
    {
        Option<TwoFactorSignInResult> result = await scope
            .GetHandler<CompleteTwoFactorSignInQueryHandler>()
            .RunQueryAsync(
                new CompleteTwoFactorSignInQuery { ChallengeToken = token, RecoveryCode = recoveryCode },
                CancellationToken.None
            );
        return QueryHandlerResultAssert.AssertSome(result);
    }
}
