using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Pipeline;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.QueryHandlers;
using OpenGitBase.Features.Pipeline.Tests.Testing;

namespace OpenGitBase.Features.Pipeline.Tests.QueryHandlers;

public class ListPipelineQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEmpty_ReturnsEmptyList()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, PipelineMapsterConfig>(
            typeof(ListPipelineQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        var handler = scope.GetHandler<ListPipelineQueryHandler>();
        var result = await handler.RunQueryAsync(new ListPipelineQuery(), CancellationToken.None);

        QueryHandlerResultAssert.AssertSome(result, list => Assert.Empty(list));
    }

    [Fact]
    public async Task RunQueryAsync_WhenSeeded_ReturnsAllItems()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, PipelineMapsterConfig>(
            typeof(ListPipelineQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (id, _) = await PipelineTestData.SeedAsync(seedContext);

        var handler = scope.GetHandler<ListPipelineQueryHandler>();
        var result = await handler.RunQueryAsync(new ListPipelineQuery(), CancellationToken.None);

        QueryHandlerResultAssert.AssertSome(
            result,
            list =>
            {
                var item = Assert.Single(list);
                Assert.Equal(id, item.Id);
                Assert.Equal(PipelineTestData.SampleName, item.Name);
            }
        );
    }

    [Fact]
    public async Task RunQueryAsync_WhenPartialIdSet_ReturnsNone()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, PipelineMapsterConfig>(
            typeof(ListPipelineQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (id, _) = await PipelineTestData.SeedAsync(seedContext);

        var handler = scope.GetHandler<ListPipelineQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListPipelineQuery
            {
                Ids = new[] { id, PipelineId.From(Guid.NewGuid()) },
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }
}
