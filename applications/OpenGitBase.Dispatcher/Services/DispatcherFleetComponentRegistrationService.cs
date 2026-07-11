using System.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenGitBase.Dispatcher.Options;

namespace OpenGitBase.Dispatcher.Services;

public sealed class DispatcherFleetComponentRegistrationService : BackgroundService
{
    private readonly FleetComponentRegistrationClient _client;
    private readonly DispatcherOptions _options;
    private readonly ILogger<DispatcherFleetComponentRegistrationService> _logger;

    public DispatcherFleetComponentRegistrationService(
        FleetComponentRegistrationClient client,
        IOptions<DispatcherOptions> options,
        ILogger<DispatcherFleetComponentRegistrationService> logger
    )
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.FleetSelfRegistrationEnabled)
        {
            return;
        }

        var instanceId = _options.DispatcherId;
        var probeUrl = ResolveProbeUrl(instanceId);

        await RegisterAsync(instanceId, probeUrl, stoppingToken).ConfigureAwait(false);

        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.FleetHeartbeatIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            var heartbeatStatus = await _client
                .HeartbeatAsync(instanceId, stoppingToken)
                .ConfigureAwait(false);
            if (!IsSuccess(heartbeatStatus))
            {
                await RegisterAsync(instanceId, probeUrl, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private static bool IsSuccess(HttpStatusCode status) =>
        (int)status is >= 200 and < 300;

    private async Task RegisterAsync(
        string instanceId,
        string probeUrl,
        CancellationToken cancellationToken
    )
    {
        var status = await _client
            .RegisterAsync(instanceId, probeUrl, cancellationToken)
            .ConfigureAwait(false);
        if (!IsSuccess(status))
        {
            _logger.LogWarning(
                "Fleet component registration failed for dispatcher {InstanceId}: HTTP {StatusCode}. "
                    + "Ensure API replicas expose /api/v1/internal/fleet-components before rolling dispatchers.",
                instanceId,
                (int)status
            );
            return;
        }

        _logger.LogInformation(
            "Registered git fleet component {InstanceId} with probe URL {ProbeUrl}",
            instanceId,
            probeUrl
        );
    }

    private string ResolveProbeUrl(string instanceId)
    {
        var configured = _options.FleetProbeUrl?.Trim();
        if (
            !string.IsNullOrWhiteSpace(configured)
            && !configured.Contains("127.0.0.1", StringComparison.Ordinal)
            && !configured.Contains("localhost", StringComparison.OrdinalIgnoreCase)
        )
        {
            return configured;
        }

        return $"http://{instanceId}:{_options.HttpPort}/health";
    }
}
