using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
[Route("admin/fleet")]
public sealed class AdminFleetController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;

    public AdminFleetController(IQueryProcessor queryProcessor)
    {
        _queryProcessor = queryProcessor;
    }

    [HttpPost("dispatcher-ssh-keys/generate")]
    public async Task<ActionResult<GenerateFleetDispatcherSshKeysResult>> GenerateDispatcherSshKeys(
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor.RunQueryAsync(
            new GenerateFleetDispatcherSshKeysQuery(),
            cancellationToken
        );
        if (result.IsNone)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Key generation failed." });
        }

        return Ok(result.Get());
    }

    [HttpGet("dispatcher-ssh-public-key")]
    public async Task<ActionResult<string>> GetDispatcherSshPublicKey(
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor.RunQueryAsync(
            new GetFleetDispatcherSshPublicKeyQuery(),
            cancellationToken
        );
        if (result.IsNone)
        {
            return NotFound();
        }

        return Ok(new { publicKey = result.Get() });
    }
}
