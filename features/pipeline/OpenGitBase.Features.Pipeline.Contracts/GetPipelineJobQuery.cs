using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Pipeline.Contracts;

public sealed class GetPipelineJobQuery : IQuery<PipelineJobDto, GetPipelineJobQuery>
{
    public PipelineJobId JobId { get; set; } = PipelineJobId.From(Guid.NewGuid());
}
