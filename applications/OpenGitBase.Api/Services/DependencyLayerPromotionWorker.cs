using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Api.Services;

public sealed class DependencyLayerPromotionWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public DependencyLayerPromotionWorker(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessQueuedPromotionsAsync(stoppingToken).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
        }
    }

    private static string ComputeRecipeHash(string recipeKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(recipeKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static MemoryStream BuildLayerArtifactStream(string recipeKey)
    {
        var payload = Encoding.UTF8.GetBytes($"promoted-layer:{recipeKey}\n");
        return new MemoryStream(payload);
    }

    private async Task ProcessQueuedPromotionsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        var layerStore = scope.ServiceProvider.GetRequiredService<ILayerStoreClient>();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        var queued = await context
            .Set<DependencyPromotionRequestEntity>()
            .Where(entity => entity.Status == DependencyPromotionRequestStatus.Queued)
            .Take(5)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        queued = queued.OrderBy(entity => entity.CreatedAt).ToList();

        foreach (var promotion in queued)
        {
            promotion.Status = DependencyPromotionRequestStatus.Running;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var contentHash = ComputeRecipeHash(promotion.RecipeKey);
                await using var stream = BuildLayerArtifactStream(promotion.RecipeKey);
                await layerStore.PutBlobAsync(contentHash, stream, cancellationToken).ConfigureAwait(false);
                promotion.ContentHash = contentHash;
                promotion.LayerStoreObjectKey = contentHash;
                promotion.Status = DependencyPromotionRequestStatus.Completed;
                promotion.CompletedAt = DateTimeOffset.UtcNow;
            }
            catch
            {
                promotion.Status = DependencyPromotionRequestStatus.Failed;
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
