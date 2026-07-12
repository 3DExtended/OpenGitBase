using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Api.Models;
using OpenGitBase.Common.Auth;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.ComputeNode.Contracts;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
[Route("admin/compute-enrollments")]
public sealed class AdminComputeEnrollmentsController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IUserContext _userContext;

    public AdminComputeEnrollmentsController(
        IQueryProcessor queryProcessor,
        IUserContext userContext
    )
    {
        _queryProcessor = queryProcessor;
        _userContext = userContext;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ComputeNodeEnrollmentDto>>> List(
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor.RunQueryAsync(
            new ListComputeNodeEnrollmentsQuery(),
            cancellationToken
        );
        return Ok(result.IsSome ? result.Get() : Array.Empty<ComputeNodeEnrollmentDto>());
    }

    [HttpPost]
    public async Task<ActionResult<CreateComputeNodeEnrollmentResult>> Create(
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
        );

        if (result.IsNone)
        {
            return BadRequest(new { error = "Could not create enrollment." });
        }

        return Ok(result.Get());
    }
}
