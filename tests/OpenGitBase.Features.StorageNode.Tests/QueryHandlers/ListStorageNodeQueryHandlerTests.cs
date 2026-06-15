﻿using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.StorageNode;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.QueryHandlers;
using OpenGitBase.Features.StorageNode.Tests.Testing;

namespace OpenGitBase.Features.StorageNode.Tests.QueryHandlers;

public class ListStorageNodeQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEmpty_ReturnsEmptyList()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, StorageNodeMapsterConfig>(
            typeof(ListStorageNodeQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        var handler = scope.GetHandler<ListStorageNodeQueryHandler>();
        var result = await handler.RunQueryAsync(new ListStorageNodeQuery(), CancellationToken.None);

        QueryHandlerResultAssert.AssertSome(result, list => Assert.Empty(list));
    }

    [Fact]
    public async Task RunQueryAsync_WhenSeeded_ReturnsAllItems()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, StorageNodeMapsterConfig>(
            typeof(ListStorageNodeQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (id, _) = await StorageNodeTestData.SeedAsync(seedContext);

        var handler = scope.GetHandler<ListStorageNodeQueryHandler>();
        var result = await handler.RunQueryAsync(new ListStorageNodeQuery(), CancellationToken.None);

        QueryHandlerResultAssert.AssertSome(
            result,
            list =>
            {
                var item = Assert.Single(list);
                Assert.Equal(id, item.Id);
                Assert.Equal(StorageNodeTestData.SampleNodeId, item.NodeId);
            }
        );
    }

    [Fact]
    public async Task RunQueryAsync_WhenPartialIdSet_ReturnsNone()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, StorageNodeMapsterConfig>(
            typeof(ListStorageNodeQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (id, _) = await StorageNodeTestData.SeedAsync(seedContext);

        var handler = scope.GetHandler<ListStorageNodeQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListStorageNodeQuery
            {
                Ids = new[] { id, StorageNodeId.From(Guid.NewGuid()) },
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }
}
