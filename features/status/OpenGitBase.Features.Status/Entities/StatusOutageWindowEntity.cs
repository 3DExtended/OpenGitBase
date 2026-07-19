using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Status.Contracts;

namespace OpenGitBase.Features.Status.Entities;

public class StatusOutageWindowEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public OutageWindowScope Scope { get; set; }

    public StatusComponentGroup ComponentGroup { get; set; }

    public string? InstanceId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset UnhealthySince { get; set; }

    public DateTimeOffset? BecamePublicAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public DateTimeOffset? LastNonUnhealthyAt { get; set; }

    public bool IsPartial { get; set; }

    public bool Suppressed { get; set; }

    public string? Annotation { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
