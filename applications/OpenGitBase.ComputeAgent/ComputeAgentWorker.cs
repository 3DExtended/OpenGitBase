using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using OpenGitBase.Features.ComputeNode.Contracts;
using OpenGitBase.Features.Pipeline.Contracts;

namespace OpenGitBase.ComputeAgent;

public sealed class ComputeAgentWorker : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ComputeAgentOptions _options;
    private readonly ISandboxExecutor _sandboxExecutor;
    private readonly ILogger<ComputeAgentWorker> _logger;
    private bool _registered;

    public ComputeAgentWorker(
        IHttpClientFactory httpClientFactory,
        IOptions<ComputeAgentOptions> options,
        ILogger<ComputeAgentWorker> logger
    )
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
        _sandboxExecutor = new ProcessSandboxExecutor();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_options.ApiBaseUrl);
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_registered)
            {
                _registered = await RegisterAsync(client, stoppingToken).ConfigureAwait(false);
                if (!_registered)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
                    continue;
                }
            }

            await SendHeartbeatAsync(client, stoppingToken).ConfigureAwait(false);
            await TryClaimJobAsync(client, stoppingToken).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromSeconds(_options.ClaimPollSeconds), stoppingToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<bool> RegisterAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var response = await client
            .PostAsJsonAsync(
                "api/v1/compute-nodes/register",
                new RegisterComputeNodeQuery
                {
                    NodeId = _options.NodeId,
                    EnrollmentToken = _options.EnrollmentToken,
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    private Task SendHeartbeatAsync(HttpClient client, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(
            "api/v1/compute-nodes/heartbeat",
            new ComputeNodeHeartbeatQuery { NodeId = _options.NodeId, RunningJobs = 0 },
            cancellationToken
        );

    private async Task TryClaimJobAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var response = await client
            .PostAsJsonAsync(
                "pipeline/jobs/claim",
                new ClaimPipelineJobQuery
                {
                    ComputeNodeId = Guid.NewGuid(),
                    HostingProfiles = ["ogb-hosted", "organization-self-hosted", "community-hosted"],
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return;
        }

        var payload = await response.Content
            .ReadFromJsonAsync<ClaimPipelineJobResultDto>(cancellationToken)
            .ConfigureAwait(false);
        if (payload?.Job is null)
        {
            return;
        }

        await client
            .PostAsJsonAsync(
                $"pipeline/jobs/{payload.Job.Id.Value}/status",
                new { status = PipelineJobStatus.Running, message = "Compute agent started job." },
                cancellationToken
            )
            .ConfigureAwait(false);

        var passed = await _sandboxExecutor
            .ExecuteAsync(payload.Job.Script, cancellationToken)
            .ConfigureAwait(false);
        await client
            .PostAsJsonAsync(
                $"pipeline/jobs/{payload.Job.Id.Value}/status",
                new
                {
                    status = passed ? PipelineJobStatus.Passed : PipelineJobStatus.Failed,
                    message = passed
                        ? "ProcessSandboxExecutor completed successfully."
                        : "ProcessSandboxExecutor failed.",
                },
                cancellationToken
            )
            .ConfigureAwait(false);
    }
}
