using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[Authorize]
public sealed class PipelineController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;

    public PipelineController(IQueryProcessor queryProcessor)
    {
        _queryProcessor = queryProcessor;
    }

    [HttpPost("api/v1/internal/pipelines/git-push-ingest")]
    [AllowAnonymous]
    public async Task<IActionResult> IngestGitPush(
        [FromBody] IngestGitPushQuery query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(Request.Headers["X-Storage-Node-Id"]))
        {
            return Unauthorized();
        }

        var result = await _queryProcessor.RunQueryAsync(query, cancellationToken).ConfigureAwait(false);
        return result.IsSome ? Accepted() : BadRequest();
    }

    [HttpGet("repository/{repositoryId:guid}/pipelines")]
    public async Task<IActionResult> ListRepositoryRuns(Guid repositoryId, CancellationToken cancellationToken)
    {
        var result = await _queryProcessor.RunQueryAsync(
            new ListPipelineRunsQuery { RepositoryId = repositoryId },
            cancellationToken
        ).ConfigureAwait(false);
        return Ok(result.IsSome ? result.Get() : Array.Empty<PipelineRunDto>());
    }

    [HttpGet("pipeline/runs/{runId:guid}")]
    public async Task<IActionResult> GetRun(Guid runId, CancellationToken cancellationToken)
    {
        var result = await _queryProcessor.RunQueryAsync(
            new GetPipelineRunQuery { RunId = PipelineRunId.From(runId) },
            cancellationToken
        ).ConfigureAwait(false);
        return ToActionResult(result);
    }

    [HttpPost("pipeline/jobs/claim")]
    [AllowAnonymous]
    public async Task<IActionResult> ClaimJob(
        [FromBody] ClaimPipelineJobQuery query,
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor.RunQueryAsync(query, cancellationToken).ConfigureAwait(false);
        return result.IsSome ? Ok(result.Get()) : NoContent();
    }

    [HttpPost("pipeline/jobs/{jobId:guid}/status")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdateJobStatus(
        Guid jobId,
        [FromBody] UpdatePipelineJobStatusRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor.RunQueryAsync(
            new UpdatePipelineJobStatusQuery
            {
                JobId = PipelineJobId.From(jobId),
                Status = request.Status,
                Message = request.Message,
            },
            cancellationToken
        ).ConfigureAwait(false);
        return ToActionResult(result);
    }

    [HttpPost("pipeline/jobs/{jobId:guid}/cancel")]
    public async Task<IActionResult> CancelJob(Guid jobId, CancellationToken cancellationToken)
    {
        var result = await _queryProcessor.RunQueryAsync(
            new CancelPipelineJobQuery { JobId = PipelineJobId.From(jobId) },
            cancellationToken
        ).ConfigureAwait(false);
        return ToActionResult(result);
    }

    [HttpPost("pipeline/jobs/{jobId:guid}/dependency-install-outcomes")]
    [AllowAnonymous]
    public async Task<IActionResult> RecordDependencyInstallOutcome(
        Guid jobId,
        [FromBody] RecordDependencyInstallOutcomeRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor.RunQueryAsync(
            new RecordDependencyInstallOutcomeQuery
            {
                JobId = PipelineJobId.From(jobId),
                RecipeKey = request.RecipeKey,
                Success = request.Success,
                ExitCode = request.ExitCode,
                DurationMs = request.DurationMs,
            },
            cancellationToken
        ).ConfigureAwait(false);
        return result.IsSome ? Accepted() : BadRequest();
    }

    [HttpPost("admin/pipeline/dependency-promotions")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> RequestDependencyPromotion(
        [FromBody] RequestDependencyPromotionRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor.RunQueryAsync(
            new RequestDependencyLayerPromotionQuery
            {
                RecipeKey = request.RecipeKey,
                RequestedByUserId = ResolveCurrentUserId(),
            },
            cancellationToken
        ).ConfigureAwait(false);
        return ToActionResult(result);
    }

    [HttpPost("pipeline/egress/domain-requests")]
    public async Task<IActionResult> SubmitDomainRequest(
        [FromBody] SubmitDomainAllowanceRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor.RunQueryAsync(
            new SubmitDomainAllowanceRequestQuery
            {
                Domain = request.Domain,
                Justification = request.Justification,
                Scope = request.Scope,
                OrganizationId = request.OrganizationId,
                RequestedByUserId = ResolveCurrentUserId(),
            },
            cancellationToken
        ).ConfigureAwait(false);
        return ToActionResult(result);
    }

    [HttpPost("admin/pipeline/egress/domain-requests/{requestId:guid}/approve")]
    [Authorize(Roles = "admin")]
    public Task<IActionResult> ApprovePlatformDomainRequest(Guid requestId, CancellationToken cancellationToken) =>
        ReviewDomainRequestAsync(requestId, true, cancellationToken);

    [HttpPost("admin/pipeline/egress/domain-requests/{requestId:guid}/deny")]
    [Authorize(Roles = "admin")]
    public Task<IActionResult> DenyPlatformDomainRequest(Guid requestId, CancellationToken cancellationToken) =>
        ReviewDomainRequestAsync(requestId, false, cancellationToken);

    [HttpPost("organizations/{organizationId:guid}/pipeline/egress/domain-requests/{requestId:guid}/approve")]
    public Task<IActionResult> ApproveOrganizationDomainRequest(
        Guid organizationId,
        Guid requestId,
        CancellationToken cancellationToken
    ) => ReviewDomainRequestAsync(requestId, true, cancellationToken);

    [HttpPost("organizations/{organizationId:guid}/pipeline/egress/domain-requests/{requestId:guid}/deny")]
    public Task<IActionResult> DenyOrganizationDomainRequest(
        Guid organizationId,
        Guid requestId,
        CancellationToken cancellationToken
    ) => ReviewDomainRequestAsync(requestId, false, cancellationToken);

    [HttpGet("pipeline/egress/effective")]
    public async Task<IActionResult> GetEffectiveEgressAllowlist(
        [FromQuery(Name = "runs-on")] string runsOn,
        [FromQuery] Guid? organizationId,
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor.RunQueryAsync(
            new ResolveEffectiveEgressAllowlistQuery
            {
                RunsOn = runsOn,
                OrganizationId = organizationId,
            },
            cancellationToken
        ).ConfigureAwait(false);
        return Ok(result.IsSome ? result.Get() : Array.Empty<string>());
    }

    [HttpPost("admin/pipeline/base-images")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateBaseImage(
        [FromBody] CreateBaseImageCatalogEntryRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor.RunQueryAsync(
            new CreateBaseImageCatalogEntryQuery
            {
                Slug = request.Slug,
                VersionLabel = request.VersionLabel,
                ArtifactUri = request.ArtifactUri,
                OciProvenance = request.OciProvenance,
                CreatedByUserId = request.CreatedByUserId,
            },
            cancellationToken
        ).ConfigureAwait(false);
        return ToActionResult(result);
    }

    [HttpGet("admin/pipeline/base-images")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ListBaseImages(CancellationToken cancellationToken)
    {
        var result = await _queryProcessor.RunQueryAsync(
            new ListBaseImageCatalogEntriesQuery(),
            cancellationToken
        ).ConfigureAwait(false);
        return Ok(result.IsSome ? result.Get() : Array.Empty<BaseImageCatalogEntryDto>());
    }

    private IActionResult ToActionResult<T>(Option<T> result)
    {
        if (result.IsNone)
        {
            return NotFound();
        }

        return Ok(result.Get());
    }

    private async Task<IActionResult> ReviewDomainRequestAsync(
        Guid requestId,
        bool approve,
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor.RunQueryAsync(
            new ReviewDomainAllowanceRequestQuery
            {
                RequestId = DomainAllowanceRequestId.From(requestId),
                Approve = approve,
                ReviewedByUserId = ResolveCurrentUserId(),
            },
            cancellationToken
        ).ConfigureAwait(false);
        return ToActionResult(result);
    }

    private Guid ResolveCurrentUserId()
    {
        if (
            Guid.TryParse(User.FindFirst("identityproviderid")?.Value, out var userId)
            && userId != Guid.Empty
        )
        {
            return userId;
        }

        return Guid.Empty;
    }
}