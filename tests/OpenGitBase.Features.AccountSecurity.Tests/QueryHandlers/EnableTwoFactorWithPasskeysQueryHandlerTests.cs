using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.QueryHandlers;
using OpenGitBase.Features.AccountSecurity.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Tests.QueryHandlers;

public class EnableTwoFactorWithPasskeysQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WithAPasskey_TurnsTwoFactorOnWithFreshRecoveryCodesAndRevokesSessions()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        using var passkey = AccountSecurityTestScope.NewPasskey();
        await scope.RegisterPasskeyAsync(userId, passkey);

        var result = await EnableAsync(scope, userId);

        Assert.Equal(PasskeyTwoFactorActivationStatus.Enabled, result.Status);
        Assert.Equal(10, result.RecoveryCodes!.Distinct().Count());
        var credentials = await scope.GetCredentialsAsync(userId);
        Assert.Equal(scope.Clock.UtcNow, credentials.TwoFactorEnabledAt);
        Assert.Equal(scope.Clock.UtcNow, credentials.SessionsValidAfter);
        Assert.Equal(10, (await scope.GetRecoveryCodesAsync(userId)).Count);
        Assert.Null(await scope.GetAuthenticatorAsync(userId));
    }

    [Fact]
    public async Task RunQueryAsync_WithoutPasskeys_ChangesNothing()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();

        var result = await EnableAsync(scope, userId);

        Assert.Equal(PasskeyTwoFactorActivationStatus.NoPasskeys, result.Status);
        Assert.Null(result.RecoveryCodes);
        Assert.Null((await scope.GetCredentialsAsync(userId)).TwoFactorEnabledAt);
        Assert.Empty(await scope.GetRecoveryCodesAsync(userId));
    }

    [Fact]
    public async Task RunQueryAsync_TwoFactorAlreadyOn_KeepsTheRecoveryCodes()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var codes = await scope.EnableTotpWithRecoveryCodesAsync(userId);
        using var passkey = AccountSecurityTestScope.NewPasskey();
        await scope.RegisterPasskeyAsync(userId, passkey);
        var before = (await scope.GetRecoveryCodesAsync(userId)).Select(code => code.Id).Order().ToList();

        var result = await EnableAsync(scope, userId);

        Assert.Equal(PasskeyTwoFactorActivationStatus.AlreadyEnabled, result.Status);
        Assert.Null(result.RecoveryCodes);
        Assert.Equal(before, (await scope.GetRecoveryCodesAsync(userId)).Select(code => code.Id).Order().ToList());
        Assert.Equal(10, codes.Count);
    }

    [Fact]
    public async Task RunQueryAsync_UnknownUser_ReturnsNone()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();

        var result = await scope
            .GetHandler<EnableTwoFactorWithPasskeysQueryHandler>()
            .RunQueryAsync(new EnableTwoFactorWithPasskeysQuery { UserId = UserId.From(Guid.NewGuid()) }, CancellationToken.None);

        Assert.True(result.IsNone);
    }

    private static async Task<PasskeyTwoFactorActivation> EnableAsync(AccountSecurityTestScope scope, UserId userId) =>
        QueryHandlerResultAssert.AssertSome(
            await scope
                .GetHandler<EnableTwoFactorWithPasskeysQueryHandler>()
                .RunQueryAsync(new EnableTwoFactorWithPasskeysQuery { UserId = userId }, CancellationToken.None)
        );
}
