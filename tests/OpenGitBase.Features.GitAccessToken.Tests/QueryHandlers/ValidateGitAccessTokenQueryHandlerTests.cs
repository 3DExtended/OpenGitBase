using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.GitAccessToken;
using OpenGitBase.Features.GitAccessToken.Contracts;
using OpenGitBase.Features.GitAccessToken.QueryHandlers;
using OpenGitBase.Features.GitAccessToken.Tests.Testing;

namespace OpenGitBase.Features.GitAccessToken.Tests.QueryHandlers;

public class ValidateGitAccessTokenQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenTokenValid_ReturnsUserAndScope()
    {
        await using var scope = new GitAccessTokenHandlerTestScope();
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        await GitAccessTokenTestData.SeedAsync(seedContext, scope: GitAccessTokenScopes.Read);

        var handler = scope.GetHandler<ValidateGitAccessTokenQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ValidateGitAccessTokenQuery { Token = GitAccessTokenTestData.SampleToken },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            result,
            value =>
            {
                Assert.Equal(GitAccessTokenTestData.OwnerUserId, value.UserId);
                Assert.Equal(GitAccessTokenScopes.Read, value.Scope);
            }
        );
    }

    [Fact]
    public async Task RunQueryAsync_WhenTokenRevoked_ReturnsNone()
    {
        await using var scope = new GitAccessTokenHandlerTestScope();
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        await GitAccessTokenTestData.SeedAsync(
            seedContext,
            revokedAt: DateTimeOffset.UtcNow.AddMinutes(-1)
        );

        var handler = scope.GetHandler<ValidateGitAccessTokenQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ValidateGitAccessTokenQuery { Token = GitAccessTokenTestData.SampleToken },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }

    [Fact]
    public async Task RunQueryAsync_WhenTokenExpired_ReturnsNone()
    {
        await using var scope = new GitAccessTokenHandlerTestScope();
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        await GitAccessTokenTestData.SeedAsync(
            seedContext,
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(-5)
        );

        var handler = scope.GetHandler<ValidateGitAccessTokenQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ValidateGitAccessTokenQuery { Token = GitAccessTokenTestData.SampleToken },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }
}
