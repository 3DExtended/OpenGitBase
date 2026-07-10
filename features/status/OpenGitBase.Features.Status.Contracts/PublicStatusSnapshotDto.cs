namespace OpenGitBase.Features.Status.Contracts;

public sealed class PublicStatusSnapshotDto
{
    public PublicHealthStatus OverallStatus { get; set; }

    public DateTimeOffset CheckedAt { get; set; }

    public List<StatusGroupSnapshot> Groups { get; set; } = [];

    public PublicStatusIncidentDto? Incident { get; set; }
}
