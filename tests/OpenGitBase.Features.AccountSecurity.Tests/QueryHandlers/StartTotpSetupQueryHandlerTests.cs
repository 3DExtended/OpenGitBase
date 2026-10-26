using OpenGitBase.Common.Services;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.QueryHandlers;
using OpenGitBase.Features.AccountSecurity.Tests.Testing;

namespace OpenGitBase.Features.AccountSecurity.Tests.QueryHandlers;

public class StartTotpSetupQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_ReturnsKeyUriWithFreshSecret_AndKeepsItEncryptedAsPending()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync("alice");

        var result = await scope
            .GetHandler<StartTotpSetupQueryHandler>()
            .RunQueryAsync(new StartTotpSetupQuery { UserId = userId, Issuer = "OpenGitBase" }, CancellationToken.None);

        var setup = QueryHandlerResultAssert.AssertSome(result);
        Assert.Matches("^[A-Z2-7]{32}$", setup.Secret);
        Assert.Equal(
            $"otpauth://totp/OpenGitBase:alice?secret={setup.Secret}&issuer=OpenGitBase",
            setup.OtpauthUri
        );

        var authenticator = await scope.GetAuthenticatorAsync(userId);
        Assert.NotNull(authenticator);
        Assert.Null(authenticator.SecretProtected);
        Assert.Null(authenticator.ConfirmedAt);
        Assert.NotEqual(setup.Secret, authenticator.PendingSecretProtected);
        Assert.Equal(
            setup.Secret,
            scope.GetService<IEmailProtectionService>().DecryptSecret(authenticator.PendingSecretProtected!)
        );
        Assert.Null((await scope.GetCredentialsAsync(userId)).TwoFactorEnabledAt);
    }

    [Fact]
    public async Task RunQueryAsync_SecondSetupReplacesThePendingSecret()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var handler = scope.GetHandler<StartTotpSetupQueryHandler>();
        var query = new StartTotpSetupQuery { UserId = userId, Issuer = "OpenGitBase" };

        var first = QueryHandlerResultAssert.AssertSome(await handler.RunQueryAsync(query, CancellationToken.None));
        var second = QueryHandlerResultAssert.AssertSome(await handler.RunQueryAsync(query, CancellationToken.None));

        Assert.NotEqual(first.Secret, second.Secret);
        var authenticator = await scope.GetAuthenticatorAsync(userId);
        Assert.Equal(
            second.Secret,
            scope.GetService<IEmailProtectionService>().DecryptSecret(authenticator!.PendingSecretProtected!)
        );
    }

    [Fact]
    public async Task RunQueryAsync_KeepsTheConfirmedSecretWhileANewSetupIsPending()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        var confirmedSecret = await scope.EnableTotpAsync(userId);

        await scope
            .GetHandler<StartTotpSetupQueryHandler>()
            .RunQueryAsync(new StartTotpSetupQuery { UserId = userId, Issuer = "OpenGitBase" }, CancellationToken.None);

        var authenticator = await scope.GetAuthenticatorAsync(userId);
        Assert.Equal(
            confirmedSecret,
            scope.GetService<IEmailProtectionService>().DecryptSecret(authenticator!.SecretProtected!)
        );
        Assert.NotNull(authenticator.PendingSecretProtected);
    }
}
