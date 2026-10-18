using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.Entities;
using OpenGitBase.Features.AccountSecurity.QueryHandlers;
using OpenGitBase.Features.AccountSecurity.Tests.Testing;

namespace OpenGitBase.Features.AccountSecurity.Tests.QueryHandlers;

public class BeginTwoFactorSignInQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_TwoFactorOff_NeedsNoSecondStep()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();

        var result = await scope
            .GetHandler<BeginTwoFactorSignInQueryHandler>()
            .RunQueryAsync(new BeginTwoFactorSignInQuery { UserId = userId }, CancellationToken.None);

        var start = QueryHandlerResultAssert.AssertSome(result);
        Assert.False(start.Required);
        Assert.Null(start.ChallengeToken);
        await using var context = await scope.CreateDbContextAsync();
        Assert.Empty(await context.Set<AuthenticationChallengeEntity>().ToListAsync());
    }

    [Fact]
    public async Task RunQueryAsync_TwoFactorOn_IssuesAFiveMinuteChallengeStoredOnlyAsHash()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        await scope.EnableTotpAsync(userId);

        var result = await scope
            .GetHandler<BeginTwoFactorSignInQueryHandler>()
            .RunQueryAsync(new BeginTwoFactorSignInQuery { UserId = userId }, CancellationToken.None);

        var start = QueryHandlerResultAssert.AssertSome(result);
        Assert.True(start.Required);
        Assert.Equal(["totp"], start.Methods);
        Assert.False(string.IsNullOrWhiteSpace(start.ChallengeToken));

        await using var context = await scope.CreateDbContextAsync();
        var challenge = await context.Set<AuthenticationChallengeEntity>().SingleAsync();
        Assert.Equal(AuthenticationChallengePurpose.TwoFactorSignIn, challenge.Purpose);
        Assert.Equal(userId.Value, challenge.UserId);
        Assert.NotEqual(start.ChallengeToken, challenge.TokenHash);
        Assert.Equal(scope.Clock.UtcNow.AddMinutes(5), challenge.ExpiresAt);
    }

    [Fact]
    public async Task RunQueryAsync_RemovesTheUsersExpiredChallenges()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        await scope.EnableTotpAsync(userId);
        var handler = scope.GetHandler<BeginTwoFactorSignInQueryHandler>();
        await handler.RunQueryAsync(new BeginTwoFactorSignInQuery { UserId = userId }, CancellationToken.None);
        scope.Clock.Advance(TimeSpan.FromMinutes(6));

        await handler.RunQueryAsync(new BeginTwoFactorSignInQuery { UserId = userId }, CancellationToken.None);

        await using var context = await scope.CreateDbContextAsync();
        var challenge = await context.Set<AuthenticationChallengeEntity>().SingleAsync();
        Assert.Equal(scope.Clock.UtcNow.AddMinutes(5), challenge.ExpiresAt);
    }
}
