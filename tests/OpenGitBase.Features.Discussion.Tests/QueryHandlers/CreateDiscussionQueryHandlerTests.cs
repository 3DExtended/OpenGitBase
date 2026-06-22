#pragma warning disable SA1402
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.Discussion.QueryHandlers;
using OpenGitBase.Features.Discussion.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Discussion.Tests.QueryHandlers;

public class CreateDiscussionQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_CreatesDiscussionWithSequentialNumber()
    {
        await using var scope = new DiscussionHandlerTestScope();
        await scope.EnsureCreatedAsync();
        var handler = scope.GetHandler<CreateDiscussionQueryHandler>();

        var first = await handler.RunQueryAsync(
            new CreateDiscussionQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                CreatorUserId = DiscussionTestData.CreatorUserId,
                Title = "First",
            },
            CancellationToken.None
        );

        var second = await handler.RunQueryAsync(
            new CreateDiscussionQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                CreatorUserId = DiscussionTestData.CreatorUserId,
                Title = "Second",
            },
            CancellationToken.None
        );

        Assert.True(first.IsSome);
        Assert.True(second.IsSome);
        Assert.Equal(1, first.Get().Number);
        Assert.Equal(2, second.Get().Number);
        Assert.Equal(DiscussionStatus.Open, first.Get().Status);
    }
}
