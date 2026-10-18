using System.Text.Json;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.QueryHandlers;
using OpenGitBase.Features.AccountSecurity.Tests.Testing;

namespace OpenGitBase.Features.AccountSecurity.Tests.QueryHandlers;

public class StartPasskeyReauthenticationQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_AllowsOnlyTheUsersPasskeysWithUserVerification()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        using var passkey = AccountSecurityTestScope.NewPasskey();
        await scope.RegisterPasskeyAsync(userId, passkey);

        var result = await scope
            .GetHandler<StartPasskeyReauthenticationQueryHandler>()
            .RunQueryAsync(new StartPasskeyReauthenticationQuery { UserId = userId }, CancellationToken.None);

        using var options = JsonDocument.Parse(QueryHandlerResultAssert.AssertSome(result).OptionsJson);
        Assert.Equal("localhost", options.RootElement.GetProperty("rpId").GetString());
        Assert.Equal("required", options.RootElement.GetProperty("userVerification").GetString());
        var allowed = Assert.Single(options.RootElement.GetProperty("allowCredentials").EnumerateArray());
        Assert.Equal(SoftwarePasskeyAuthenticator.ToBase64Url(passkey.CredentialId), allowed.GetProperty("id").GetString());
    }

    [Fact]
    public async Task RunQueryAsync_NoPasskeys_ReturnsNone()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();

        var result = await scope
            .GetHandler<StartPasskeyReauthenticationQueryHandler>()
            .RunQueryAsync(new StartPasskeyReauthenticationQuery { UserId = userId }, CancellationToken.None);

        Assert.True(result.IsNone);
    }
}
