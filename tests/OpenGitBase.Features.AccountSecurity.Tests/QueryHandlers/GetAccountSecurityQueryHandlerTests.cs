using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.QueryHandlers;
using OpenGitBase.Features.AccountSecurity.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Tests.QueryHandlers;

public class GetAccountSecurityQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_NewAccount_HasNothingSetUp()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();

        var status = await GetAsync(scope, userId);

        Assert.False(status.TwoFactorEnabled);
        Assert.False(status.TotpConfigured);
        Assert.True(status.HasPassword);
        Assert.Equal(0, status.RecoveryCodesRemaining);
    }

    [Fact]
    public async Task RunQueryAsync_PendingSetup_IsNotConfiguredYet()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        await scope
            .GetHandler<StartTotpSetupQueryHandler>()
            .RunQueryAsync(new StartTotpSetupQuery { UserId = userId, Issuer = "OpenGitBase" }, CancellationToken.None);

        var status = await GetAsync(scope, userId);

        Assert.False(status.TwoFactorEnabled);
        Assert.False(status.TotpConfigured);
    }

    [Fact]
    public async Task RunQueryAsync_ReportsTwoFactorWithAuthenticatorApp()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync(withPassword: false);
        await scope.EnableTotpAsync(userId);

        var status = await GetAsync(scope, userId);

        Assert.True(status.TwoFactorEnabled);
        Assert.True(status.TotpConfigured);
        Assert.False(status.HasPassword);
    }

    [Fact]
    public async Task RunQueryAsync_CountsOnlyUnusedRecoveryCodes()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var codes = await scope.EnableTotpWithRecoveryCodesAsync(userId);
        Assert.Equal(10, (await GetAsync(scope, userId)).RecoveryCodesRemaining);
        await scope
            .GetHandler<ReauthenticateQueryHandler>()
            .RunQueryAsync(
                new ReauthenticateQuery
                {
                    UserId = userId,
                    SessionId = "session",
                    Password = AccountSecurityTestScope.Password,
                    RecoveryCode = codes[0],
                },
                CancellationToken.None
            );

        var status = await GetAsync(scope, userId);

        Assert.Equal(9, status.RecoveryCodesRemaining);
    }

    private static async Task<AccountSecurityStatus> GetAsync(AccountSecurityTestScope scope, UserId userId) =>
        QueryHandlerResultAssert.AssertSome(
            await scope
                .GetHandler<GetAccountSecurityQueryHandler>()
                .RunQueryAsync(new GetAccountSecurityQuery { UserId = userId }, CancellationToken.None)
        );
}
