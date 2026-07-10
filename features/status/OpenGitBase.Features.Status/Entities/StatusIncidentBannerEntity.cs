using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Status.Contracts;

namespace OpenGitBase.Features.Status.Entities;

public class StatusIncidentBannerEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public string Message { get; set; } = string.Empty;

    public IncidentSeverity Severity { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }
}
