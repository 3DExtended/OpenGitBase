using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.Services;

namespace OpenGitBase.Features.Pipeline.QueryHandlers;

/// <summary>
/// Republish Kafka wake signals for queued / cancelled jobs after a Kafka wipe or restart.
/// Jobs remain source of truth in Postgres; this restores low-latency platform-agent wake.
/// </summary>
public sealed class RepublishKafkaJobWakesQueryHandler
    : IQueryHandler<RepublishKafkaJobWakesQuery, RepublishKafkaJobWakesResult>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IJobAvailableEventPublisher _availablePublisher;
    private readonly IJobCancelledEventPublisher _cancelledPublisher;

    public RepublishKafkaJobWakesQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IJobAvailableEventPublisher availablePublisher,
        IJobCancelledEventPublisher cancelledPublisher
    )
    {
        _contextFactory = contextFactory;
        _availablePublisher = availablePublisher;
        _cancelledPublisher = cancelledPublisher;
    }

    public async Task<Option<RepublishKafkaJobWakesResult>> RunQueryAsync(
        RepublishKafkaJobWakesQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var queuedIds = await context
            .Set<PipelineJobEntity>()
            .Where(job => job.Status == PipelineJobStatus.Queued)
            .Select(job => job.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var cancelledCutoff = DateTimeOffset.UtcNow.AddHours(-1);
        var cancelledJobs = await context
            .Set<PipelineJobEntity>()
            .Where(job => job.Status == PipelineJobStatus.Cancelled)
            .Select(job => new { job.Id, job.FinishedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var cancelledIds = cancelledJobs
            .Where(job => job.FinishedAt is null || job.FinishedAt > cancelledCutoff)
            .Select(job => job.Id)
            .ToList();

        var available = 0;
        foreach (var jobId in queuedIds)
        {
            try
            {
                await _availablePublisher.PublishAsync(jobId, cancellationToken).ConfigureAwait(false);
                available++;
            }
            catch
            {
                // best-effort
            }
        }

        var cancelled = 0;
        foreach (var jobId in cancelledIds)
        {
            try
            {
                await _cancelledPublisher
                    .PublishCancelledAsync(jobId, cancellationToken)
                    .ConfigureAwait(false);
                cancelled++;
            }
            catch
            {
                // best-effort
            }
        }

        return Option.From(
            new RepublishKafkaJobWakesResult
            {
                QueuedJobWakes = available,
                CancelledJobWakes = cancelled,
            }
        );
    }
}
