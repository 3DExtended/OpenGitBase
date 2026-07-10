using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Api.Models;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
[Route("admin/storage-nodes")]
public sealed class AdminStorageNodesController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;

    public AdminStorageNodesController(IQueryProcessor queryProcessor)
    {
        _queryProcessor = queryProcessor;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StorageNodeDto>>> List(CancellationToken cancellationToken)
    {
        var result = await _queryProcessor.RunQueryAsync(new ListStorageNodeQuery(), cancellationToken);
        return Ok(result.IsSome ? result.Get() : Array.Empty<StorageNodeDto>());
    }

    [HttpPatch("{storageNodeId:guid}/capacity")]
    public async Task<ActionResult<StorageNodeDto>> UpdateCapacity(
        Guid storageNodeId,
        [FromBody] UpdateStorageNodeCapacityRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor.RunQueryAsync(
            new UpdateStorageNodeCapacityQuery
            {
                StorageNodeId = StorageNodeId.From(storageNodeId),
                MaxBytes = request.MaxBytes,
            },
            cancellationToken
        );

        return result.IsSome ? Ok(result.Get()) : NotFound();
    }
}
