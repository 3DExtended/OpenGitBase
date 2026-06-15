using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Api.Models;
using OpenGitBase.Common.Auth;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
[Route("admin/storage-enrollments")]
public sealed class AdminStorageEnrollmentsController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IUserContext _userContext;

    public AdminStorageEnrollmentsController(
        IQueryProcessor queryProcessor,
        IUserContext userContext
    )
    {
        _queryProcessor = queryProcessor;
        _userContext = userContext;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StorageNodeEnrollmentDto>>> List(
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor.RunQueryAsync(
            new ListStorageNodeEnrollmentsQuery(),
            cancellationToken
        );
        return Ok(result.IsSome ? result.Get() : Array.Empty<StorageNodeEnrollmentDto>());
    }

    [HttpPost]
    public async Task<ActionResult<CreateStorageNodeEnrollmentResult>> Create(
        [FromBody] CreateStorageNodeEnrollmentRequest request,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(request.NodeId))
        {
            return BadRequest(new { error = "NodeId is required." });
        }

        var result = await _queryProcessor.RunQueryAsync(
            new CreateStorageNodeEnrollmentQuery
            {
                NodeId = request.NodeId,
                CreatedByUserId = _userContext.User.UserId,
                ExpiresInHours = request.ExpiresInHours,
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
