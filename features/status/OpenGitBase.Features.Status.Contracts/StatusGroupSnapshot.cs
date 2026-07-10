namespace OpenGitBase.Features.Status.Contracts;

public sealed class StatusGroupSnapshot
{
    public StatusComponentGroup Group { get; set; }

    public PublicHealthStatus Status { get; set; }

    public List<StatusInstanceSnapshot> Instances { get; set; } = [];
}
