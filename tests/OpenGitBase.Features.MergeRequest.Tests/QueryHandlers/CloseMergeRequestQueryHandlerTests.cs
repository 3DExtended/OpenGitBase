using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.MergeRequest.QueryHandlers;
using OpenGitBase.Features.MergeRequest.Tests.Testing;

namespace OpenGitBase.Features.MergeRequest.Tests.QueryHandlers;

public class CloseMergeRequestQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_ClosesOpenMergeRequest()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        await MergeRequestTestData.SeedAsync(context, status: MergeRequestStatus.Open);

        var handler = scope.GetHandler<CloseMergeRequestQueryHandler>();
        var result = await handler.RunQueryAsync(
            new CloseMergeRequestQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 1,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(MergeRequestStatus.Closed, result.Get().Status);
    }

    [Fact]
    public async Task RunQueryAsync_ClosesDraftMergeRequest()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        await MergeRequestTestData.SeedAsync(context, status: MergeRequestStatus.Draft);

        var handler = scope.GetHandler<CloseMergeRequestQueryHandler>();
        var result = await handler.RunQueryAsync(
            new CloseMergeRequestQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 1,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(MergeRequestStatus.Closed, result.Get().Status);
    }
}
