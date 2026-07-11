using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Pipeline;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.QueryHandlers;
using OpenGitBase.Features.Pipeline.Tests.Testing;

namespace OpenGitBase.Features.Pipeline.Tests.QueryHandlers;

public class CreatePipelineQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_PersistsEntity_ReturnsNewId()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, PipelineMapsterConfig>(
            typeof(CreatePipelineQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        var handler = scope.GetHandler<CreatePipelineQueryHandler>();
        var result = await handler.RunQueryAsync(
            new CreatePipelineQuery
            {
                ModelToCreate = new PipelineDto { Name = PipelineTestData.SampleName },
            },
            CancellationToken.None
        );

        var id = QueryHandlerResultAssert.AssertSome(result);
        QueryHandlerResultAssert.AssertIdentifierNonEmpty(id);

        await using var context = await scope.CreateDbContextAsync();
        var entity = await context.Set<PipelineEntity>().FindAsync(id.Value);
        Assert.NotNull(entity);
        Assert.Equal(id.Value, entity.Id);
        Assert.Equal(PipelineTestData.SampleName, entity.Name);
    }
}
