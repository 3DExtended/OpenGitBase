using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.QueryHandlers;
using OpenGitBase.Features.AccountSecurity.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Tests.QueryHandlers;

public class DisableTwoFactorQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_RemovesTheAuthenticatorAndRevokesSessions()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        await scope.EnableTotpAsync(userId);
        scope.Clock.Advance(TimeSpan.FromMinutes(1));

        var result = await DisableAsync(scope, userId);

        QueryHandlerResultAssert.AssertSome(result);
        Assert.Null(await scope.GetAuthenticatorAsync(userId));
        var credentials = await scope.GetCredentialsAsync(userId);
        Assert.Null(credentials.TwoFactorEnabledAt);
        Assert.Equal(scope.Clock.UtcNow, credentials.SessionsValidAfter);
    }

    [Fact]
    public async Task RunQueryAsync_DeletesAllRecoveryCodes()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        await scope.EnableTotpWithRecoveryCodesAsync(userId);

        await DisableAsync(scope, userId);

        Assert.Empty(await scope.GetRecoveryCodesAsync(userId));
    }

    [Fact]
    public async Task RunQueryAsync_AfterDisabling_SignInNeedsNoSecondStep()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        await scope.EnableTotpAsync(userId);

        await DisableAsync(scope, userId);

        var start = await scope
            .GetHandler<BeginTwoFactorSignInQueryHandler>()
            .RunQueryAsync(new BeginTwoFactorSignInQuery { UserId = userId }, CancellationToken.None);
        Assert.False(QueryHandlerResultAssert.AssertSome(start).Required);
    }

    [Fact]
    public async Task RunQueryAsync_UnknownUser_ReturnsNone()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();

        var result = await DisableAsync(scope, UserId.From(Guid.NewGuid()));

        Assert.True(result.IsNone);
    }

    private static Task<OpenGitBase.Cqrs.Option<OpenGitBase.Cqrs.Unit>> DisableAsync(
        AccountSecurityTestScope scope,
        UserId userId
    ) =>
        scope
            .GetHandler<DisableTwoFactorQueryHandler>()
            .RunQueryAsync(new DisableTwoFactorQuery { UserId = userId }, CancellationToken.None);
}
