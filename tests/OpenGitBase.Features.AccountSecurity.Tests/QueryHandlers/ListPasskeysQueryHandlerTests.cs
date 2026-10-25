using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.QueryHandlers;
using OpenGitBase.Features.AccountSecurity.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Tests.QueryHandlers;

public class ListPasskeysQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_ListsOnlyTheUsersOwnPasskeysOldestFirst()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var alice = await scope.SeedUserAsync("alice");
        var bob = await scope.SeedUserAsync("bob");
        using var laptop = AccountSecurityTestScope.NewPasskey();
        using var phone = AccountSecurityTestScope.NewPasskey();
        using var bobs = AccountSecurityTestScope.NewPasskey();
        await scope.RegisterPasskeyAsync(alice, laptop, "Laptop");
        scope.Clock.Advance(TimeSpan.FromMinutes(1));
        await scope.RegisterPasskeyAsync(alice, phone, "Phone");
        await scope.RegisterPasskeyAsync(bob, bobs, "Bob's key");

        var list = await ListAsync(scope, alice);

        Assert.Equal(["Laptop", "Phone"], list.Passkeys.Select(p => p.Name));
    }

    [Fact]
    public async Task RunQueryAsync_NoPasskeys_ReturnsAnEmptyList()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();

        Assert.Empty((await ListAsync(scope, userId)).Passkeys);
    }

    private static async Task<PasskeyList> ListAsync(AccountSecurityTestScope scope, UserId userId) =>
        QueryHandlerResultAssert.AssertSome(
            await scope
                .GetHandler<ListPasskeysQueryHandler>()
                .RunQueryAsync(new ListPasskeysQuery { UserId = userId }, CancellationToken.None)
        );
}
