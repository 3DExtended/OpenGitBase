using OpenGitBase.Common.Services;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.QueryHandlers;
using OpenGitBase.Features.AccountSecurity.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Tests.QueryHandlers;

public class RegenerateRecoveryCodesQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_TwoFactorOn_ReplacesTheWholeSet()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var oldCodes = await scope.EnableTotpWithRecoveryCodesAsync(userId);

        var result = await RegenerateAsync(scope, userId);

        var newCodes = QueryHandlerResultAssert.AssertSome(result).Codes;
        Assert.Equal(10, newCodes.Distinct().Count());
        Assert.Empty(newCodes.Intersect(oldCodes));
        var rows = await scope.GetRecoveryCodesAsync(userId);
        Assert.Equal(10, rows.Count);
        var hasher = scope.GetService<IPasswordHasherService>();
        Assert.DoesNotContain(
            rows,
            row => oldCodes.Any(code => hasher.VerifyPassword(row.CodeHash, code.Replace("-", string.Empty).ToUpperInvariant()))
        );
    }

    [Fact]
    public async Task RunQueryAsync_OldCodesNoLongerCompleteTheSignIn()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var oldCodes = await scope.EnableTotpWithRecoveryCodesAsync(userId);
        var newCodes = QueryHandlerResultAssert.AssertSome(await RegenerateAsync(scope, userId)).Codes;

        Assert.Equal(TwoFactorSignInStatus.InvalidCode, await SignInWithRecoveryCodeAsync(scope, userId, oldCodes[0]));
        Assert.Equal(TwoFactorSignInStatus.Succeeded, await SignInWithRecoveryCodeAsync(scope, userId, newCodes[0]));
    }

    [Fact]
    public async Task RunQueryAsync_AfterUsingCodes_StartsAgainWithTenUnused()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var oldCodes = await scope.EnableTotpWithRecoveryCodesAsync(userId);
        await SignInWithRecoveryCodeAsync(scope, userId, oldCodes[0]);

        await RegenerateAsync(scope, userId);

        var rows = await scope.GetRecoveryCodesAsync(userId);
        Assert.Equal(10, rows.Count);
        Assert.All(rows, row => Assert.Null(row.UsedAt));
    }

    [Fact]
    public async Task RunQueryAsync_TwoFactorOff_ReturnsNoneAndCreatesNoCodes()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();

        var result = await RegenerateAsync(scope, userId);

        Assert.True(result.IsNone);
        Assert.Empty(await scope.GetRecoveryCodesAsync(userId));
    }

    [Fact]
    public async Task RunQueryAsync_UnknownUser_ReturnsNone()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();

        var result = await RegenerateAsync(scope, UserId.From(Guid.NewGuid()));

        Assert.True(result.IsNone);
    }

    private static Task<Option<RecoveryCodeSet>> RegenerateAsync(AccountSecurityTestScope scope, UserId userId) =>
        scope
            .GetHandler<RegenerateRecoveryCodesQueryHandler>()
            .RunQueryAsync(new RegenerateRecoveryCodesQuery { UserId = userId }, CancellationToken.None);

    private static async Task<TwoFactorSignInStatus> SignInWithRecoveryCodeAsync(
        AccountSecurityTestScope scope,
        UserId userId,
        string recoveryCode
    )
    {
        var start = await scope
            .GetHandler<BeginTwoFactorSignInQueryHandler>()
            .RunQueryAsync(new BeginTwoFactorSignInQuery { UserId = userId }, CancellationToken.None);
        var result = await scope
            .GetHandler<CompleteTwoFactorSignInQueryHandler>()
            .RunQueryAsync(
                new CompleteTwoFactorSignInQuery
                {
                    ChallengeToken = start.Get().ChallengeToken!,
                    RecoveryCode = recoveryCode,
                },
                CancellationToken.None
            );
        return result.Get().Status;
    }
}
