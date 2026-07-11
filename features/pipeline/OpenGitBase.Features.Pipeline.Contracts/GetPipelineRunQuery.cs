using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Pipeline.Contracts;

public sealed class GetPipelineRunQuery : IQuery<PipelineRunDto, GetPipelineRunQuery>
{
    public PipelineRunId RunId { get; set; } = PipelineRunId.From(Guid.NewGuid());
}
