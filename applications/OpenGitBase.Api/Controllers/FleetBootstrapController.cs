using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/fleet/bootstrap")]
public sealed class FleetBootstrapController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;

    public FleetBootstrapController(IQueryProcessor queryProcessor)
    {
        _queryProcessor = queryProcessor;
    }

    [HttpGet("dispatcher-ssh-private-key")]
    public async Task<IActionResult> GetDispatcherSshPrivateKey(CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("X-Fleet-Bootstrap-Token", out var tokenValues))
        {
            return Unauthorized();
        }

        var token = tokenValues.ToString();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized();
        }

        var result = await _queryProcessor.RunQueryAsync(
            new GetFleetDispatcherSshPrivateKeyQuery { FleetBootstrapToken = token },
            cancellationToken
        );
        if (result.IsNone)
        {
            return Unauthorized();
        }

        return Ok(new { privateKey = result.Get() });
    }
}
