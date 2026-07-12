using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Api.Models;
using OpenGitBase.Common.Auth;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.ComputeNode.Contracts;

namespace OpenGitBase.Api.Controllers;

[ApiController]
public sealed class ComputeNodeController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IUserContext _userContext;

    public ComputeNodeController(
        IQueryProcessor queryProcessor,
        IUserContext userContext
    )
    {
        _queryProcessor = queryProcessor;
        _userContext = userContext;
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
        [FromBody] CreateComputeNodeEnrollmentRequest request,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(request.NodeId))
        {
            return BadRequest(new { error = "NodeId is required." });
        }

        var result = await _queryProcessor.RunQueryAsync(
            new CreateComputeNodeEnrollmentQuery
            {
                NodeId = request.NodeId,
                CreatedByUserId = _userContext.User.UserId,
                OrganizationId = null,
                HostingScope = request.HostingScope,
                MaxConcurrentJobs = request.MaxConcurrentJobs,
                MaxCpu = request.MaxCpu,
                MaxMemoryBytes = request.MaxMemoryBytes,
            },
            cancellationToken
        ).ConfigureAwait(false);
        return result.IsSome ? Ok(result.Get()) : BadRequest(new { error = "Could not create enrollment." });
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
