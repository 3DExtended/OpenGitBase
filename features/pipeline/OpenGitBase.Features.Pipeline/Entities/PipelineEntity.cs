using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Pipeline.Entities;

public class PipelineEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
