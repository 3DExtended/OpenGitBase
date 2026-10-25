using System.Text.Json;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.QueryHandlers;
using OpenGitBase.Features.AccountSecurity.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Tests.QueryHandlers;

public class StartPasskeyRegistrationQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_AsksForADiscoverableUserVerifiedPasskeyWithoutAttestation()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync("alice");

        var options = await StartAsync(scope, userId);

        var root = options.RootElement;
        Assert.Equal("localhost", root.GetProperty("rp").GetProperty("id").GetString());
        Assert.Equal("alice", root.GetProperty("user").GetProperty("name").GetString());
        Assert.Equal(
            userId.Value,
            new Guid(SoftwarePasskeyAuthenticator.FromBase64Url(root.GetProperty("user").GetProperty("id").GetString()!))
        );
        var selection = root.GetProperty("authenticatorSelection");
        Assert.Equal("required", selection.GetProperty("residentKey").GetString());
        Assert.Equal("required", selection.GetProperty("userVerification").GetString());
        Assert.Equal("none", root.GetProperty("attestation").GetString());
        Assert.Contains(
            root.GetProperty("pubKeyCredParams").EnumerateArray(),
            param => param.GetProperty("alg").GetInt32() == -7
        );
        Assert.Empty(root.GetProperty("excludeCredentials").EnumerateArray());
    }

    [Fact]
    public async Task RunQueryAsync_ExcludesTheUsersExistingPasskeys()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        using var passkey = AccountSecurityTestScope.NewPasskey();
        await scope.RegisterPasskeyAsync(userId, passkey);

        var options = await StartAsync(scope, userId);

        var excluded = Assert.Single(options.RootElement.GetProperty("excludeCredentials").EnumerateArray());
        Assert.Equal(SoftwarePasskeyAuthenticator.ToBase64Url(passkey.CredentialId), excluded.GetProperty("id").GetString());
    }

    [Fact]
    public async Task RunQueryAsync_UnknownUser_ReturnsNone()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();

        var result = await scope
            .GetHandler<StartPasskeyRegistrationQueryHandler>()
            .RunQueryAsync(new StartPasskeyRegistrationQuery { UserId = UserId.From(Guid.NewGuid()) }, CancellationToken.None);

        Assert.True(result.IsNone);
    }

    private static async Task<JsonDocument> StartAsync(AccountSecurityTestScope scope, UserId userId)
    {
        var result = await scope
            .GetHandler<StartPasskeyRegistrationQueryHandler>()
            .RunQueryAsync(new StartPasskeyRegistrationQuery { UserId = userId }, CancellationToken.None);
        return JsonDocument.Parse(QueryHandlerResultAssert.AssertSome(result).OptionsJson);
    }
}
