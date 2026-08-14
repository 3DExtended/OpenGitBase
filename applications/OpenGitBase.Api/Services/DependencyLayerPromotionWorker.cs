using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.Services;

namespace OpenGitBase.Api.Services;

public sealed class DependencyLayerPromotionWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DependencyLayerPromotionWorker> _logger;

    public DependencyLayerPromotionWorker(
        IServiceProvider serviceProvider,
        ILogger<DependencyLayerPromotionWorker> logger
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessQueuedPromotionsAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // An unhandled exception here would otherwise crash the whole API process.
                // Log and retry next tick instead.
                _logger.LogError(ex, "Dependency layer promotion cycle failed.");
            }

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

        // Serialize across API replicas: without this, two replicas can both pick up the
        // same queued promotion request and each create a duplicate pipeline run/job for it.
        if (
            !await PostgresAdvisoryLockService
                .TryAcquireAsync(
                    context,
                    BackgroundWorkerAdvisoryLocks.DependencyLayerPromotionWorker,
                    cancellationToken
                )
                .ConfigureAwait(false)
        )
        {
            return;
        }

        try
        {
            await ProcessQueuedPromotionsCoreAsync(context, publisher, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            await PostgresAdvisoryLockService
                .ReleaseAsync(
                    context,
                    BackgroundWorkerAdvisoryLocks.DependencyLayerPromotionWorker,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
    }

    private async Task ProcessQueuedPromotionsCoreAsync(
        OpenGitBaseDbContext context,
        IJobAvailableEventPublisher publisher,
        CancellationToken cancellationToken
    )
    {
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
