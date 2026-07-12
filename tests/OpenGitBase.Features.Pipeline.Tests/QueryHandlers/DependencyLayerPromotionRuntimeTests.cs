using Microsoft.EntityFrameworkCore;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.QueryHandlers;

namespace OpenGitBase.Features.Pipeline.Tests.QueryHandlers;

public class DependencyLayerPromotionRuntimeTests
{
    [Fact]
    public async Task ResolvePromotedLayer_ReturnsArtifactAfterPromotionCompletes()
    {
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        await using var context = await scope.ContextFactory.CreateDbContextAsync();
        var recipeKey = "linux:apt:docker";
        context.Set<DependencyPromotionRequestEntity>()
            .Add(
                new DependencyPromotionRequestEntity
                {
                    Id = Guid.NewGuid(),
                    RecipeKey = recipeKey,
                    RequestedByUserId = Guid.NewGuid(),
                    Status = DependencyPromotionRequestStatus.Completed,
                    ContentHash = "abc123",
                    LayerStoreObjectKey = "abc123",
                    CreatedAt = DateTimeOffset.UtcNow,
                    CompletedAt = DateTimeOffset.UtcNow,
                }
            );
        await context.SaveChangesAsync();

        var handler = new ResolvePromotedDependencyLayerQueryHandler(scope.ContextFactory);
        var result = await handler.RunQueryAsync(
            new ResolvePromotedDependencyLayerQuery { RecipeKey = recipeKey },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal("abc123", result.Get().ContentHash);
    }
}
