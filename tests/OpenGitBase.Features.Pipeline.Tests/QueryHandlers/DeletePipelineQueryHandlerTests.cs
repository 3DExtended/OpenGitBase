using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Pipeline;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.QueryHandlers;
using OpenGitBase.Features.Pipeline.Tests.Testing;

namespace OpenGitBase.Features.Pipeline.Tests.QueryHandlers;

public class DeletePipelineQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEntityExists_DeletesAndReturnsUnit()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, PipelineMapsterConfig>(
            typeof(DeletePipelineQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (id, _) = await PipelineTestData.SeedAsync(seedContext);

        var handler = scope.GetHandler<DeletePipelineQueryHandler>();
        var result = await handler.RunQueryAsync(
            new DeletePipelineQuery { Id = id },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertUnit(result);

        await using var verifyContext = await scope.CreateDbContextAsync();
        Assert.Empty(verifyContext.Set<PipelineEntity>());
    }

    [Fact]
    public async Task RunQueryAsync_WhenEntityMissing_ReturnsNone()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, PipelineMapsterConfig>(
            typeof(DeletePipelineQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        var handler = scope.GetHandler<DeletePipelineQueryHandler>();
        var result = await handler.RunQueryAsync(
            new DeletePipelineQuery { Id = PipelineId.From(Guid.NewGuid()) },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }
}
