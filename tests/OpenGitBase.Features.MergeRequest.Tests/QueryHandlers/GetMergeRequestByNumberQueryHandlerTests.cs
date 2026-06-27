using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.MergeRequest.QueryHandlers;
using OpenGitBase.Features.MergeRequest.Tests.Testing;

namespace OpenGitBase.Features.MergeRequest.Tests.QueryHandlers;

public class GetMergeRequestByNumberQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_ReturnsMergeRequestByNumber()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        await MergeRequestTestData.SeedAsync(context, number: 7);

        var handler = scope.GetHandler<GetMergeRequestByNumberQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetMergeRequestByNumberQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 7,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(7, result.Get().Number);
        Assert.Equal(MergeRequestTestData.Title, result.Get().Title);
    }

    [Fact]
    public async Task RunQueryAsync_ReturnsNoneWhenMissing()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        var handler = scope.GetHandler<GetMergeRequestByNumberQueryHandler>();

        var result = await handler.RunQueryAsync(
            new GetMergeRequestByNumberQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 99,
            },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }
}
