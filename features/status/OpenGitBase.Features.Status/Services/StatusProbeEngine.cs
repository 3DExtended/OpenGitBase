using System.Diagnostics;
using System.Net.Sockets;
using OpenGitBase.Features.Status.Contracts;

namespace OpenGitBase.Features.Status.Services;

public sealed class StatusProbeEngine
{
    private readonly IHttpClientFactory _httpClientFactory;

    public StatusProbeEngine(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<StatusInstanceSnapshot> ProbeHttpAsync(
        string instanceId,
        string url,
        int timeoutMs,
        int slowThresholdMs,
        CancellationToken cancellationToken
    )
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var client = _httpClientFactory.CreateClient(nameof(StatusProbeEngine));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);

            using var response = await client.GetAsync(url, cts.Token).ConfigureAwait(false);
            stopwatch.Stop();
            var durationMs = stopwatch.ElapsedMilliseconds;

            if (!response.IsSuccessStatusCode)
            {
                return CreateSnapshot(
                    instanceId,
                    checkedAt,
                    durationMs,
                    PublicHealthStatus.Unhealthy,
                    $"HTTP {(int)response.StatusCode}"
                );
            }

            var status =
                durationMs > slowThresholdMs
                    ? PublicHealthStatus.Degraded
                    : PublicHealthStatus.Healthy;
            var message =
                status == PublicHealthStatus.Degraded ? $"Slow response ({durationMs}ms)" : null;

            return CreateSnapshot(instanceId, checkedAt, durationMs, status, message);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return CreateSnapshot(
                instanceId,
                checkedAt,
                stopwatch.ElapsedMilliseconds,
                PublicHealthStatus.Unhealthy,
                $"Probe timeout after {timeoutMs}ms"
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return CreateSnapshot(
                instanceId,
                checkedAt,
                stopwatch.ElapsedMilliseconds,
                PublicHealthStatus.Unhealthy,
                ex.Message
            );
        }
    }

    public async Task<StatusInstanceSnapshot> ProbeTcpAsync(
        string instanceId,
        string host,
        int port,
        int timeoutMs,
        int slowThresholdMs,
        CancellationToken cancellationToken
    )
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);
            await client.ConnectAsync(host, port, cts.Token).ConfigureAwait(false);
            stopwatch.Stop();
            var durationMs = stopwatch.ElapsedMilliseconds;
            var status =
                durationMs > slowThresholdMs
                    ? PublicHealthStatus.Degraded
                    : PublicHealthStatus.Healthy;
            var message =
                status == PublicHealthStatus.Degraded ? $"Slow connect ({durationMs}ms)" : null;
            return CreateSnapshot(instanceId, checkedAt, durationMs, status, message);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return CreateSnapshot(
                instanceId,
                checkedAt,
                stopwatch.ElapsedMilliseconds,
                PublicHealthStatus.Unhealthy,
                $"Connect timeout after {timeoutMs}ms"
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return CreateSnapshot(
                instanceId,
                checkedAt,
                stopwatch.ElapsedMilliseconds,
                PublicHealthStatus.Unhealthy,
                ex.Message
            );
        }
    }

    private static StatusInstanceSnapshot CreateSnapshot(
        string instanceId,
        DateTimeOffset checkedAt,
        long durationMs,
        PublicHealthStatus status,
        string? message
    ) =>
        new()
        {
            InstanceId = instanceId,
            LastCheckedAt = checkedAt,
            ResponseTimeMs = durationMs,
            Status = status,
            Message = message,
        };
}
