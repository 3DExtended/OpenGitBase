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
}