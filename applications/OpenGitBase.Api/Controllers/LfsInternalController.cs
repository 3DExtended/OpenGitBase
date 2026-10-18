using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;

namespace OpenGitBase.Api.Controllers;

/// <summary>
/// Git LFS endpoints for the dispatcher. Reachable only from the internal network
/// (<c>InternalNetworkOptions.RestrictedPathPrefixes</c>); the dispatcher relays status and body to the client.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/internal/lfs")]
public sealed class LfsInternalController : ControllerBase
{
    private readonly LfsService _lfsService;

    public LfsInternalController(LfsService lfsService)
    {
        _lfsService = lfsService;
    }

    [HttpPost("batch")]
    public async Task<IActionResult> Batch([FromBody] LfsBatchRequest request, CancellationToken cancellationToken) =>
        ToActionResult(
            await _lfsService.BatchAsync(request, HttpContext.TraceIdentifier, cancellationToken).ConfigureAwait(false)
        );

    [HttpPost("transfer-check")]
    public async Task<IActionResult> CheckTransfer(
        [FromBody] LfsTransferCheckRequest request,
        CancellationToken cancellationToken
    ) =>
        ToActionResult(
            await _lfsService
                .CheckTransferAsync(request, HttpContext.TraceIdentifier, cancellationToken)
                .ConfigureAwait(false)
        );

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] LfsVerifyRequest request, CancellationToken cancellationToken) =>
        ToActionResult(
            await _lfsService.VerifyAsync(request, HttpContext.TraceIdentifier, cancellationToken).ConfigureAwait(false)
        );

    [HttpPost("repair")]
    public async Task<IActionResult> RequestRepair([FromBody] LfsRepairRequest request, CancellationToken cancellationToken) =>
        ToActionResult(
            await _lfsService
                .RequestRepairAsync(request, HttpContext.TraceIdentifier, cancellationToken)
                .ConfigureAwait(false)
        );

    private ObjectResult ToActionResult(LfsResult result)
    {
        if (result.RetryAfterSeconds is { } retryAfter)
        {
            Response.Headers.RetryAfter = retryAfter.ToString(CultureInfo.InvariantCulture);
        }

        return new ObjectResult(result.Body) { StatusCode = result.StatusCode };
    }
}
