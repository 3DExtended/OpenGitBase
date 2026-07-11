using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Pipeline.Contracts;

public enum DomainAllowanceRequestScope
{
    Platform = 0,
    Organization = 1,
}

public enum DomainAllowanceRequestStatus
{
    Pending = 0,
    Approved = 1,
    Denied = 2,
}

public enum DependencyPromotionRequestStatus
{
    Queued = 0,
    Rejected = 1,
}

public sealed record DomainAllowanceRequestId : Identifier<Guid, DomainAllowanceRequestId>;

public sealed record DependencyPromotionRequestId
    : Identifier<Guid, DependencyPromotionRequestId>;

public sealed class AdvancePipelineRunQuery : IQuery<PipelineRunDto, AdvancePipelineRunQuery>
{
    public PipelineRunId RunId { get; set; } = PipelineRunId.From(Guid.NewGuid());
}

public sealed class RecordDependencyInstallOutcomeQuery
    : IQuery<bool, RecordDependencyInstallOutcomeQuery>
{
    public PipelineJobId JobId { get; set; } = PipelineJobId.From(Guid.NewGuid());

    public string RecipeKey { get; set; } = string.Empty;

    public bool Success { get; set; }

    public int ExitCode { get; set; }

    public long DurationMs { get; set; }
}

public sealed class RequestDependencyLayerPromotionQuery
    : IQuery<DependencyPromotionRequestDto, RequestDependencyLayerPromotionQuery>
{
    public string RecipeKey { get; set; } = string.Empty;

    public Guid RequestedByUserId { get; set; }
}

public sealed class DependencyPromotionRequestDto
{
    public DependencyPromotionRequestId Id { get; set; } =
        DependencyPromotionRequestId.From(Guid.NewGuid());

    public string RecipeKey { get; set; } = string.Empty;

    public DependencyPromotionRequestStatus Status { get; set; }

    public bool PromotionJobScheduled { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class SubmitDomainAllowanceRequestQuery
    : IQuery<DomainAllowanceRequestDto, SubmitDomainAllowanceRequestQuery>
{
    public string Domain { get; set; } = string.Empty;

    public string Justification { get; set; } = string.Empty;

    public DomainAllowanceRequestScope Scope { get; set; }

    public Guid? OrganizationId { get; set; }

    public Guid RequestedByUserId { get; set; }
}

public sealed class ReviewDomainAllowanceRequestQuery
    : IQuery<DomainAllowanceRequestDto, ReviewDomainAllowanceRequestQuery>
{
    public DomainAllowanceRequestId RequestId { get; set; } =
        DomainAllowanceRequestId.From(Guid.NewGuid());

    public bool Approve { get; set; }

    public Guid ReviewedByUserId { get; set; }
}

public sealed class DomainAllowanceRequestDto
{
    public DomainAllowanceRequestId Id { get; set; } = DomainAllowanceRequestId.From(Guid.NewGuid());

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

public sealed class ResolveEffectiveEgressAllowlistQuery
    : IQuery<IReadOnlyList<string>, ResolveEffectiveEgressAllowlistQuery>
{
    public string RunsOn { get; set; } = string.Empty;

    public Guid? OrganizationId { get; set; }
}
