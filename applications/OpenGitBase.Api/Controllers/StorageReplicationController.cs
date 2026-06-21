using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[AllowAnonymous]
[EnableRateLimiting("sensitive")]
[Route("api/v1/storage-nodes/repositories")]
public sealed class StorageReplicationController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;

    public StorageReplicationController(IQueryProcessor queryProcessor)
    {
        _queryProcessor = queryProcessor;
    }

    [HttpGet("{repositoryId:guid}/replication")]
    [ProducesResponseType(typeof(RepositoryReplicationContextDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RepositoryReplicationContextDto>> GetReplicationContext(
        Guid repositoryId,
        CancellationToken cancellationToken
    )
    {
        var storageNodeId = await AuthenticateStorageNodeAsync(cancellationToken).ConfigureAwait(false);
        if (storageNodeId is null)
        {
            return Unauthorized();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new GetRepositoryReplicationContextQuery
                {
                    RepositoryId = RepositoryId.From(repositoryId),
                    StorageNodeId = storageNodeId,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (result.IsNone)
        {
            return NotFound();
        }

        return Ok(result.Get());
    }

    [HttpPost("{repositoryId:guid}/quorum-replicate")]
    [ProducesResponseType(typeof(QuorumReplicateRepositoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<QuorumReplicateRepositoryResponse>> QuorumReplicate(
        Guid repositoryId,
        [FromBody] QuorumReplicateRepositoryRequest request,
        CancellationToken cancellationToken
    )
    {
        var storageNodeId = await AuthenticateStorageNodeAsync(cancellationToken).ConfigureAwait(false);
        if (storageNodeId is null)
        {
            return Unauthorized();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new QuorumReplicateRepositoryQuery
                {
                    RepositoryId = RepositoryId.From(repositoryId),
                    StorageNodeId = storageNodeId,
                    AppliedWatermark = request.AppliedWatermark,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (result.IsNone || !result.Get().Success)
        {
            var error = result.IsSome
                ? result.Get().Error
                : "Quorum replication failed.";
            return Conflict(new QuorumReplicateRepositoryResponse { Success = false, Error = error });
        }

        return Ok(
            new QuorumReplicateRepositoryResponse
            {
                Success = true,
                PrimaryWatermark = result.Get().PrimaryWatermark,
            }
        );
    }

    private async Task<StorageNodeId?> AuthenticateStorageNodeAsync(CancellationToken cancellationToken)
    {
        if (!TryGetBearerToken(out var token))
        {
            return null;
        }

        var certificateThumbprint = StorageNodeCertificateHeaderReader.ReadThumbprint(Request);
        if (string.IsNullOrWhiteSpace(certificateThumbprint))
        {
            return null;
        }

        var nodeIdHeader = Request.Headers["X-Storage-Node-Id"].ToString();
        if (string.IsNullOrWhiteSpace(nodeIdHeader))
        {
            return null;
        }

        var verified = await _queryProcessor
            .RunQueryAsync(
                new VerifyStorageNodeTokenQuery
                {
                    NodeId = nodeIdHeader,
                    ApiToken = token,
                    CertificateThumbprint = certificateThumbprint,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return verified.IsSome ? verified.Get() : null;
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
