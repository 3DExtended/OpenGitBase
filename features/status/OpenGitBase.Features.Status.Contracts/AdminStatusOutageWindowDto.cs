namespace OpenGitBase.Features.Status.Contracts;

/// <summary>Admin DTO for an outage window, including operator-only fields.</summary>
public sealed class AdminStatusOutageWindowDto
{
    public Guid Id { get; set; }

    public OutageWindowScope Scope { get; set; }

    public StatusComponentGroup Group { get; set; }

    public string? InstanceId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public bool IsOpen { get; set; }

    public bool IsPartial { get; set; }

    public double? DurationMinutes { get; set; }

    public bool Suppressed { get; set; }

    public string? Annotation { get; set; }
}
