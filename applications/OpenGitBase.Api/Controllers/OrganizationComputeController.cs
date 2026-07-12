using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Auth;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.ComputeNode.Contracts;
using OpenGitBase.Features.Organization.Contracts;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[Authorize]
[Route("organization/{organizationId:guid}/compute")]
public sealed class OrganizationComputeController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IUserContext _userContext;
    private readonly IOrganizationAccessService _organizationAccess;

    public OrganizationComputeController(
        IQueryProcessor queryProcessor,
        IUserContext userContext,
        IOrganizationAccessService organizationAccess
    )
    {
        _queryProcessor = queryProcessor;
        _userContext = userContext;
        _organizationAccess = organizationAccess;
    }

    [HttpGet("nodes")]
    public async Task<ActionResult<IReadOnlyList<ComputeNodeDto>>> ListNodes(
        Guid organizationId,
        CancellationToken cancellationToken
    )
    {
        var access = await AuthorizeOwnerAsync(organizationId, cancellationToken)
            .ConfigureAwait(false);
        if (access is not null)
        {
            return access;
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new ListComputeNodesQuery { OrganizationId = organizationId },
                cancellationToken
            )
            .ConfigureAwait(false);

        return Ok(result.IsSome ? result.Get() : Array.Empty<ComputeNodeDto>());
    }

    [HttpGet("enrollments")]
    public async Task<ActionResult<IReadOnlyList<ComputeNodeEnrollmentDto>>> ListEnrollments(
        Guid organizationId,
        CancellationToken cancellationToken
    )
    {
        var access = await AuthorizeOwnerAsync(organizationId, cancellationToken)
            .ConfigureAwait(false);
        if (access is not null)
        {
            return access;
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new ListComputeNodeEnrollmentsQuery { OrganizationId = organizationId },
                cancellationToken
            )
            .ConfigureAwait(false);

        return Ok(result.IsSome ? result.Get() : Array.Empty<ComputeNodeEnrollmentDto>());
    }

    [HttpPost("enrollments")]
    public async Task<ActionResult<CreateComputeNodeEnrollmentResult>> CreateEnrollment(
        Guid organizationId,
        [FromBody] CreateComputeNodeEnrollmentRequest request,
        CancellationToken cancellationToken
    )
    {
        var access = await AuthorizeOwnerAsync(organizationId, cancellationToken)
            .ConfigureAwait(false);
        if (access is not null)
        {
            return access;
        }

        if (string.IsNullOrWhiteSpace(request.NodeId))
        {
            return BadRequest(new { error = "NodeId is required." });
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new CreateComputeNodeEnrollmentQuery
                {
                    NodeId = request.NodeId,
                    CreatedByUserId = _userContext.User.UserId,
                    OrganizationId = organizationId,
                    HostingScope = request.HostingScope,
                    MaxConcurrentJobs = request.MaxConcurrentJobs,
                    MaxCpu = request.MaxCpu,
                    MaxMemoryBytes = request.MaxMemoryBytes,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (result.IsNone)
        {
            return BadRequest(new { error = "Could not create enrollment." });
        }

        return Ok(result.Get());
    }

    [HttpPatch("nodes/{computeNodeId:guid}/capacity")]
    public async Task<ActionResult<ComputeNodeDto>> UpdateCapacity(
        Guid organizationId,
        Guid computeNodeId,
        [FromBody] UpdateComputeNodeCapacityRequest request,
        CancellationToken cancellationToken
    )
    {
        var access = await AuthorizeOwnerAsync(organizationId, cancellationToken)
            .ConfigureAwait(false);
        if (access is not null)
        {
            return access;
        }

        var nodes = await _queryProcessor
            .RunQueryAsync(
                new ListComputeNodesQuery { OrganizationId = organizationId },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (
            nodes.IsNone
            || !nodes.Get().Any(node => node.Id.Value == computeNodeId)
        )
        {
            return NotFound();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new UpdateComputeNodeCapacityQuery
                {
                    ComputeNodeId = ComputeNodeId.From(computeNodeId),
                    MaxConcurrentJobs = request.MaxConcurrentJobs,
                    MaxCpu = request.MaxCpu,
                    MaxMemoryBytes = request.MaxMemoryBytes,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (result.IsNone)
        {
            return BadRequest(new { error = "Capacity reduction rejected while jobs are running." });
        }

        return Ok(result.Get());
    }

    private async Task<ActionResult?> AuthorizeOwnerAsync(
        Guid organizationId,
        CancellationToken cancellationToken
    )
    {
        var access = await _organizationAccess
            .CheckOwnerAccessAsync(
                OrganizationId.From(organizationId),
                _userContext.GetUserId(),
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!access.OrganizationExists)
        {
            return NotFound();
        }

        if (!access.IsOwner)
        {
            return Forbid();
        }

        return null;
    }
}
