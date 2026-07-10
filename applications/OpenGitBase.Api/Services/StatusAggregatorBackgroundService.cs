using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenGitBase.Common.Options;
using OpenGitBase.Features.Status.Services;

namespace OpenGitBase.Api.Services;

public sealed class StatusAggregatorBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly StatusProbeOptions _options;
    private readonly ILogger<StatusAggregatorBackgroundService> _logger;

    public StatusAggregatorBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<StatusProbeOptions> options,
        ILogger<StatusAggregatorBackgroundService> logger
    )
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.IntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var aggregator = scope.ServiceProvider.GetRequiredService<StatusAggregatorService>();
                await aggregator.TryRunAggregationCycleAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Status aggregation cycle failed");
            }
        }
    }
}
