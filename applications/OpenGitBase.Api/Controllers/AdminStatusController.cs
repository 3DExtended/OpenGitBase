using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Api.Models;
using OpenGitBase.Common.Auth;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Status.Contracts;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
[Route("admin/status")]
public sealed class AdminStatusController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IUserContext _userContext;

    public AdminStatusController(IQueryProcessor queryProcessor, IUserContext userContext)
    {
        _queryProcessor = queryProcessor;
        _userContext = userContext;
    }

    [HttpGet("incident")]
    [ProducesResponseType(typeof(PublicStatusIncidentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PublicStatusIncidentDto?>> GetIncident(
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor.RunQueryAsync(
            new GetAdminStatusIncidentBannerQuery(),
            cancellationToken
        );
        return Ok(result.Get());
    }

    [HttpPost("incident")]
    [ProducesResponseType(typeof(PublicStatusIncidentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PublicStatusIncidentDto>> SetIncident(
        [FromBody] SetStatusIncidentBannerRequest request,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Message is required." });
        }

        if (!Enum.TryParse<IncidentSeverity>(request.Severity, true, out var severity))
        {
            return BadRequest(new { error = "Severity is invalid." });
        }

        var userId = _userContext.User.UserId;

        var result = await _queryProcessor.RunQueryAsync(
            new SetStatusIncidentBannerQuery
            {
                Message = request.Message,
                Severity = severity,
                AdminUserId = userId,
            },
            cancellationToken
        );

        if (result.IsNone)
        {
            return BadRequest(new { error = "Unable to set incident banner." });
        }

        return Ok(result.Get());
    }

    [HttpPost("incident/resolve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResolveIncident(CancellationToken cancellationToken)
    {
        await _queryProcessor.RunQueryAsync(
            new ResolveStatusIncidentBannerQuery(),
            cancellationToken
        );
        return NoContent();
    }

    [HttpGet("windows")]
    [ProducesResponseType(typeof(List<AdminStatusOutageWindowDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AdminStatusOutageWindowDto>>> GetWindows(
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor.RunQueryAsync(
            new ListAdminStatusOutageWindowsQuery(),
            cancellationToken
        );
        return Ok(result.Get());
    }

    [HttpPost("windows/{windowId:guid}/suppress")]
    [ProducesResponseType(typeof(AdminStatusOutageWindowDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminStatusOutageWindowDto>> Suppress(
        Guid windowId,
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor.RunQueryAsync(
            new SuppressStatusOutageWindowQuery { WindowId = windowId, Suppressed = true },
            cancellationToken
        );
        return result.IsNone ? NotFound() : Ok(result.Get());
    }

    [HttpPost("windows/{windowId:guid}/unsuppress")]
    [ProducesResponseType(typeof(AdminStatusOutageWindowDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminStatusOutageWindowDto>> Unsuppress(
        Guid windowId,
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor.RunQueryAsync(
            new SuppressStatusOutageWindowQuery { WindowId = windowId, Suppressed = false },
            cancellationToken
        );
        return result.IsNone ? NotFound() : Ok(result.Get());
    }

    [HttpPut("windows/{windowId:guid}/annotation")]
    [ProducesResponseType(typeof(AdminStatusOutageWindowDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminStatusOutageWindowDto>> SetWindowAnnotation(
        Guid windowId,
        [FromBody] SetStatusOutageWindowAnnotationRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor.RunQueryAsync(
            new SetStatusOutageWindowAnnotationQuery
            {
                WindowId = windowId,
                Annotation = request.Annotation,
            },
            cancellationToken
        );
        return result.IsNone ? NotFound() : Ok(result.Get());
    }
}
