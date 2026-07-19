namespace OpenGitBase.Features.Status.Contracts;

/// <summary>Public DTO for an outage window on snapshot / windows API.</summary>
public sealed class PublicStatusOutageWindowDto
{
    public Guid Id { get; set; }

    public OutageWindowScope Scope { get; set; }

    public StatusComponentGroup Group { get; set; }

    public string? InstanceId { get; set; }

    /// <summary>Headline subject, e.g. "Message bus" or "Postgres".</summary>
    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public bool IsOpen { get; set; }

    public bool IsPartial { get; set; }

    public double? DurationMinutes { get; set; }

    public string? Annotation { get; set; }
}
