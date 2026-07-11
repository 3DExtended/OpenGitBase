using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.ComputeNode.Contracts;

namespace OpenGitBase.Api.Controllers;

[ApiController]
public sealed class ComputeNodeController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;

    public ComputeNodeController(IQueryProcessor queryProcessor)
    {
        _queryProcessor = queryProcessor;
    }

    [HttpPost("api/v1/compute-nodes/register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(
        [FromBody] RegisterComputeNodeQuery query,
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor.RunQueryAsync(query, cancellationToken).ConfigureAwait(false);
        return result.IsSome ? Ok(result.Get()) : BadRequest();
    }

    [HttpPost("api/v1/compute-nodes/heartbeat")]
    [AllowAnonymous]
    public async Task<IActionResult> Heartbeat(
        [FromBody] ComputeNodeHeartbeatQuery query,
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor.RunQueryAsync(query, cancellationToken).ConfigureAwait(false);
        return result.IsSome ? Ok(result.Get()) : NotFound();
    }

    [HttpPost("admin/compute-nodes/enrollments")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreatePlatformEnrollment(
        [FromBody] CreateComputeNodeEnrollmentQuery query,
        CancellationToken cancellationToken
    )
    {
        query.OrganizationId = null;
        var result = await _queryProcessor.RunQueryAsync(query, cancellationToken).ConfigureAwait(false);
        return result.IsSome ? Ok(new { enrollmentToken = result.Get() }) : BadRequest();
    }

    [HttpPost("organizations/{organizationId:guid}/compute-nodes/enrollments")]
    [Authorize]
    public async Task<IActionResult> CreateOrganizationEnrollment(
        Guid organizationId,
        [FromBody] CreateComputeNodeEnrollmentQuery query,
        CancellationToken cancellationToken
    )
    {
        query.OrganizationId = organizationId;
        var result = await _queryProcessor.RunQueryAsync(query, cancellationToken).ConfigureAwait(false);
        return result.IsSome ? Ok(new { enrollmentToken = result.Get() }) : BadRequest();
    }

    [HttpPatch("admin/compute-nodes/{computeNodeId:guid}/capacity")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateCapacity(
        Guid computeNodeId,
        [FromBody] UpdateComputeNodeCapacityRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor.RunQueryAsync(
            new UpdateComputeNodeCapacityQuery
            {
                ComputeNodeId = ComputeNodeId.From(computeNodeId),
                MaxConcurrentJobs = request.MaxConcurrentJobs,
                MaxCpu = request.MaxCpu,
                MaxMemoryBytes = request.MaxMemoryBytes,
            },
            cancellationToken
        ).ConfigureAwait(false);
        return result.IsSome ? Ok(result.Get()) : BadRequest();
    }

    [HttpGet("admin/compute-nodes")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _queryProcessor.RunQueryAsync(
            new ListComputeNodesQuery(),
            cancellationToken
        ).ConfigureAwait(false);
        return Ok(result.IsSome ? result.Get() : Array.Empty<ComputeNodeDto>());
    }
}
