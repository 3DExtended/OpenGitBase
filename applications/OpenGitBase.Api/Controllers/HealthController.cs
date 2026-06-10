using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Common.Models.HealthCheck;
using OpenGitBase.Common.Queries.HealthCheck;
using OpenGitBase.Cqrs;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[Route("[controller]")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly ILogger<HealthController> _logger;

    public HealthController(IQueryProcessor queryProcessor, ILogger<HealthController> logger)
    {
        _queryProcessor = queryProcessor;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(HealthCheckReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(
        [FromQuery] bool includeDetails = true,
        [FromQuery] int timeoutMs = 5000,
        [FromQuery] bool runInParallel = true,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var query = new SystemHealthCheckQuery
            {
                IncludeDetails = includeDetails,
                TimeoutMs = timeoutMs,
                RunInParallel = runInParallel,
            };

            var result = await _queryProcessor.RunQueryAsync(query, cancellationToken);

            if (result.IsNone)
            {
                _logger.LogError("Health check query returned no result");
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        Status = HealthStatus.Unhealthy,
                        Message = "Health check failed to execute",
                    }
                );
            }

            var report = result.Get();
            var statusCode = report.Status switch
            {
                HealthStatus.Healthy => StatusCodes.Status200OK,
                HealthStatus.Degraded => StatusCodes.Status200OK,
                HealthStatus.Unhealthy => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status503ServiceUnavailable,
            };

            return StatusCode(statusCode, report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform health check");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    Status = HealthStatus.Unhealthy,
                    Message = "Health check system failure",
                    Exception = ex.Message,
                }
            );
        }
    }
}
