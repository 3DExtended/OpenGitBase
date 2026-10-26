using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.QueryHandlers;
using OpenGitBase.Features.AccountSecurity.Tests.Testing;

namespace OpenGitBase.Features.AccountSecurity.Tests.QueryHandlers;

public class RenamePasskeyQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_OwnPasskey_IsRenamed()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        using var passkey = AccountSecurityTestScope.NewPasskey();
        var registered = await scope.RegisterPasskeyAsync(userId, passkey, "Laptop");

        var result = await scope
            .GetHandler<RenamePasskeyQueryHandler>()
            .RunQueryAsync(
                new RenamePasskeyQuery { UserId = userId, PasskeyId = registered.Passkey!.Id, Name = " Work laptop " },
                CancellationToken.None
            );

        Assert.Equal("Work laptop", QueryHandlerResultAssert.AssertSome(result).Name);
        Assert.Equal("Work laptop", Assert.Single(await scope.GetPasskeysAsync(userId)).Name);
    }

    [Fact]
    public async Task RunQueryAsync_AnotherUsersPasskey_ReturnsNoneAndChangesNothing()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var alice = await scope.SeedUserAsync("alice");
        var mallory = await scope.SeedUserAsync("mallory");
        using var passkey = AccountSecurityTestScope.NewPasskey();
        var registered = await scope.RegisterPasskeyAsync(alice, passkey, "Laptop");

        var result = await scope
            .GetHandler<RenamePasskeyQueryHandler>()
            .RunQueryAsync(
                new RenamePasskeyQuery { UserId = mallory, PasskeyId = registered.Passkey!.Id, Name = "Mine now" },
                CancellationToken.None
            );

        Assert.True(result.IsNone);
        Assert.Equal("Laptop", Assert.Single(await scope.GetPasskeysAsync(alice)).Name);
    }
}
