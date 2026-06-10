using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Models.HealthCheck;
using OpenGitBase.Common.Queries.HealthCheck;
using OpenGitBase.Cqrs;

namespace OpenGitBase.Common.QueryHandlers.HealthCheck;

public class SystemHealthCheckQueryHandler
    : IQueryHandler<SystemHealthCheckQuery, HealthCheckReport>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SystemHealthCheckQueryHandler> _logger;

    public SystemHealthCheckQueryHandler(
        IServiceProvider serviceProvider,
        ILogger<SystemHealthCheckQueryHandler> logger
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<Option<HealthCheckReport>> RunQueryAsync(
        SystemHealthCheckQuery query,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var report = new HealthCheckReport();

        try
        {
            var healthChecks = new List<Func<Task<HealthCheckResult>>>
            {
                () => CheckApplicationRunning(),
                () => CheckDatabaseConnectivity(query.TimeoutMs, cancellationToken),
            };

            if (query.RunInParallel)
            {
                report.Results = (
                    await Task.WhenAll(healthChecks.Select(check => check()))
                ).ToList();
            }
            else
            {
                report.Results = new List<HealthCheckResult>();
                foreach (var check in healthChecks)
                {
                    report.Results.Add(await check());
                }
            }

            if (report.Results.Exists(r => r.Status == HealthStatus.Unhealthy))
            {
                report.Status = HealthStatus.Unhealthy;
            }
            else if (report.Results.Exists(r => r.Status == HealthStatus.Degraded))
            {
                report.Status = HealthStatus.Degraded;
            }
            else
            {
                report.Status = HealthStatus.Healthy;
            }

            report.TotalDurationMs = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation(
                "Health check completed in {Duration}ms with status {Status}",
                report.TotalDurationMs,
                report.Status
            );

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform health check");

            report.Status = HealthStatus.Unhealthy;
            report.TotalDurationMs = stopwatch.ElapsedMilliseconds;
            report.Results.Add(
                new HealthCheckResult
                {
                    Name = "SystemHealthCheck",
                    Status = HealthStatus.Unhealthy,
                    Description = "Health check system failure",
                    Exception = ex.Message,
                    DurationMs = stopwatch.ElapsedMilliseconds,
                }
            );

            return report;
        }
    }

    private static Task<HealthCheckResult> CheckApplicationRunning()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new HealthCheckResult
        {
            Name = "Application",
            Status = HealthStatus.Healthy,
            Description = "Application is running",
            DurationMs = stopwatch.ElapsedMilliseconds,
        };

        return Task.FromResult(result);
    }

    private async Task<HealthCheckResult> CheckDatabaseConnectivity(
        int timeoutMs,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new HealthCheckResult { Name = "DatabaseConnectivity" };

        var contextFactory = _serviceProvider.GetService<IDbContextFactory<OpenGitBaseDbContext>>();
        if (contextFactory == null)
        {
            result.Status = HealthStatus.Healthy;
            result.Description = "Database check skipped (no database configured)";
            result.DurationMs = stopwatch.ElapsedMilliseconds;
            return result;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);

            await using var context = await contextFactory.CreateDbContextAsync(cts.Token);
            var canConnect = await context.Database.CanConnectAsync(cts.Token);

            if (canConnect)
            {
                result.Status = HealthStatus.Healthy;
                result.Description = "Database connection successful";
            }
            else
            {
                result.Status = HealthStatus.Unhealthy;
                result.Description = "Cannot connect to database";
            }

            result.Data["CanConnect"] = canConnect;
            result.Data["DatabaseProvider"] = context.Database.ProviderName ?? "Unknown";
        }
        catch (OperationCanceledException)
        {
            result.Status = HealthStatus.Unhealthy;
            result.Description = $"Database connectivity check timed out after {timeoutMs}ms";
        }
        catch (Exception ex)
        {
            result.Status = HealthStatus.Unhealthy;
            result.Description = "Database connectivity check failed";
            result.Exception = ex.Message;
        }

        result.DurationMs = stopwatch.ElapsedMilliseconds;
        return result;
    }
}
