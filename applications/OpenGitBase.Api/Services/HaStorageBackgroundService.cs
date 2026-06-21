using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Api.Services;

public sealed class HaStorageBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HaStorageBackgroundOptions _options;
    private readonly ILogger<HaStorageBackgroundService> _logger;

    public HaStorageBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<HaStorageBackgroundOptions> options,
        ILogger<HaStorageBackgroundService> logger
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

        var failoverDue = DateTimeOffset.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            try
            {
                if (now >= failoverDue)
                {
                    await RunFailoverAsync(stoppingToken).ConfigureAwait(false);
                    failoverDue = now.AddSeconds(_options.FailoverIntervalSeconds);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "HA storage background cycle failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RunFailoverAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (environment.IsEnvironment("E2ETest"))
        {
            return;
        }

        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        var queryProcessor = scope.ServiceProvider.GetRequiredService<IQueryProcessor>();

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var repositories = await context
            .Set<RepositoryEntity>()
            .AsNoTracking()
            .Where(repository => repository.PrimaryStorageNodeId != null)
            .Select(repository => repository.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var repositoryId in repositories)
        {
            await queryProcessor
                .RunQueryAsync(
                    new PromotePrimaryReplicaQuery
                    {
                        RepositoryId = RepositoryId.From(repositoryId),
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
    }
}
