using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.QueryHandlers;
using OpenGitBase.Features.AccountSecurity.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Tests.QueryHandlers;

public class DeletePasskeyQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_OwnPasskey_IsDeleted()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        using var passkey = AccountSecurityTestScope.NewPasskey();
        var registered = await scope.RegisterPasskeyAsync(userId, passkey);

        var result = await scope
            .GetHandler<DeletePasskeyQueryHandler>()
            .RunQueryAsync(new DeletePasskeyQuery { UserId = userId, PasskeyId = registered.Passkey!.Id }, CancellationToken.None);

        Assert.Equal(PasskeyDeletionStatus.Deleted, QueryHandlerResultAssert.AssertSome(result).Status);
        Assert.Empty(await scope.GetPasskeysAsync(userId));
    }

    [Fact]
    public async Task RunQueryAsync_LastPasskeyOfPasskeyOnlyTwoFactor_IsRefusedAndKept()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        using var passkey = AccountSecurityTestScope.NewPasskey();
        var registered = await scope.RegisterPasskeyAsync(userId, passkey);
        await scope.EnableTwoFactorWithPasskeysAsync(userId);

        var result = await DeleteAsync(scope, userId, registered.Passkey!.Id);

        Assert.Equal(PasskeyDeletionStatus.LastSecondFactor, result.Status);
        Assert.Single(await scope.GetPasskeysAsync(userId));
    }

    [Fact]
    public async Task RunQueryAsync_PasskeyOnlyTwoFactorWithTwoPasskeys_DeletesOneAndKeepsTheLast()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        using var laptop = AccountSecurityTestScope.NewPasskey();
        using var phone = AccountSecurityTestScope.NewPasskey();
        var first = await scope.RegisterPasskeyAsync(userId, laptop, "Laptop");
        var second = await scope.RegisterPasskeyAsync(userId, phone, "Phone");
        await scope.EnableTwoFactorWithPasskeysAsync(userId);

        var deleted = await DeleteAsync(scope, userId, first.Passkey!.Id);
        var refused = await DeleteAsync(scope, userId, second.Passkey!.Id);

        Assert.Equal(PasskeyDeletionStatus.Deleted, deleted.Status);
        Assert.Equal(PasskeyDeletionStatus.LastSecondFactor, refused.Status);
        Assert.Equal("Phone", Assert.Single(await scope.GetPasskeysAsync(userId)).Name);
    }

    [Fact]
    public async Task RunQueryAsync_TwoFactorWithAnAuthenticatorApp_DeletesTheLastPasskey()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        await scope.EnableTotpAsync(userId);
        using var passkey = AccountSecurityTestScope.NewPasskey();
        var registered = await scope.RegisterPasskeyAsync(userId, passkey);

        var result = await DeleteAsync(scope, userId, registered.Passkey!.Id);

        Assert.Equal(PasskeyDeletionStatus.Deleted, result.Status);
        Assert.Empty(await scope.GetPasskeysAsync(userId));
    }

    [Fact]
    public async Task RunQueryAsync_AnotherUsersPasskey_ReturnsNoneAndKeepsIt()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var alice = await scope.SeedUserAsync("alice");
        var mallory = await scope.SeedUserAsync("mallory");
        using var passkey = AccountSecurityTestScope.NewPasskey();
        var registered = await scope.RegisterPasskeyAsync(alice, passkey);

        var result = await scope
            .GetHandler<DeletePasskeyQueryHandler>()
            .RunQueryAsync(new DeletePasskeyQuery { UserId = mallory, PasskeyId = registered.Passkey!.Id }, CancellationToken.None);

        Assert.True(result.IsNone);
        Assert.Single(await scope.GetPasskeysAsync(alice));
    }

    private static async Task<PasskeyDeletion> DeleteAsync(AccountSecurityTestScope scope, UserId userId, Guid passkeyId) =>
        QueryHandlerResultAssert.AssertSome(
            await scope
                .GetHandler<DeletePasskeyQueryHandler>()
                .RunQueryAsync(new DeletePasskeyQuery { UserId = userId, PasskeyId = passkeyId }, CancellationToken.None)
        );
}
