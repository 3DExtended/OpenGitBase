using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Status.Contracts;

public sealed class SetStatusIncidentBannerQuery
    : IQuery<PublicStatusIncidentDto, SetStatusIncidentBannerQuery>
{
    public string Message { get; set; } = string.Empty;

    public IncidentSeverity Severity { get; set; }

    public Guid AdminUserId { get; set; }
}
