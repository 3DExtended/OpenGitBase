using OpenGitBase.Common.Services;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.QueryHandlers;
using OpenGitBase.Features.AccountSecurity.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Tests.QueryHandlers;

public class ConfirmTotpSetupQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_CorrectCode_TurnsTwoFactorOnAndRevokesSessions()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var setup = await StartSetupAsync(scope, userId);

        var result = await Confirm(scope, userId, scope.CurrentCode(setup.Secret));

        var confirmation = QueryHandlerResultAssert.AssertSome(result);
        Assert.Equal(TotpConfirmationStatus.Confirmed, confirmation.Status);
        Assert.True(confirmation.TwoFactorNewlyEnabled);

        var authenticator = await scope.GetAuthenticatorAsync(userId);
        Assert.Equal(setup.Secret, scope.GetService<IEmailProtectionService>().DecryptSecret(authenticator!.SecretProtected!));
        Assert.Null(authenticator.PendingSecretProtected);
        Assert.Equal(scope.Clock.UtcNow, authenticator.ConfirmedAt);

        var credentials = await scope.GetCredentialsAsync(userId);
        Assert.Equal(scope.Clock.UtcNow, credentials.TwoFactorEnabledAt);
        Assert.Equal(scope.Clock.UtcNow, credentials.SessionsValidAfter);
    }

    [Fact]
    public async Task RunQueryAsync_WrongCode_ChangesNothing()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var setup = await StartSetupAsync(scope, userId);

        var result = await Confirm(scope, userId, AccountSecurityTestScope.WrongCode(scope.CurrentCode(setup.Secret)));

        Assert.Equal(TotpConfirmationStatus.InvalidCode, QueryHandlerResultAssert.AssertSome(result).Status);
        var authenticator = await scope.GetAuthenticatorAsync(userId);
        Assert.Null(authenticator!.SecretProtected);
        Assert.NotNull(authenticator.PendingSecretProtected);
        Assert.Null((await scope.GetCredentialsAsync(userId)).TwoFactorEnabledAt);
    }

    [Fact]
    public async Task RunQueryAsync_WithoutPendingSetup_ReportsIt()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();

        var result = await Confirm(scope, userId, "123456");

        Assert.Equal(TotpConfirmationStatus.NoPendingSetup, QueryHandlerResultAssert.AssertSome(result).Status);
    }

    [Fact]
    public async Task RunQueryAsync_NewAppReplacesTheConfirmedOne_WithoutTogglingTwoFactor()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var oldSecret = await scope.EnableTotpAsync(userId);
        var enabledAt = (await scope.GetCredentialsAsync(userId)).TwoFactorEnabledAt;
        scope.Clock.Advance(TimeSpan.FromMinutes(5));
        var setup = await StartSetupAsync(scope, userId);

        var result = await Confirm(scope, userId, scope.CurrentCode(setup.Secret));

        var confirmation = QueryHandlerResultAssert.AssertSome(result);
        Assert.Equal(TotpConfirmationStatus.Confirmed, confirmation.Status);
        Assert.False(confirmation.TwoFactorNewlyEnabled);
        var authenticator = await scope.GetAuthenticatorAsync(userId);
        var decrypted = scope.GetService<IEmailProtectionService>().DecryptSecret(authenticator!.SecretProtected!);
        Assert.Equal(setup.Secret, decrypted);
        Assert.NotEqual(oldSecret, decrypted);
        var credentials = await scope.GetCredentialsAsync(userId);
        Assert.Equal(enabledAt, credentials.TwoFactorEnabledAt);
        Assert.Null(credentials.SessionsValidAfter);
    }

    [Fact]
    public async Task RunQueryAsync_FirstConfirmation_ReturnsTenRecoveryCodesStoredOnlyAsHashes()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var setup = await StartSetupAsync(scope, userId);

        var confirmation = QueryHandlerResultAssert.AssertSome(await Confirm(scope, userId, scope.CurrentCode(setup.Secret)));

        var codes = confirmation.RecoveryCodes!;
        Assert.Equal(10, codes.Count);
        Assert.Equal(10, codes.Distinct().Count());
        Assert.All(codes, code => Assert.Matches("^[a-z2-7]{5}-[a-z2-7]{5}$", code));

        var rows = await scope.GetRecoveryCodesAsync(userId);
        Assert.Equal(10, rows.Count);
        Assert.All(rows, row => Assert.Null(row.UsedAt));
        var hasher = scope.GetService<IPasswordHasherService>();
        foreach (var code in codes)
        {
            var plain = code.Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
            Assert.DoesNotContain(rows, row => row.CodeHash.Contains(plain, StringComparison.OrdinalIgnoreCase));
            Assert.Single(rows, row => hasher.VerifyPassword(row.CodeHash, plain));
        }
    }

    [Fact]
    public async Task RunQueryAsync_ReplacingTheApp_KeepsTheRecoveryCodesAndReturnsNone()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var first = await StartSetupAsync(scope, userId);
        await Confirm(scope, userId, scope.CurrentCode(first.Secret));
        var rowsBefore = await scope.GetRecoveryCodesAsync(userId);
        scope.Clock.Advance(TimeSpan.FromMinutes(1));
        var second = await StartSetupAsync(scope, userId);

        var confirmation = QueryHandlerResultAssert.AssertSome(await Confirm(scope, userId, scope.CurrentCode(second.Secret)));

        Assert.Null(confirmation.RecoveryCodes);
        var rowsAfter = await scope.GetRecoveryCodesAsync(userId);
        Assert.Equal(rowsBefore.Select(row => row.Id).Order(), rowsAfter.Select(row => row.Id).Order());
    }

    [Fact]
    public async Task RunQueryAsync_TheConfirmationCodeCannotBeReusedToSignIn()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var setup = await StartSetupAsync(scope, userId);

        await Confirm(scope, userId, scope.CurrentCode(setup.Secret));

        var authenticator = await scope.GetAuthenticatorAsync(userId);
        Assert.Equal(scope.Clock.UtcNow.ToUnixTimeSeconds() / 30, authenticator!.LastUsedTimeStep);
    }

    private static async Task<TotpSetup> StartSetupAsync(AccountSecurityTestScope scope, UserId userId) =>
        QueryHandlerResultAssert.AssertSome(
            await scope
                .GetHandler<StartTotpSetupQueryHandler>()
                .RunQueryAsync(new StartTotpSetupQuery { UserId = userId, Issuer = "OpenGitBase" }, CancellationToken.None)
        );

    private static Task<OpenGitBase.Cqrs.Option<TotpConfirmation>> Confirm(
        AccountSecurityTestScope scope,
        UserId userId,
        string code
    ) =>
        scope
            .GetHandler<ConfirmTotpSetupQueryHandler>()
            .RunQueryAsync(new ConfirmTotpSetupQuery { UserId = userId, Code = code }, CancellationToken.None);
}
