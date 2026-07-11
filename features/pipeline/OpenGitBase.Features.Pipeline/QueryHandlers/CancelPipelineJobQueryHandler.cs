using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;

namespace OpenGitBase.Features.Pipeline.QueryHandlers;

public sealed class CancelPipelineJobQueryHandler : IQueryHandler<CancelPipelineJobQuery, PipelineJobDto>
{
    private readonly UpdatePipelineJobStatusQueryHandler _updateHandler;

    public CancelPipelineJobQueryHandler(UpdatePipelineJobStatusQueryHandler updateHandler)
    {
        _updateHandler = updateHandler;
    }

    public Task<Option<PipelineJobDto>> RunQueryAsync(
        CancelPipelineJobQuery query,
        CancellationToken cancellationToken
    ) =>
        _updateHandler.RunQueryAsync(
            new UpdatePipelineJobStatusQuery
            {
                JobId = query.JobId,
                Status = PipelineJobStatus.Cancelled,
                Message = "Cancelled by user.",
            },
            cancellationToken
        );
}
