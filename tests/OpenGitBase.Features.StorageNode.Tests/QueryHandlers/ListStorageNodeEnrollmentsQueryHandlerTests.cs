using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.StorageNode;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;
using OpenGitBase.Features.StorageNode.QueryHandlers;

namespace OpenGitBase.Features.StorageNode.Tests.QueryHandlers;

public class ListStorageNodeEnrollmentsQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_ReturnsEnrollments()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, StorageNodeMapsterConfig>(
            typeof(ListStorageNodeEnrollmentsQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        seedContext
            .Set<StorageNodeEnrollmentEntity>()
            .Add(
                new StorageNodeEnrollmentEntity
                {
                    Id = Guid.NewGuid(),
                    NodeId = "storage-1",
                    EnrollmentTokenHash = "hash",
                    CreatedByUserId = Guid.NewGuid(),
                    CreatedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
                }
            );
        await seedContext.SaveChangesAsync();

        var handler = scope.GetHandler<ListStorageNodeEnrollmentsQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListStorageNodeEnrollmentsQuery(),
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(result, list => Assert.Single(list));
    }
}
