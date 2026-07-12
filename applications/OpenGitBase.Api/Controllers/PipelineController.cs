using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Auth;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Controllers;

[ApiController]
public sealed class PipelineController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly RepositoryContentAuthorizationService _authorization;
    private readonly IUserContext _userContext;

    public PipelineController(
        IQueryProcessor queryProcessor,
        RepositoryContentAuthorizationService authorization,
        IUserContext userContext
    )
    {
        _queryProcessor = queryProcessor;
        _authorization = authorization;
        _userContext = userContext;
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
    [AllowAnonymous]
    public async Task<IActionResult> ListRepositoryRuns(Guid repositoryId, CancellationToken cancellationToken)
    {
        var access = await _authorization
            .AuthorizeReadByIdAsync(RepositoryId.From(repositoryId), cancellationToken)
            .ConfigureAwait(false);
        if (access.Kind != RepositoryContentAccessResultKind.Allowed)
        {
            return MapAccessFailure(access);
        }

        var result = await _queryProcessor.RunQueryAsync(
            new ListPipelineRunsQuery { RepositoryId = repositoryId },
            cancellationToken
        ).ConfigureAwait(false);
        return Ok(result.IsSome ? result.Get() : Array.Empty<PipelineRunDto>());
    }

    [HttpGet("pipeline/runs/{runId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRun(Guid runId, CancellationToken cancellationToken)
    {
        var result = await _queryProcessor.RunQueryAsync(
            new GetPipelineRunQuery { RunId = PipelineRunId.From(runId) },
            cancellationToken
        ).ConfigureAwait(false);
        if (result.IsNone)
        {
            return NotFound();
        }

        var access = await _authorization
            .AuthorizeReadByIdAsync(RepositoryId.From(result.Get().RepositoryId), cancellationToken)
            .ConfigureAwait(false);
        if (access.Kind != RepositoryContentAccessResultKind.Allowed)
        {
            return MapAccessFailure(access);
        }

        return Ok(result.Get());
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
                LogSection = request.LogSection,
                LogLines = request.LogLines ?? [],
            },
            cancellationToken
        ).ConfigureAwait(false);
        return ToActionResult(result);
    }

    [HttpGet("pipeline/jobs/{jobId:guid}/logs")]
    [AllowAnonymous]
    public async Task<IActionResult> GetJobLogs(Guid jobId, CancellationToken cancellationToken)
    {
        var jobResult = await _queryProcessor.RunQueryAsync(
            new GetPipelineJobQuery { JobId = PipelineJobId.From(jobId) },
            cancellationToken
        ).ConfigureAwait(false);
        if (jobResult.IsNone)
        {
            return NotFound();
        }

        var runResult = await _queryProcessor.RunQueryAsync(
            new GetPipelineRunQuery { RunId = jobResult.Get().RunId },
            cancellationToken
        ).ConfigureAwait(false);
        if (runResult.IsNone)
        {
            return NotFound();
        }

        var access = await _authorization
            .AuthorizeReadByIdAsync(RepositoryId.From(runResult.Get().RepositoryId), cancellationToken)
            .ConfigureAwait(false);
        if (access.Kind != RepositoryContentAccessResultKind.Allowed)
        {
            return MapAccessFailure(access);
        }

        var result = await _queryProcessor.RunQueryAsync(
            new GetPipelineJobLogsQuery { JobId = PipelineJobId.From(jobId) },
            cancellationToken
        ).ConfigureAwait(false);
        return ToActionResult(result);
    }

    [HttpPost("pipeline/jobs/{jobId:guid}/cancel")]
    [Authorize]
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

    [HttpGet("pipeline/base-images/resolve")]
    [AllowAnonymous]
    public async Task<IActionResult> ResolveBaseImage(
        [FromQuery] string slug,
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor.RunQueryAsync(
            new ResolveBaseImageBySlugQuery { Slug = slug },
            cancellationToken
        ).ConfigureAwait(false);
        return result.IsSome ? Ok(result.Get()) : NotFound(new { error = $"Unknown base image slug '{slug}'." });
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
                ContentHash = request.ContentHash,
                OciProvenance = request.OciProvenance,
                CreatedByUserId = _userContext.User.UserId,
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

    private IActionResult MapAccessFailure(RepositoryContentAccessResult access) =>
        access.Kind switch
        {
            RepositoryContentAccessResultKind.NotFound => NotFound(),
            RepositoryContentAccessResultKind.Forbidden => Forbid(),
            RepositoryContentAccessResultKind.Unavailable => StatusCode(
                StatusCodes.Status503ServiceUnavailable
            ),
            _ => NotFound(),
        };
}