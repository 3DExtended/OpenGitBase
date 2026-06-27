using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.MergeRequest.QueryHandlers;
using OpenGitBase.Features.MergeRequest.Tests.Testing;

namespace OpenGitBase.Features.MergeRequest.Tests.QueryHandlers;

public class UpdateMergeRequestMetadataQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_UpdatesTitleAndBody()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        await MergeRequestTestData.SeedAsync(context);

        var handler = scope.GetHandler<UpdateMergeRequestMetadataQueryHandler>();
        var result = await handler.RunQueryAsync(
            new UpdateMergeRequestMetadataQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 1,
                Title = MergeRequestTestData.UpdatedTitle,
                Body = "Updated body",
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(MergeRequestTestData.UpdatedTitle, result.Get().Title);
        Assert.Equal("Updated body", result.Get().Body);
    }
}
