﻿using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.StorageNode;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.QueryHandlers;
using OpenGitBase.Features.StorageNode.Tests.Testing;

namespace OpenGitBase.Features.StorageNode.Tests.QueryHandlers;

public class GetStorageNodeQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEntityExists_ReturnsMappedDto()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, StorageNodeMapsterConfig>(
            typeof(GetStorageNodeQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (id, _) = await StorageNodeTestData.SeedAsync(seedContext);

        var handler = scope.GetHandler<GetStorageNodeQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetStorageNodeQuery { ModelId = id },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            result,
            dto =>
            {
                Assert.Equal(id, dto.Id);
                Assert.Equal(StorageNodeTestData.SampleNodeId, dto.NodeId);
            }
        );
    }

    [Fact]
    public async Task RunQueryAsync_WhenEntityMissing_ReturnsNone()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, StorageNodeMapsterConfig>(
            typeof(GetStorageNodeQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        var handler = scope.GetHandler<GetStorageNodeQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetStorageNodeQuery { ModelId = StorageNodeId.From(Guid.NewGuid()) },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }
}
