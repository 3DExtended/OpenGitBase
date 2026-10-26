using Microsoft.Extensions.Logging;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.QueryHandlers;
using OpenGitBase.Features.AccountSecurity.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Tests.QueryHandlers;

/// <summary>
/// The security events of mfa-01 to mfa-05 across handlers: each listed event emails the account once,
/// a failing email never fails the action, and every log entry follows the <c>security.*</c> scheme.
/// </summary>
public class SecurityEventsTests
{
    private const string SessionId = "session-1";

    [Fact]
    public async Task TurningTwoFactorOnWithAnApp_EmailsOnceAndLogsTheMethod()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();

        await scope.EnableTotpWithRecoveryCodesAsync(userId);

        Assert.Equal(["Two-factor authentication turned on"], Subjects(scope));
        var logged = Assert.Single(scope.Logs.Named("security.two_factor.enabled"));
        Assert.Equal(userId.Value, logged.Properties["UserId"]);
        Assert.Equal("203.0.113.7", logged.Properties["RemoteIp"]);
        Assert.Equal("method=totp", logged.Properties["Detail"]);
    }

    [Fact]
    public async Task ReplacingTheAuthenticator_SendsNoFurtherEmail()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        await scope.EnableTotpWithRecoveryCodesAsync(userId);
        scope.Clock.Advance(TimeSpan.FromSeconds(30));

        var setup = await scope
            .GetHandler<StartTotpSetupQueryHandler>()
            .RunQueryAsync(new StartTotpSetupQuery { UserId = userId, Issuer = "OpenGitBase" }, CancellationToken.None);
        var replaced = await scope
            .GetHandler<ConfirmTotpSetupQueryHandler>()
            .RunQueryAsync(
                new ConfirmTotpSetupQuery { UserId = userId, Code = scope.CurrentCode(setup.Get().Secret) },
                CancellationToken.None
            );

        Assert.Equal(TotpConfirmationStatus.Confirmed, QueryHandlerResultAssert.AssertSome(replaced).Status);
        Assert.Single(scope.Emails.Sent);
        Assert.Single(scope.Logs.Named("security.two_factor.enabled"));
    }

    [Fact]
    public async Task TurningTwoFactorOnWithPasskeys_EmailsOnceAfterThePasskeyEmail()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        using var passkey = AccountSecurityTestScope.NewPasskey();
        await scope.RegisterPasskeyAsync(userId, passkey);

        await scope.EnableTwoFactorWithPasskeysAsync(userId);

        Assert.Equal(["Passkey added to your account", "Two-factor authentication turned on"], Subjects(scope));
        Assert.Equal("method=passkeys", Assert.Single(scope.Logs.Named("security.two_factor.enabled")).Properties["Detail"]);
    }

    [Fact]
    public async Task TurningTwoFactorOff_EmailsOnce_AndNotWhenItWasOff()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        await scope.EnableTotpAsync(userId);

        await DisableAsync(scope, userId);
        await DisableAsync(scope, userId);

        Assert.Equal(["Two-factor authentication turned off"], Subjects(scope));
        Assert.Single(scope.Logs.Named("security.two_factor.disabled"));
    }

    [Fact]
    public async Task AddingAPasskey_EmailsOnce()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();

        using var passkey = AccountSecurityTestScope.NewPasskey();
        var registration = await scope.RegisterPasskeyAsync(userId, passkey);

        Assert.Equal(["Passkey added to your account"], Subjects(scope));
        var logged = Assert.Single(scope.Logs.Named("security.passkey.added"));
        Assert.Equal($"passkey={registration.Passkey!.Id}", logged.Properties["Detail"]);
    }

    [Fact]
    public async Task SigningInWithARecoveryCode_EmailsOnceWithTheCodesLeft()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var codes = await scope.EnableTotpWithRecoveryCodesAsync(userId);

        var result = await CompleteAsync(scope, await BeginAsync(scope, userId), recoveryCode: codes[0]);

        Assert.Equal(TwoFactorSignInStatus.Succeeded, result.Status);
        Assert.Equal(["Two-factor authentication turned on", "Recovery code used to sign in"], Subjects(scope));
        Assert.Contains("9 unused recovery codes", scope.Emails.Sent[^1].HtmlBody, StringComparison.Ordinal);
        Assert.Equal("step=sign-in remaining=9", Assert.Single(scope.Logs.Named("security.recovery_code.used")).Properties["Detail"]);
        Assert.Equal("method=recovery", Assert.Single(scope.Logs.Named("security.two_factor.signed_in")).Properties["Detail"]);
    }

    [Fact]
    public async Task SigningInWithTheApp_SendsNoEmailButLogsTheSignIn()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var secret = await scope.EnableTotpAsync(userId);

        var result = await CompleteAsync(scope, await BeginAsync(scope, userId), code: scope.CurrentCode(secret));

        Assert.Equal(TwoFactorSignInStatus.Succeeded, result.Status);
        Assert.Empty(scope.Emails.Sent);
        Assert.Equal("method=totp", Assert.Single(scope.Logs.Named("security.two_factor.signed_in")).Properties["Detail"]);
    }

    [Fact]
    public async Task FifthWrongSecondStep_EmailsTheLockoutOnce()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var secret = await scope.EnableTotpAsync(userId);
        var wrong = AccountSecurityTestScope.WrongCode(scope.CurrentCode(secret));

        for (var attempt = 0; attempt < 6; attempt++)
        {
            await CompleteAsync(scope, await BeginAsync(scope, userId), code: wrong);
        }

        Assert.Equal(["Sign-in blocked after failed second steps"], Subjects(scope));
        var logged = Assert.Single(scope.Logs.Named("security.second_factor.locked_out"));
        Assert.StartsWith("step=sign-in failures=5 locked_until=", (string)logged.Properties["Detail"]!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LockoutDuringReauthentication_EmailsOnce()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var secret = await scope.EnableTotpAsync(userId);
        var wrong = AccountSecurityTestScope.WrongCode(scope.CurrentCode(secret));

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await ReauthenticateAsync(scope, userId, code: wrong);
        }

        Assert.Equal(["Sign-in blocked after failed second steps"], Subjects(scope));
        Assert.StartsWith(
            "step=re-authentication",
            (string)Assert.Single(scope.Logs.Named("security.second_factor.locked_out")).Properties["Detail"]!,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task RecoveryCodeInReauthentication_IsLoggedWithoutEmail()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var codes = await scope.EnableTotpWithRecoveryCodesAsync(userId);

        var result = await ReauthenticateAsync(scope, userId, recoveryCode: codes[0]);

        Assert.Equal(ReauthenticationStatus.Succeeded, result.Status);
        Assert.Equal(["Two-factor authentication turned on"], Subjects(scope));
        Assert.Equal(
            "step=re-authentication remaining=9",
            Assert.Single(scope.Logs.Named("security.recovery_code.used")).Properties["Detail"]
        );
    }

    [Fact]
    public async Task FailingEmail_DoesNotFailTheAction()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        scope.Emails.Failure = EmailSenderFailure.Throws;

        var codes = await scope.EnableTotpWithRecoveryCodesAsync(userId);
        var signIn = await CompleteAsync(scope, await BeginAsync(scope, userId), recoveryCode: codes[0]);

        Assert.Equal(10, codes.Count);
        Assert.NotNull((await scope.GetCredentialsAsync(userId)).TwoFactorEnabledAt);
        Assert.Equal(TwoFactorSignInStatus.Succeeded, signIn.Status);
        Assert.Equal(2, scope.Logs.Named("security.notification.failed").Count);
    }

    [Fact]
    public async Task EveryLogEntryOfTheFeature_UsesTheSchemeWithUserAndRemoteIp()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var codes = await scope.EnableTotpWithRecoveryCodesAsync(userId);
        await CompleteAsync(scope, await BeginAsync(scope, userId), recoveryCode: codes[0]);
        await scope
            .GetHandler<RegenerateRecoveryCodesQueryHandler>()
            .RunQueryAsync(new RegenerateRecoveryCodesQuery { UserId = userId }, CancellationToken.None);
        using var passkey = AccountSecurityTestScope.NewPasskey();
        var registration = await scope.RegisterPasskeyAsync(userId, passkey);
        await scope.RegisterPasskeyAsync(userId, passkey);
        await SignInWithPasskeyAsync(scope, passkey);
        await scope
            .GetHandler<DeletePasskeyQueryHandler>()
            .RunQueryAsync(new DeletePasskeyQuery { UserId = userId, PasskeyId = registration.Passkey!.Id }, CancellationToken.None);
        await SignInWithPasskeyAsync(scope, passkey);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            // Five digits never match, unlike a six-digit guess.
            await CompleteAsync(scope, await BeginAsync(scope, userId), code: "12345");
        }

        scope.Emails.Failure = EmailSenderFailure.ReturnsNone;
        await DisableAsync(scope, userId);

        var entries = scope.Logs.Entries
            .Where(entry => entry.Category.StartsWith("OpenGitBase.Features.AccountSecurity", StringComparison.Ordinal))
            .Where(entry => entry.Level >= LogLevel.Information)
            .ToList();
        Assert.Equal(
            [
                "security.notification.failed",
                "security.passkey.added",
                "security.passkey.registration_rejected",
                "security.passkey.rejected",
                "security.passkey.removed",
                "security.passkey.signed_in",
                "security.recovery_code.used",
                "security.recovery_codes.regenerated",
                "security.second_factor.locked_out",
                "security.two_factor.disabled",
                "security.two_factor.enabled",
                "security.two_factor.signed_in",
            ],
            entries.Select(entry => entry.EventId.Name ?? string.Empty).Distinct().Order(StringComparer.Ordinal).ToArray()
        );
        Assert.All(
            entries,
            entry =>
            {
                Assert.StartsWith(entry.EventId.Name + " user=", entry.Message, StringComparison.Ordinal);
                Assert.Equal("203.0.113.7", entry.Properties["RemoteIp"]);
            }
        );
    }

    private static string[] Subjects(AccountSecurityTestScope scope) =>
        scope.Emails.Sent.Select(email => email.Subject).ToArray();

    private static async Task<string> BeginAsync(AccountSecurityTestScope scope, UserId userId)
    {
        var start = QueryHandlerResultAssert.AssertSome(
            await scope
                .GetHandler<BeginTwoFactorSignInQueryHandler>()
                .RunQueryAsync(new BeginTwoFactorSignInQuery { UserId = userId }, CancellationToken.None)
        );
        return start.ChallengeToken!;
    }

    private static async Task<TwoFactorSignInResult> CompleteAsync(
        AccountSecurityTestScope scope,
        string token,
        string code = "",
        string? recoveryCode = null
    ) =>
        QueryHandlerResultAssert.AssertSome(
            await scope
                .GetHandler<CompleteTwoFactorSignInQueryHandler>()
                .RunQueryAsync(
                    new CompleteTwoFactorSignInQuery
                    {
                        ChallengeToken = token,
                        Code = code,
                        RecoveryCode = recoveryCode,
                    },
                    CancellationToken.None
                )
        );

    private static async Task<ReauthenticationResult> ReauthenticateAsync(
        AccountSecurityTestScope scope,
        UserId userId,
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
                        Password = AccountSecurityTestScope.Password,
                        Code = code,
                        RecoveryCode = recoveryCode,
                    },
                    CancellationToken.None
                )
        );

    private static Task<Option<Unit>> DisableAsync(AccountSecurityTestScope scope, UserId userId) =>
        scope
            .GetHandler<DisableTwoFactorQueryHandler>()
            .RunQueryAsync(new DisableTwoFactorQuery { UserId = userId }, CancellationToken.None);

    private static async Task SignInWithPasskeyAsync(AccountSecurityTestScope scope, SoftwarePasskeyAuthenticator passkey)
    {
        var options = QueryHandlerResultAssert.AssertSome(
            await scope
                .GetHandler<StartPasskeySignInQueryHandler>()
                .RunQueryAsync(new StartPasskeySignInQuery(), CancellationToken.None)
        );
        await scope
            .GetHandler<CompletePasskeySignInQueryHandler>()
            .RunQueryAsync(
                new CompletePasskeySignInQuery { AssertionResponseJson = passkey.GetAssertion(options.OptionsJson) },
                CancellationToken.None
            );
    }
}
