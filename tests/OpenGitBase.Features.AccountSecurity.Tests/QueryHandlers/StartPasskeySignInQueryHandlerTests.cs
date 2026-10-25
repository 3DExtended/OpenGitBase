using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.Entities;
using OpenGitBase.Features.AccountSecurity.QueryHandlers;
using OpenGitBase.Features.AccountSecurity.Tests.Testing;

namespace OpenGitBase.Features.AccountSecurity.Tests.QueryHandlers;

public class StartPasskeySignInQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_ReturnsOptionsForAnyDiscoverablePasskeyWithUserVerification()
    {
        await using var scope = await AccountSecurityTestScope.CreateAsync();
        var userId = await scope.SeedUserAsync();
        using var passkey = AccountSecurityTestScope.NewPasskey();
        await scope.RegisterPasskeyAsync(userId, passkey);

        var result = QueryHandlerResultAssert.AssertSome(
            await scope
                .GetHandler<StartPasskeySignInQueryHandler>()
                .RunQueryAsync(new StartPasskeySignInQuery(), CancellationToken.None)
        );

        using var options = JsonDocument.Parse(result.OptionsJson);
        Assert.Equal("localhost", options.RootElement.GetProperty("rpId").GetString());
        Assert.Equal("required", options.RootElement.GetProperty("userVerification").GetString());
        Assert.False(
            options.RootElement.TryGetProperty("allowCredentials", out var allowed) && allowed.GetArrayLength() > 0,
            "No credential ids may be listed: they would name the account."
        );
        await using var context = await scope.CreateDbContextAsync();
        var challenge = Assert.Single(
            await context
                .Set<AuthenticationChallengeEntity>()
                .Where(c => c.Purpose == AuthenticationChallengePurpose.PasskeySignIn)
                .ToListAsync()
        );
        Assert.Null(challenge.UserId);
    }
}
