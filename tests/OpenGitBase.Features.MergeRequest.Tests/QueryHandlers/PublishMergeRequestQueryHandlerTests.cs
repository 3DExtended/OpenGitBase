using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.MergeRequest.QueryHandlers;
using OpenGitBase.Features.MergeRequest.Tests.Testing;

namespace OpenGitBase.Features.MergeRequest.Tests.QueryHandlers;

public class PublishMergeRequestQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_PublishesDraftToApprovedWhenUnprotected()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        await MergeRequestTestData.SeedAsync(context, status: MergeRequestStatus.Draft);

        var handler = scope.GetHandler<PublishMergeRequestQueryHandler>();
        var result = await handler.RunQueryAsync(
            new PublishMergeRequestQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 1,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(MergeRequestStatus.Approved, result.Get().Status);
        Assert.False(result.Get().IsDraft);
    }

    [Fact]
    public async Task RunQueryAsync_RejectsNonDraft()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        await MergeRequestTestData.SeedAsync(context, status: MergeRequestStatus.Open);

        var handler = scope.GetHandler<PublishMergeRequestQueryHandler>();
        var result = await handler.RunQueryAsync(
            new PublishMergeRequestQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 1,
            },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }
}
