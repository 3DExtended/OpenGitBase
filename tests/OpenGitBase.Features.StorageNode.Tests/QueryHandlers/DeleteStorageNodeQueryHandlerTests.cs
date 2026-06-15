using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.StorageNode;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;
using OpenGitBase.Features.StorageNode.QueryHandlers;
using OpenGitBase.Features.StorageNode.Tests.Testing;

namespace OpenGitBase.Features.StorageNode.Tests.QueryHandlers;

public class DeleteStorageNodeQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEntityExists_DeletesAndReturnsUnit()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, StorageNodeMapsterConfig>(
            typeof(DeleteStorageNodeQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (id, _) = await StorageNodeTestData.SeedAsync(seedContext);

        var handler = scope.GetHandler<DeleteStorageNodeQueryHandler>();
        var result = await handler.RunQueryAsync(
            new DeleteStorageNodeQuery { Id = id },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertUnit(result);

        await using var verifyContext = await scope.CreateDbContextAsync();
        Assert.Empty(verifyContext.Set<StorageNodeEntity>());
    }

    [Fact]
    public async Task RunQueryAsync_WhenEntityMissing_ReturnsNone()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, StorageNodeMapsterConfig>(
            typeof(DeleteStorageNodeQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        var handler = scope.GetHandler<DeleteStorageNodeQueryHandler>();
        var result = await handler.RunQueryAsync(
            new DeleteStorageNodeQuery { Id = StorageNodeId.From(Guid.NewGuid()) },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }
}
