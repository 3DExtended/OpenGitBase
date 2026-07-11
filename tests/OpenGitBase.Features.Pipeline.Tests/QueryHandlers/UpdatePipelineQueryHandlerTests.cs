using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Pipeline;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.QueryHandlers;
using OpenGitBase.Features.Pipeline.Tests.Testing;

namespace OpenGitBase.Features.Pipeline.Tests.QueryHandlers;

public class UpdatePipelineQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEntityExists_UpdatesAndReturnsUnit()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, PipelineMapsterConfig>(
            typeof(UpdatePipelineQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (id, _) = await PipelineTestData.SeedAsync(seedContext);

        var handler = scope.GetHandler<UpdatePipelineQueryHandler>();
        var result = await handler.RunQueryAsync(
            new UpdatePipelineQuery
            {
                UpdatedModel = new PipelineDto
                {
                    Id = id,
                    Name = PipelineTestData.UpdatedName,
                },
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertUnit(result);

        await using var verifyContext = await scope.CreateDbContextAsync();
        var entity = await verifyContext.Set<PipelineEntity>().FindAsync(id.Value);
        Assert.NotNull(entity);
        Assert.Equal(PipelineTestData.UpdatedName, entity.Name);
    }

    [Fact]
    public async Task RunQueryAsync_WhenEntityMissing_ReturnsNone()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, PipelineMapsterConfig>(
            typeof(UpdatePipelineQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        var handler = scope.GetHandler<UpdatePipelineQueryHandler>();
        var result = await handler.RunQueryAsync(
            new UpdatePipelineQuery
            {
                UpdatedModel = new PipelineDto
                {
                    Id = PipelineId.From(Guid.NewGuid()),
                    Name = PipelineTestData.UpdatedName,
                },
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }
}
