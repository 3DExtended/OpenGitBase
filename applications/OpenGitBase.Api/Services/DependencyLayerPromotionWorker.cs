using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.Services;

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

    private async Task ProcessQueuedPromotionsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        var publisher = scope.ServiceProvider.GetRequiredService<IJobAvailableEventPublisher>();
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
            var jobId = Guid.NewGuid();
            var runId = Guid.NewGuid();
            context.Set<PipelineRunEntity>()
                .Add(
                    new PipelineRunEntity
                    {
                        Id = runId,
                        RepositoryId = Guid.Empty,
                        Ref = "refs/internal/layer-promotion",
                        AfterSha = promotion.RecipeKey,
                        Status = PipelineRunStatus.Running,
                        StageOrderJson = "[\"promotion\"]",
                        CreatedAt = DateTimeOffset.UtcNow,
                    }
                );
            context.Set<PipelineJobEntity>()
                .Add(
                    new PipelineJobEntity
                    {
                        Id = jobId,
                        RunId = runId,
                        Name = "__layer_promotion__",
                        Stage = "promotion",
                        RunsOn = "ogb-hosted",
                        Status = PipelineJobStatus.Queued,
                        Script = "echo layer-promotion-complete",
                        EnvironmentJson = System.Text.Json.JsonSerializer.Serialize(
                            new Dictionary<string, string>
                            {
                                ["OGB_LAYER_PROMOTION_RECIPE_KEY"] = promotion.RecipeKey,
                                ["OGB_LAYER_PROMOTION_REQUEST_ID"] = promotion.Id.ToString("D"),
                            }
                        ),
                        CpuLimit = 1,
                        MemoryMiB = 2048,
                        DiskGiB = 20,
                        TimeoutSeconds = 30 * 60,
                        CreatedAt = DateTimeOffset.UtcNow,
                    }
                );
            promotion.Status = DependencyPromotionRequestStatus.Running;
            promotion.LayerStoreObjectKey = jobId.ToString("D");
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await publisher.PublishAsync(jobId, cancellationToken).ConfigureAwait(false);
        }
    }
}
