﻿using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.StorageNode;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;
using OpenGitBase.Features.StorageNode.QueryHandlers;
using OpenGitBase.Features.StorageNode.Tests.Testing;

namespace OpenGitBase.Features.StorageNode.Tests.QueryHandlers;

public class UpdateStorageNodeQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEntityExists_UpdatesAndReturnsUnit()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, StorageNodeMapsterConfig>(
            typeof(UpdateStorageNodeQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (id, _) = await StorageNodeTestData.SeedAsync(seedContext);

        var handler = scope.GetHandler<UpdateStorageNodeQueryHandler>();
        var result = await handler.RunQueryAsync(
            new UpdateStorageNodeQuery
            {
                UpdatedModel = new StorageNodeDto
                {
                    Id = id,
                    NodeId = StorageNodeTestData.UpdatedNodeId,
                },
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertUnit(result);

        await using var verifyContext = await scope.CreateDbContextAsync();
        var entity = await verifyContext.Set<StorageNodeEntity>().FindAsync(id.Value);
        Assert.NotNull(entity);
        Assert.Equal(StorageNodeTestData.UpdatedNodeId, entity.NodeId);
    }

    [Fact]
    public async Task RunQueryAsync_WhenEntityMissing_ReturnsNone()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, StorageNodeMapsterConfig>(
            typeof(UpdateStorageNodeQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        var handler = scope.GetHandler<UpdateStorageNodeQueryHandler>();
        var result = await handler.RunQueryAsync(
            new UpdateStorageNodeQuery
            {
                UpdatedModel = new StorageNodeDto
                {
                    Id = StorageNodeId.From(Guid.NewGuid()),
                    NodeId = StorageNodeTestData.UpdatedNodeId,
                },
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }
}
