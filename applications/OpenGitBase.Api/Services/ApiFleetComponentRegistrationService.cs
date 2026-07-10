using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Status.Contracts;

namespace OpenGitBase.Api.Services;

public sealed class ApiFleetComponentRegistrationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FleetComponentOptions _options;
    private readonly ILogger<ApiFleetComponentRegistrationService> _logger;

    public ApiFleetComponentRegistrationService(
        IServiceScopeFactory scopeFactory,
        IOptions<FleetComponentOptions> options,
        ILogger<ApiFleetComponentRegistrationService> logger
    )
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || !_options.SelfRegistrationEnabled)
        {
            return;
        }

        var instanceId = ResolveInstanceId();
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            _logger.LogWarning(
                "Fleet component self-registration skipped because InstanceId is not configured"
            );
            return;
        }

        await RegisterAsync(instanceId, stoppingToken).ConfigureAwait(false);

        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.HeartbeatIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await SendHeartbeatAsync(instanceId, stoppingToken).ConfigureAwait(false);
        }
    }

    private string ResolveInstanceId()
    {
        if (!string.IsNullOrWhiteSpace(_options.InstanceId))
        {
            return _options.InstanceId.Trim();
        }

        return Environment.GetEnvironmentVariable("HOSTNAME")?.Trim()
            ?? Environment.MachineName;
    }

    private async Task RegisterAsync(string instanceId, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<IQueryProcessor>();

        var result = await processor
            .RunQueryAsync(
                new RegisterFleetComponentQuery
                {
                    ComponentType = FleetComponentType.Api,
                    InstanceId = instanceId,
                    ProbeUrl = ResolveProbeUrl(instanceId),
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (result.IsNone)
        {
            _logger.LogWarning(
                "Fleet component registration failed for API instance {InstanceId}",
                instanceId
            );
            return;
        }

        _logger.LogInformation(
            "Registered API fleet component {InstanceId} with probe URL {ProbeUrl}",
            instanceId,
            ResolveProbeUrl(instanceId)
        );
    }

    private string ResolveProbeUrl(string instanceId)
    {
        var configured = _options.ProbeUrl?.Trim();
        if (
            !string.IsNullOrWhiteSpace(configured)
            && !configured.Contains("127.0.0.1", StringComparison.Ordinal)
            && !configured.Contains("localhost", StringComparison.OrdinalIgnoreCase)
        )
        {
            return configured;
        }

        return $"http://{instanceId}:8080/health";
    }

    private async Task SendHeartbeatAsync(string instanceId, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<IQueryProcessor>();

        var result = await processor
            .RunQueryAsync(
                new FleetComponentHeartbeatQuery
                {
                    ComponentType = FleetComponentType.Api,
                    InstanceId = instanceId,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (result.IsNone)
        {
            _logger.LogWarning(
                "Fleet component heartbeat failed for API instance {InstanceId}; re-registering",
                instanceId
            );
            await RegisterAsync(instanceId, cancellationToken).ConfigureAwait(false);
        }
    }
}
