﻿using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.StorageNode;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;
using OpenGitBase.Features.StorageNode.QueryHandlers;
using OpenGitBase.Features.StorageNode.Tests.Testing;

namespace OpenGitBase.Features.StorageNode.Tests.QueryHandlers;

public class CreateStorageNodeQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_PersistsEntity_ReturnsNewId()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, StorageNodeMapsterConfig>(
            typeof(CreateStorageNodeQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        var handler = scope.GetHandler<CreateStorageNodeQueryHandler>();
        var result = await handler.RunQueryAsync(
            new CreateStorageNodeQuery
            {
                ModelToCreate = new StorageNodeDto
                {
                    NodeId = StorageNodeTestData.SampleNodeId,
                    InternalHost = StorageNodeTestData.SampleNodeId,
                    InternalHttpPort = 8081,
                    IsHealthy = true,
                    RegisteredAt = DateTimeOffset.UtcNow,
                },
            },
            CancellationToken.None
        );

        var id = QueryHandlerResultAssert.AssertSome(result);
        QueryHandlerResultAssert.AssertIdentifierNonEmpty(id);

        await using var context = await scope.CreateDbContextAsync();
        var entity = await context.Set<StorageNodeEntity>().FindAsync(id.Value);
        Assert.NotNull(entity);
        Assert.Equal(id.Value, entity.Id);
        Assert.Equal(StorageNodeTestData.SampleNodeId, entity.NodeId);
    }
}
