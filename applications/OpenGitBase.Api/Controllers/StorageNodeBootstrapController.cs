using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[AllowAnonymous]
[EnableRateLimiting("sensitive")]
[Route("api/v1/storage-nodes/bootstrap")]
public sealed class StorageNodeBootstrapController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;

    public StorageNodeBootstrapController(IQueryProcessor queryProcessor)
    {
        _queryProcessor = queryProcessor;
    }

    [HttpGet("dispatcher-ssh-public-key")]
    public async Task<IActionResult> GetDispatcherSshPublicKey(CancellationToken cancellationToken)
    {
        if (!TryGetEnrollmentToken(out var enrollmentToken))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(Request.Headers["X-Storage-Node-Id"]))
        {
            return BadRequest(new { error = "X-Storage-Node-Id header is required." });
        }

        var nodeId = Request.Headers["X-Storage-Node-Id"].ToString();
        var verified = await _queryProcessor.RunQueryAsync(
            new VerifyStorageNodeEnrollmentQuery
            {
                NodeId = nodeId,
                EnrollmentToken = enrollmentToken,
                Consume = false,
            },
            cancellationToken
        );
        if (verified.IsNone)
        {
            return Unauthorized();
        }

        var publicKey = await _queryProcessor.RunQueryAsync(
            new GetFleetDispatcherSshPublicKeyQuery(),
            cancellationToken
        );
        if (publicKey.IsNone)
        {
            return NotFound(new { error = "Fleet dispatcher SSH public key is not configured." });
        }

        return Ok(new { publicKey = publicKey.Get() });
    }

    private bool TryGetEnrollmentToken(out string token)
    {
        token = Request.Headers["X-Storage-Enrollment-Token"].ToString();
        return !string.IsNullOrWhiteSpace(token);
    }
}
