using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenGitBase.Api.Models;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Status.Contracts;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[AllowAnonymous]
[EnableRateLimiting("sensitive")]
[Route("api/v1/internal/fleet-components")]
public sealed class FleetComponentRegistryController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;

    public FleetComponentRegistryController(IQueryProcessor queryProcessor)
    {
        _queryProcessor = queryProcessor;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterFleetComponentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RegisterFleetComponentResponse>> Register(
        [FromBody] RegisterFleetComponentRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryParseComponentType(request.ComponentType, out var componentType))
        {
            ModelState.AddModelError(nameof(request.ComponentType), "ComponentType is invalid.");
        }

        if (string.IsNullOrWhiteSpace(request.InstanceId))
        {
            ModelState.AddModelError(nameof(request.InstanceId), "InstanceId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ProbeUrl))
        {
            ModelState.AddModelError(nameof(request.ProbeUrl), "ProbeUrl is required.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _queryProcessor.RunQueryAsync(
            new RegisterFleetComponentQuery
            {
                ComponentType = componentType,
                InstanceId = request.InstanceId,
                ProbeUrl = request.ProbeUrl,
                Version = request.Version,
            },
            cancellationToken
        );

        if (result.IsNone)
        {
            return BadRequest(new { error = "Fleet component registration failed." });
        }

        var payload = result.Get();
        return Ok(
            new RegisterFleetComponentResponse
            {
                FleetComponentId = payload.FleetComponentId.Value,
                HeartbeatIntervalSeconds = payload.HeartbeatIntervalSeconds,
            }
        );
    }

    [HttpPost("heartbeat")]
    [ProducesResponseType(typeof(FleetComponentHeartbeatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FleetComponentHeartbeatResponse>> Heartbeat(
        [FromBody] FleetComponentHeartbeatRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryParseComponentType(request.ComponentType, out var componentType))
        {
            return BadRequest(new { error = "ComponentType is invalid." });
        }

        if (string.IsNullOrWhiteSpace(request.InstanceId))
        {
            return BadRequest(new { error = "InstanceId is required." });
        }

        var result = await _queryProcessor.RunQueryAsync(
            new FleetComponentHeartbeatQuery
            {
                ComponentType = componentType,
                InstanceId = request.InstanceId,
            },
            cancellationToken
        );

        if (result.IsNone)
        {
            return NotFound();
        }

        return Ok(new FleetComponentHeartbeatResponse { Acknowledged = result.Get().Acknowledged });
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FleetComponentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FleetComponentDto>>> List(
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor.RunQueryAsync(
            new ListFleetComponentsQuery(),
            cancellationToken
        );

        return Ok(result.IsSome ? result.Get() : Array.Empty<FleetComponentDto>());
    }

    private static bool TryParseComponentType(string value, out FleetComponentType componentType)
    {
        return Enum.TryParse(value, ignoreCase: true, out componentType);
    }
}
