using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Pipeline.Contracts;

public sealed class ListPipelineRunsQuery : IQuery<IReadOnlyList<PipelineRunDto>, ListPipelineRunsQuery>
{
    public Guid RepositoryId { get; set; }
}
