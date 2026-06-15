using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.StorageNode;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.QueryHandlers;
using OpenGitBase.Features.StorageNode.Tests.Testing;

namespace OpenGitBase.Features.StorageNode.Tests.QueryHandlers;

public class GetStorageNodeByNodeIdQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEntityExists_ReturnsMappedDto()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, StorageNodeMapsterConfig>(
            typeof(GetStorageNodeByNodeIdQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        await StorageNodeTestData.SeedAsync(seedContext);

        var handler = scope.GetHandler<GetStorageNodeByNodeIdQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetStorageNodeByNodeIdQuery { NodeId = StorageNodeTestData.SampleNodeId },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            result,
            dto => Assert.Equal(StorageNodeTestData.SampleNodeId, dto.NodeId)
        );
    }
}
