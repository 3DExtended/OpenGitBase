using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.GitAccessToken;
using OpenGitBase.Features.GitAccessToken.Contracts;
using OpenGitBase.Features.GitAccessToken.QueryHandlers;
using OpenGitBase.Features.GitAccessToken.Tests.Testing;

namespace OpenGitBase.Features.GitAccessToken.Tests.QueryHandlers;

public class GetGitAccessTokenQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEntityExists_ReturnsMappedDto()
    {
        await using var scope = new GitAccessTokenHandlerTestScope();
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (id, _) = await GitAccessTokenTestData.SeedAsync(seedContext);

        var handler = scope.GetHandler<GetGitAccessTokenQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetGitAccessTokenQuery { ModelId = id },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            result,
            dto =>
            {
                Assert.Equal(id, dto.Id);
                Assert.Equal(GitAccessTokenTestData.SampleName, dto.Name);
            }
        );
    }

    [Fact]
    public async Task RunQueryAsync_WhenEntityMissing_ReturnsNone()
    {
        await using var scope = new GitAccessTokenHandlerTestScope();
        await scope.EnsureCreatedAsync();

        var handler = scope.GetHandler<GetGitAccessTokenQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetGitAccessTokenQuery { ModelId = GitAccessTokenId.From(Guid.NewGuid()) },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }
}
