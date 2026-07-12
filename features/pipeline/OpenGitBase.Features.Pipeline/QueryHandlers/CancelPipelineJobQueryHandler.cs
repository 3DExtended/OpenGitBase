using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Services;

namespace OpenGitBase.Features.Pipeline.QueryHandlers;

public sealed class CancelPipelineJobQueryHandler : IQueryHandler<CancelPipelineJobQuery, PipelineJobDto>
{
    private readonly UpdatePipelineJobStatusQueryHandler _updateHandler;
    private readonly GetPipelineJobQueryHandler _getJobHandler;
    private readonly IJobCancelledEventPublisher _cancelPublisher;

    public CancelPipelineJobQueryHandler(
        UpdatePipelineJobStatusQueryHandler updateHandler,
        GetPipelineJobQueryHandler getJobHandler,
        IJobCancelledEventPublisher cancelPublisher
    )
    {
        _updateHandler = updateHandler;
        _getJobHandler = getJobHandler;
        _cancelPublisher = cancelPublisher;
    }

    public async Task<Option<PipelineJobDto>> RunQueryAsync(
        CancelPipelineJobQuery query,
        CancellationToken cancellationToken
    )
    {
        var existing = await _getJobHandler.RunQueryAsync(
            new GetPipelineJobQuery { JobId = query.JobId },
            cancellationToken
        ).ConfigureAwait(false);
        if (existing.IsNone)
        {
            return Option<PipelineJobDto>.None;
        }

        var wasRunning = existing.Get().Status == PipelineJobStatus.Running;
        var result = await _updateHandler
            .RunQueryAsync(
                new UpdatePipelineJobStatusQuery
                {
                    JobId = query.JobId,
                    Status = PipelineJobStatus.Cancelled,
                    Message = "Cancelled by user.",
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (result.IsSome && wasRunning)
        {
            await _cancelPublisher
                .PublishCancelledAsync(query.JobId.Value, cancellationToken)
                .ConfigureAwait(false);
        }

        return result;
    }
}
