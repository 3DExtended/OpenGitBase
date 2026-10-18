using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.QueryHandlers;
using OpenGitBase.Features.AccountSecurity.Tests.Testing;

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

        QueryHandlerResultAssert.AssertUnit(result);
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
}
