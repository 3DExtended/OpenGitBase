using OpenGitBase.Features.Status.Contracts;

namespace OpenGitBase.Features.Status.Services;

public sealed class OutageWindowRecord
{
    public Guid Id { get; set; }

    public OutageWindowScope Scope { get; set; }

    public StatusComponentGroup Group { get; set; }

    public string? InstanceId { get; set; }

    public DateTimeOffset UnhealthySince { get; set; }

    public DateTimeOffset? BecamePublicAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public DateTimeOffset? LastNonUnhealthyAt { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public bool IsPartial { get; set; }

    public bool Suppressed { get; set; }

    public string? Annotation { get; set; }
}
