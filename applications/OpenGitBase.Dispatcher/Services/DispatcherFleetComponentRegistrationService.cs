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
        var probeUrl =
            _options.FleetProbeUrl ?? $"http://127.0.0.1:{_options.HttpPort}/health";

        await RegisterAsync(instanceId, probeUrl, stoppingToken).ConfigureAwait(false);

        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.FleetHeartbeatIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            var acknowledged = await _client
                .HeartbeatAsync(instanceId, stoppingToken)
                .ConfigureAwait(false);
            if (!acknowledged)
            {
                await RegisterAsync(instanceId, probeUrl, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task RegisterAsync(
        string instanceId,
        string probeUrl,
        CancellationToken cancellationToken
    )
    {
        var registered = await _client
            .RegisterAsync(instanceId, probeUrl, cancellationToken)
            .ConfigureAwait(false);
        if (!registered)
        {
            _logger.LogWarning(
                "Fleet component registration failed for dispatcher {InstanceId}",
                instanceId
            );
            return;
        }

        _logger.LogInformation(
            "Registered git fleet component {InstanceId} with probe URL {ProbeUrl}",
            instanceId,
            probeUrl
        );
    }
}
