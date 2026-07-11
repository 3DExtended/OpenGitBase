using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Pipeline.Contracts;

namespace OpenGitBase.Features.Pipeline.Entities;

public sealed class PipelineRunEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public Guid RepositoryId { get; set; }

    public string Ref { get; set; } = string.Empty;

    public string AfterSha { get; set; } = string.Empty;

    public PipelineRunStatus Status { get; set; } = PipelineRunStatus.Queued;

    public string StageOrderJson { get; set; } = "[]";

    public DateTimeOffset CreatedAt { get; set; }
}
