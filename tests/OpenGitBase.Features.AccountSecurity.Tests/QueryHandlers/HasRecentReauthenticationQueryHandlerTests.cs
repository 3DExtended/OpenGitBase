using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.QueryHandlers;
using OpenGitBase.Features.AccountSecurity.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Tests.QueryHandlers;

public class HasRecentReauthenticationQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_PasswordAccount_NeedsAnExplicitReauthentication()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();

        // A session issued a moment ago is not enough while the account has a password to confirm.
        Assert.False(await IsRecentAsync(scope, userId, scope.Clock.UtcNow));
    }

    [Fact]
    public async Task RunQueryAsync_AccountWithoutPassword_CountsASessionYoungerThanTenMinutes()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync(withPassword: false);

        Assert.True(await IsRecentAsync(scope, userId, scope.Clock.UtcNow.AddMinutes(-9)));
        Assert.False(await IsRecentAsync(scope, userId, scope.Clock.UtcNow.AddMinutes(-11)));
    }

    [Fact]
    public async Task RunQueryAsync_ReauthenticationOfAnotherUser_DoesNotCount()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var alice = await scope.SeedUserAsync("alice");
        var bob = await scope.SeedUserAsync("bob");
        await scope
            .GetHandler<ReauthenticateQueryHandler>()
            .RunQueryAsync(
                new ReauthenticateQuery { UserId = alice, SessionId = "shared", Password = AccountSecurityTestScope.Password },
                CancellationToken.None
            );

        var result = await scope
            .GetHandler<HasRecentReauthenticationQueryHandler>()
            .RunQueryAsync(
                new HasRecentReauthenticationQuery { UserId = bob, SessionId = "shared", SessionIssuedAt = scope.Clock.UtcNow },
                CancellationToken.None
            );

        Assert.False(QueryHandlerResultAssert.AssertSome(result));
    }

    private static async Task<bool> IsRecentAsync(AccountSecurityTestScope scope, UserId userId, DateTimeOffset issuedAt) =>
        QueryHandlerResultAssert.AssertSome(
            await scope
                .GetHandler<HasRecentReauthenticationQueryHandler>()
                .RunQueryAsync(
                    new HasRecentReauthenticationQuery { UserId = userId, SessionId = "s", SessionIssuedAt = issuedAt },
                    CancellationToken.None
                )
        );
}
