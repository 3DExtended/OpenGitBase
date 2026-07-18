using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Pipeline.Entities;

public sealed class GitPushOutboxEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public Guid RepositoryId { get; set; }

    public string Ref { get; set; } = string.Empty;

    public string AfterSha { get; set; } = string.Empty;

    public GitPushOutboxStatus Status { get; set; } = GitPushOutboxStatus.Pending;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }
}
