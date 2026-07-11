using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Pipeline.Contracts;

public sealed class GetPipelineJobLogsQuery : IQuery<IReadOnlyList<PipelineJobLogDto>, GetPipelineJobLogsQuery>
{
    public PipelineJobId JobId { get; set; } = PipelineJobId.From(Guid.NewGuid());
}
