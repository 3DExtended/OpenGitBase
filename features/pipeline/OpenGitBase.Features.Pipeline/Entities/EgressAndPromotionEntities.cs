using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Pipeline.Contracts;

namespace OpenGitBase.Features.Pipeline.Entities;

#pragma warning disable SA1402

public sealed class DependencyPromotionRequestEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public string RecipeKey { get; set; } = string.Empty;

    public DependencyPromotionRequestStatus Status { get; set; }

    public Guid RequestedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class DomainAllowanceRequestEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public string Domain { get; set; } = string.Empty;

    public string Justification { get; set; } = string.Empty;

    public DomainAllowanceRequestScope Scope { get; set; }

    public Guid? OrganizationId { get; set; }

    public DomainAllowanceRequestStatus Status { get; set; }

    public Guid RequestedByUserId { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }
}

public sealed class PlatformEgressAllowlistEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public string Domain { get; set; } = string.Empty;

    public Guid ApprovedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class OrgEgressAllowlistEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string Domain { get; set; } = string.Empty;

    public Guid ApprovedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

#pragma warning restore SA1402
