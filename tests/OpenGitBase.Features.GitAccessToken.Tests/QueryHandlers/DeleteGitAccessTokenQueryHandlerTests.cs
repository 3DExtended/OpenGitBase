using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.GitAccessToken;
using OpenGitBase.Features.GitAccessToken.Contracts;
using OpenGitBase.Features.GitAccessToken.QueryHandlers;
using OpenGitBase.Features.GitAccessToken.Tests.Testing;

namespace OpenGitBase.Features.GitAccessToken.Tests.QueryHandlers;

public class DeleteGitAccessTokenQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEntityExists_RevokesAndReturnsUnit()
    {
        await using var scope = new GitAccessTokenHandlerTestScope();
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (id, _) = await GitAccessTokenTestData.SeedAsync(seedContext);

        var handler = scope.GetHandler<DeleteGitAccessTokenQueryHandler>();
        var result = await handler.RunQueryAsync(
            new DeleteGitAccessTokenQuery { Id = id },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertUnit(result);

        await using var verifyContext = await scope.CreateDbContextAsync();
        var entity = await verifyContext
            .Set<OpenGitBase.Features.GitAccessToken.Entities.GitAccessTokenEntity>()
            .FindAsync(id.Value);
        Assert.NotNull(entity);
        Assert.NotNull(entity.RevokedAt);
    }

    [Fact]
    public async Task RunQueryAsync_WhenEntityMissing_ReturnsNone()
    {
        await using var scope = new GitAccessTokenHandlerTestScope();
        await scope.EnsureCreatedAsync();

        var handler = scope.GetHandler<DeleteGitAccessTokenQueryHandler>();
        var result = await handler.RunQueryAsync(
            new DeleteGitAccessTokenQuery { Id = GitAccessTokenId.From(Guid.NewGuid()) },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }
}
