using OpenGitBase.Cqrs;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.Discussion.Entities;
using OpenGitBase.Features.Discussion.QueryHandlers;
using OpenGitBase.Features.Discussion.Tests.Testing;

namespace OpenGitBase.Features.Discussion.Tests.QueryHandlers;

public class ListDiscussionsByRepositoryQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_PutsClosedDiscussionsAfterOpenAndEngaged()
    {
        await using var scope = new DiscussionHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        await DiscussionTestData.SeedRepositoryAsync(context);

        var baseTime = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        await SeedDiscussionAsync(
            context,
            number: 1,
            status: DiscussionStatus.Open,
            updatedAt: baseTime.AddHours(3)
        );
        await SeedDiscussionAsync(
            context,
            number: 2,
            status: DiscussionStatus.Engaged,
            updatedAt: baseTime.AddHours(2)
        );
        await SeedDiscussionAsync(
            context,
            number: 3,
            status: DiscussionStatus.Resolved,
            updatedAt: baseTime.AddHours(4)
        );
        await SeedDiscussionAsync(
            context,
            number: 4,
            status: DiscussionStatus.Dismissed,
            updatedAt: baseTime.AddHours(1)
        );

        var handler = scope.GetHandler<ListDiscussionsByRepositoryQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListDiscussionsByRepositoryQuery { RepositoryId = DiscussionTestData.RepositoryId },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(
            [1, 2, 3, 4],
            result.Get().Select(d => d.Number).ToArray()
        );
    }

    private static Task SeedDiscussionAsync(
        OpenGitBase.Common.Data.OpenGitBaseDbContext context,
        int number,
        DiscussionStatus status,
        DateTimeOffset updatedAt
    )
    {
        context.Set<DiscussionEntity>().Add(
            new DiscussionEntity
            {
                Id = Guid.NewGuid(),
                RepositoryId = DiscussionTestData.RepositoryId,
                Number = number,
                Title = $"Discussion {number}",
                Body = "Body",
                Status = (int)status,
                CreatorUserId = DiscussionTestData.CreatorUserId.Value,
                CreatedAt = updatedAt,
                UpdatedAt = updatedAt,
            }
        );
        return context.SaveChangesAsync();
    }
}
