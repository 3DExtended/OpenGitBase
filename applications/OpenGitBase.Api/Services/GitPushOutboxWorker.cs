using OpenGitBase.Features.Pipeline.Services;

namespace OpenGitBase.Api.Services;

public sealed class GitPushOutboxWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GitPushOutboxWorker> _logger;

    public GitPushOutboxWorker(
        IServiceProvider serviceProvider,
        ILogger<GitPushOutboxWorker> logger
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _serviceProvider.CreateAsyncScope();
                var drain = scope.ServiceProvider.GetRequiredService<GitPushOutboxDrainService>();
                await drain.DrainPendingAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Git push outbox drain failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
        }
    }
}
