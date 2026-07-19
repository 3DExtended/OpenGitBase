using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Status.Contracts;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[AllowAnonymous]
[EnableRateLimiting("content-browse-anonymous")]
[Route("public/status")]
public sealed class PublicStatusController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;

    public PublicStatusController(IQueryProcessor queryProcessor)
    {
        _queryProcessor = queryProcessor;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PublicStatusSnapshotDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PublicStatusSnapshotDto>> Get(CancellationToken cancellationToken)
    {
        var result = await _queryProcessor.RunQueryAsync(
            new GetPublicStatusQuery(),
            cancellationToken
        );
        return Ok(result.Get());
    }

    [HttpGet("history")]
    [ProducesResponseType(typeof(PublicStatusHistoryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PublicStatusHistoryDto>> GetHistory(
        [FromQuery] int days = 90,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _queryProcessor.RunQueryAsync(
            new GetPublicStatusHistoryQuery { Days = days },
            cancellationToken
        );
        return Ok(result.Get());
    }

    [HttpGet("windows")]
    [ProducesResponseType(typeof(List<PublicStatusOutageWindowDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PublicStatusOutageWindowDto>>> GetWindows(
        [FromQuery] int days = 7,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _queryProcessor.RunQueryAsync(
            new GetPublicStatusWindowsQuery { Days = days },
            cancellationToken
        );
        return Ok(result.Get());
    }
}
