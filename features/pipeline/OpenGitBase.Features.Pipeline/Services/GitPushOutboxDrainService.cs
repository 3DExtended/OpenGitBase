using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.Services;

namespace OpenGitBase.Features.Pipeline.Services;

public sealed class GitPushOutboxDrainService
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IQueryProcessor _queryProcessor;
    private readonly IGitPushEventPublisher _publisher;
    private readonly ILogger<GitPushOutboxDrainService> _logger;

    public GitPushOutboxDrainService(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IQueryProcessor queryProcessor,
        IGitPushEventPublisher publisher,
        ILogger<GitPushOutboxDrainService> logger
    )
    {
        _contextFactory = contextFactory;
        _queryProcessor = queryProcessor;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<int> DrainPendingAsync(CancellationToken cancellationToken, int take = 20)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var pending = await context
            .Set<GitPushOutboxEntity>()
            .Where(entity => entity.Status == GitPushOutboxStatus.Pending)
            .OrderBy(entity => entity.Id)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        pending = pending.OrderBy(entity => entity.CreatedAt).ToList();

        var processed = 0;
        foreach (var row in pending)
        {
            try
            {
                await _queryProcessor
                    .RunQueryAsync(
                        new SchedulePipelineRunFromPushQuery
                        {
                            RepositoryId = row.RepositoryId,
                            Ref = row.Ref,
                            AfterSha = row.AfterSha,
                        },
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Scheduling outbox {OutboxId} failed; leaving Pending for retry.",
                    row.Id
                );
                continue;
            }

            try
            {
                await _publisher
                    .PublishAsync(row.RepositoryId, row.Ref, row.AfterSha, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(
                    ex,
                    "Kafka publish for outbox {OutboxId} failed; schedule already durable.",
                    row.Id
                );
            }

            row.Status = GitPushOutboxStatus.Processed;
            row.ProcessedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            processed++;
        }

        return processed;
    }
}
