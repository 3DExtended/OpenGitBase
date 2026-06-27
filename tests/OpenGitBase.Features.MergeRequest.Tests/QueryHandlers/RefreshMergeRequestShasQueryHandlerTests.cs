using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.MergeRequest.QueryHandlers;
using OpenGitBase.Features.MergeRequest.Tests.Testing;

namespace OpenGitBase.Features.MergeRequest.Tests.QueryHandlers;

public class RefreshMergeRequestShasQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_UpdatesStoredShas()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        await MergeRequestTestData.SeedAsync(context);

        const string newSource = "cccccccccccccccccccccccccccccccccccccccc";
        const string newTarget = "dddddddddddddddddddddddddddddddddddddddd";

        var handler = scope.GetHandler<RefreshMergeRequestShasQueryHandler>();
        var result = await handler.RunQueryAsync(
            new RefreshMergeRequestShasQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 1,
                SourceHeadSha = newSource,
                TargetBaseSha = newTarget,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(newSource, result.Get().SourceHeadSha);
        Assert.Equal(newTarget, result.Get().TargetBaseSha);
    }
}
