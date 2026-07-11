using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Pipeline.Contracts;

namespace OpenGitBase.Features.Pipeline.Entities;

public sealed class JobStatusTransitionEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public Guid JobId { get; set; }

    public PipelineJobStatus FromStatus { get; set; }

    public PipelineJobStatus ToStatus { get; set; }

    public string Message { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
