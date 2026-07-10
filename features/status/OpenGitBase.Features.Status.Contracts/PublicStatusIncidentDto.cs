namespace OpenGitBase.Features.Status.Contracts;

public sealed class PublicStatusIncidentDto
{
    public string Message { get; set; } = string.Empty;

    public IncidentSeverity Severity { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
