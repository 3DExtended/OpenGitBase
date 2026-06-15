using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[AllowAnonymous]
[EnableRateLimiting("sensitive")]
[Route("api/v1/storage-nodes")]
public sealed class StorageNodeRegistryController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;

    public StorageNodeRegistryController(IQueryProcessor queryProcessor)
    {
        _queryProcessor = queryProcessor;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterStorageNodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RegisterStorageNodeResponse>> Register(
        [FromBody] RegisterStorageNodeRequest request,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(request.NodeId))
        {
            ModelState.AddModelError(nameof(request.NodeId), "NodeId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.InternalHost))
        {
            ModelState.AddModelError(nameof(request.InternalHost), "InternalHost is required.");
        }

        if (request.InternalHttpPort <= 0)
        {
            ModelState.AddModelError(
                nameof(request.InternalHttpPort),
                "InternalHttpPort must be positive."
            );
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!Request.Headers.TryGetValue("X-Storage-Enrollment-Token", out var enrollmentHeader))
        {
            enrollmentHeader = string.Empty;
        }

        var certificateThumbprint = StorageNodeCertificateHeaderReader.ReadThumbprint(Request);
        if (string.IsNullOrWhiteSpace(certificateThumbprint))
        {
            return BadRequest(new { error = "Storage node certificate thumbprint is required." });
        }

        var result = await _queryProcessor.RunQueryAsync(
            new RegisterStorageNodeQuery
            {
                NodeId = request.NodeId,
                InternalHost = request.InternalHost,
                InternalSshPort = request.InternalSshPort,
                InternalHttpPort = request.InternalHttpPort,
                FreeBytesAvailable = request.FreeBytesAvailable,
                TotalBytesAvailable = request.TotalBytesAvailable,
                EnrollmentToken = enrollmentHeader.ToString(),
                CertificateThumbprint = certificateThumbprint,
            },
            cancellationToken
        );

        if (result.IsNone)
        {
            return BadRequest(new { error = "Storage node registration failed. Check enrollment token and node id." });
        }

        var payload = result.Get();
        return Ok(
            new RegisterStorageNodeResponse
            {
                StorageNodeId = payload.StorageNodeId.Value,
                ApiToken = payload.ApiToken,
                HeartbeatIntervalSeconds = payload.HeartbeatIntervalSeconds,
            }
        );
    }

    [HttpPost("heartbeat")]
    [ProducesResponseType(typeof(StorageNodeHeartbeatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<StorageNodeHeartbeatResponse>> Heartbeat(
        [FromBody] StorageNodeHeartbeatRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetBearerToken(out var token))
        {
            return Unauthorized();
        }

        var certificateThumbprint = StorageNodeCertificateHeaderReader.ReadThumbprint(Request);
        if (string.IsNullOrWhiteSpace(certificateThumbprint))
        {
            return Unauthorized();
        }

        var verified = await _queryProcessor.RunQueryAsync(
            new VerifyStorageNodeTokenQuery
            {
                NodeId = request.NodeId,
                ApiToken = token,
                CertificateThumbprint = certificateThumbprint,
            },
            cancellationToken
        );
        if (verified.IsNone)
        {
            return Unauthorized();
        }

        var result = await _queryProcessor.RunQueryAsync(
            new StorageNodeHeartbeatQuery
            {
                NodeId = request.NodeId,
                FreeBytesAvailable = request.FreeBytesAvailable,
                TotalBytesAvailable = request.TotalBytesAvailable,
                CertificateThumbprint = certificateThumbprint,
            },
            cancellationToken
        );

        if (result.IsNone)
        {
            return NotFound();
        }

        return Ok(new StorageNodeHeartbeatResponse { Acknowledged = result.Get().Acknowledged });
    }

    [HttpGet("healthy")]
    [ProducesResponseType(typeof(IReadOnlyList<StorageNodeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StorageNodeDto>>> ListHealthy(
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor.RunQueryAsync(
            new ListHealthyStorageNodesQuery(),
            cancellationToken
        );

        return Ok(result.IsSome ? result.Get() : Array.Empty<StorageNodeDto>());
    }

    private bool TryGetBearerToken(out string token)
    {
        token = string.Empty;
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        token = header["Bearer ".Length..].Trim();
        return !string.IsNullOrWhiteSpace(token);
    }
}
