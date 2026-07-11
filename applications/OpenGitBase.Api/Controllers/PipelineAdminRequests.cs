using OpenGitBase.Features.Pipeline.Contracts;

namespace OpenGitBase.Api.Controllers;

#pragma warning disable SA1402

public sealed class RecordDependencyInstallOutcomeRequest
{
    public string RecipeKey { get; set; } = string.Empty;

    public bool Success { get; set; }

    public int ExitCode { get; set; }

    public long DurationMs { get; set; }
}

public sealed class RequestDependencyPromotionRequest
{
    public string RecipeKey { get; set; } = string.Empty;
}

public sealed class SubmitDomainAllowanceRequest
{
    public string Domain { get; set; } = string.Empty;

    public string Justification { get; set; } = string.Empty;

    public DomainAllowanceRequestScope Scope { get; set; } = DomainAllowanceRequestScope.Platform;

    public Guid? OrganizationId { get; set; }
}

#pragma warning restore SA1402
