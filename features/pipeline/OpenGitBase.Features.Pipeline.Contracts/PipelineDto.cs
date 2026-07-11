using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Pipeline.Contracts;

public class PipelineDto : ModelBase<PipelineId, Guid>
{
    public string Name { get; set; } = string.Empty;
}
