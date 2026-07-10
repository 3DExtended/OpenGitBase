namespace OpenGitBase.Features.Status.Contracts;

public sealed class StatusInstanceSnapshot
{
    public string InstanceId { get; set; } = string.Empty;

    public PublicHealthStatus Status { get; set; }

    public DateTimeOffset LastCheckedAt { get; set; }

    public long? ResponseTimeMs { get; set; }

    public DateTimeOffset? LastSeenAt { get; set; }

    public string? Message { get; set; }
}
