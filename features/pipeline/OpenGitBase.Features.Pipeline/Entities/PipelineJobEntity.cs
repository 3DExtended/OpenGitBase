using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Pipeline.Contracts;

namespace OpenGitBase.Features.Pipeline.Entities;

public sealed class PipelineJobEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public Guid RunId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Stage { get; set; } = string.Empty;

    public string RunsOn { get; set; } = string.Empty;

    public PipelineJobStatus Status { get; set; } = PipelineJobStatus.Queued;

    public string Script { get; set; } = string.Empty;

    public string ResolvedSpecJson { get; set; } = "{}";

    public Guid? ClaimedByComputeNodeId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }
}
