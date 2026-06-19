using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.GitAccessToken;
using OpenGitBase.Features.GitAccessToken.Contracts;
using OpenGitBase.Features.GitAccessToken.QueryHandlers;
using OpenGitBase.Features.GitAccessToken.Tests.Testing;

namespace OpenGitBase.Features.GitAccessToken.Tests.QueryHandlers;

public class ListGitAccessTokenQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEmpty_ReturnsEmptyList()
    {
        await using var scope = new GitAccessTokenHandlerTestScope();
        await scope.EnsureCreatedAsync();

        var handler = scope.GetHandler<ListGitAccessTokenQueryHandler>();
        var result = await handler.RunQueryAsync(new ListGitAccessTokenQuery(), CancellationToken.None);

        QueryHandlerResultAssert.AssertSome(result, list => Assert.Empty(list));
    }

    [Fact]
    public async Task RunQueryAsync_WhenSeeded_ReturnsAllItems()
    {
        await using var scope = new GitAccessTokenHandlerTestScope();
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (id, _) = await GitAccessTokenTestData.SeedAsync(seedContext);

        var handler = scope.GetHandler<ListGitAccessTokenQueryHandler>();
        var result = await handler.RunQueryAsync(new ListGitAccessTokenQuery(), CancellationToken.None);

        QueryHandlerResultAssert.AssertSome(
            result,
            list =>
            {
                var item = Assert.Single(list);
                Assert.Equal(id, item.Id);
                Assert.Equal(GitAccessTokenTestData.SampleName, item.Name);
            }
        );
    }

    [Fact]
    public async Task RunQueryAsync_WhenPartialIdSet_ReturnsNone()
    {
        await using var scope = new GitAccessTokenHandlerTestScope();
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (id, _) = await GitAccessTokenTestData.SeedAsync(seedContext);

        var handler = scope.GetHandler<ListGitAccessTokenQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListGitAccessTokenQuery
            {
                Ids = new[] { id, GitAccessTokenId.From(Guid.NewGuid()) },
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }
}
