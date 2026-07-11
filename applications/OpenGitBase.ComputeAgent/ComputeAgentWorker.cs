using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using OpenGitBase.Common.Options;
using OpenGitBase.Features.ComputeNode.Contracts;
using OpenGitBase.Features.Pipeline.Contracts;

namespace OpenGitBase.ComputeAgent;

public sealed class ComputeAgentWorker : BackgroundService
{
    private const int MaxLogLinesPerUpdate = 200;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ComputeAgentOptions _options;
    private readonly ISandboxExecutor _sandboxExecutor;
    private readonly KafkaOptions _kafkaOptions;
    private readonly ILogger<ComputeAgentWorker> _logger;
    private readonly SemaphoreSlim _claimWakeSignal = new(0, 1);
    private bool _registered;
    private ComputeNodeDto? _node;

    public ComputeAgentWorker(
        IHttpClientFactory httpClientFactory,
        IOptions<ComputeAgentOptions> options,
        IOptions<KafkaOptions> kafkaOptions,
        ILogger<ComputeAgentWorker> logger
    )
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _kafkaOptions = kafkaOptions.Value;
        _logger = logger;
        _sandboxExecutor = new ProcessSandboxExecutor();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_options.ApiBaseUrl);
        Task? kafkaWakeLoop = null;
        if (_options.HostingProfiles.Contains("ogb-hosted", StringComparer.OrdinalIgnoreCase))
        {
            kafkaWakeLoop = RunKafkaWakeLoopAsync(stoppingToken);
        }

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
            await WaitForClaimWakeAsync(stoppingToken).ConfigureAwait(false);
        }

        if (kafkaWakeLoop is not null)
        {
            await kafkaWakeLoop.ConfigureAwait(false);
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
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        _node = await response.Content
            .ReadFromJsonAsync<ComputeNodeDto>(cancellationToken)
            .ConfigureAwait(false);
        return _node is not null;
    }

    private Task SendHeartbeatAsync(HttpClient client, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(
            "api/v1/compute-nodes/heartbeat",
            new ComputeNodeHeartbeatQuery { NodeId = _options.NodeId, RunningJobs = 0 },
            cancellationToken
        );

    private async Task TryClaimJobAsync(HttpClient client, CancellationToken cancellationToken)
    {
        if (_node is null)
        {
            return;
        }

        var response = await client
            .PostAsJsonAsync(
                "pipeline/jobs/claim",
                new ClaimPipelineJobQuery
                {
                    ComputeNodeId = _node.Id.Value,
                    HostingProfiles = _options.HostingProfiles,
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

        var env = ParseEnvironment(payload.Job.EnvironmentJson);
        var workspacePath = await MaterializeWorkspaceAsync(payload.Job, env, cancellationToken)
            .ConfigureAwait(false);

        await ExecuteDependencyInstallsAsync(client, payload.Job, payload.Job.Id.Value, workspacePath, env, cancellationToken)
            .ConfigureAwait(false);

        await PostJobStatusAsync(
            client,
            payload.Job.Id.Value,
            PipelineJobStatus.Running,
            "Compute agent started job.",
            "workspace",
            [$"Workspace prepared at {workspacePath}."],
            cancellationToken
        ).ConfigureAwait(false);

        using var timeoutCts = payload.Job.TimeoutSeconds > 0
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;
        if (timeoutCts is not null)
        {
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(payload.Job.TimeoutSeconds));
        }

        var execution = await _sandboxExecutor
            .ExecuteAsync(
                payload.Job.Script,
                workspacePath,
                env,
                timeoutCts?.Token ?? cancellationToken
            )
            .ConfigureAwait(false);
        await PostJobStatusAsync(
            client,
            payload.Job.Id.Value,
            execution.Success ? PipelineJobStatus.Passed : PipelineJobStatus.Failed,
            execution.Success
                ? "ProcessSandboxExecutor completed successfully."
                : $"ProcessSandboxExecutor failed with exit code {execution.ExitCode}.",
            "script",
            BuildExecutionLogLines(execution),
            cancellationToken
        ).ConfigureAwait(false);
    }

    private async Task<string> MaterializeWorkspaceAsync(
        PipelineJobDto job,
        Dictionary<string, string> env,
        CancellationToken cancellationToken
    )
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), "opengitbase-agent", job.Id.Value.ToString("N"));
        Directory.CreateDirectory(workspaceRoot);
        var projectDir = Path.Combine(workspaceRoot, "repo");

        if (!env.TryGetValue("CI_REPOSITORY_GIT_DIR", out var repositoryPath) || string.IsNullOrWhiteSpace(repositoryPath))
        {
            Directory.CreateDirectory(projectDir);
            env["CI_PROJECT_DIR"] = projectDir;
            return projectDir;
        }

        await RunGitCommandAsync(
            $"clone {(job.GitDepth > 0 ? $"--depth {job.GitDepth} " : string.Empty)}\"{repositoryPath}\" \"{projectDir}\"",
            workspaceRoot,
            cancellationToken
        ).ConfigureAwait(false);

        if (env.TryGetValue("CI_COMMIT_SHA", out var commitSha) && !string.IsNullOrWhiteSpace(commitSha))
        {
            await RunGitCommandAsync(
                $"checkout \"{commitSha}\"",
                projectDir,
                cancellationToken
            ).ConfigureAwait(false);
        }

        env["CI_PROJECT_DIR"] = projectDir;
        return projectDir;
    }

    private async Task RunGitCommandAsync(
        string args,
        string workingDirectory,
        CancellationToken cancellationToken
    )
    {
        var info = new System.Diagnostics.ProcessStartInfo("sh")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            WorkingDirectory = workingDirectory,
        };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add($"git {args}");
        using var process = System.Diagnostics.Process.Start(info);
        if (process is null)
        {
            return;
        }

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteDependencyInstallsAsync(
        HttpClient client,
        PipelineJobDto job,
        Guid jobId,
        string workspacePath,
        IReadOnlyDictionary<string, string> env,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(job.ResolvedSpecJson))
        {
            return;
        }

        using var document = JsonDocument.Parse(job.ResolvedSpecJson);
        if (
            !document.RootElement.TryGetProperty("Dependencies", out var dependencies)
            || dependencies.ValueKind != JsonValueKind.Array
        )
        {
            return;
        }

        foreach (var dependency in dependencies.EnumerateArray())
        {
            if (
                !dependency.TryGetProperty("InstallScript", out var installScript)
                || string.IsNullOrWhiteSpace(installScript.GetString())
            )
            {
                continue;
            }

            var recipeKey = BuildRecipeKey(job, dependency);
            var result = await _sandboxExecutor
                .ExecuteAsync(installScript.GetString()!, workspacePath, env, cancellationToken)
                .ConfigureAwait(false);
            await client
                .PostAsJsonAsync(
                    $"pipeline/jobs/{jobId}/dependency-install-outcomes",
                    new
                    {
                        recipeKey,
                        success = result.Success,
                        exitCode = result.ExitCode,
                        durationMs = result.DurationMs,
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);

            await PostJobStatusAsync(
                client,
                jobId,
                PipelineJobStatus.Running,
                result.Success
                    ? $"Dependency install succeeded for {recipeKey}."
                    : $"Dependency install failed for {recipeKey} (exit {result.ExitCode}).",
                "install",
                BuildExecutionLogLines(result),
                cancellationToken
            ).ConfigureAwait(false);
        }
    }

    private string BuildRecipeKey(PipelineJobDto job, JsonElement dependency)
    {
        var dependencyName = dependency.TryGetProperty("Name", out var name) ? name.GetString() : "dependency";
        return $"{job.RunsOn}:{dependencyName}:{job.Id.Value:D}";
    }

    private Dictionary<string, string> ParseEnvironment(string environmentJson)
    {
        if (string.IsNullOrWhiteSpace(environmentJson))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(environmentJson)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> BuildExecutionLogLines(SandboxExecutionResult result)
    {
        var lines = new List<string>(MaxLogLinesPerUpdate + 2);
        if (!string.IsNullOrWhiteSpace(result.StdOut))
        {
            lines.AddRange(SplitLines(result.StdOut));
        }

        if (!string.IsNullOrWhiteSpace(result.StdErr))
        {
            lines.AddRange(SplitLines(result.StdErr).Select(line => $"stderr: {line}"));
        }

        lines.Add($"exit_code={result.ExitCode}");
        lines.Add($"duration_ms={result.DurationMs}");
        return lines.Take(MaxLogLinesPerUpdate).ToList();
    }

    private static IEnumerable<string> SplitLines(string value) =>
        value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static Task PostJobStatusAsync(
        HttpClient client,
        Guid jobId,
        PipelineJobStatus status,
        string message,
        string logSection,
        IReadOnlyList<string> logLines,
        CancellationToken cancellationToken
    ) =>
        client.PostAsJsonAsync(
            $"pipeline/jobs/{jobId}/status",
            new
            {
                status,
                message,
                logSection,
                logLines,
            },
            cancellationToken
        );

    private async Task WaitForClaimWakeAsync(CancellationToken cancellationToken)
    {
        using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var delayTask = Task.Delay(TimeSpan.FromSeconds(_options.ClaimPollSeconds), delayCts.Token);
        var wakeTask = _claimWakeSignal.WaitAsync(cancellationToken);
        var completed = await Task.WhenAny(delayTask, wakeTask).ConfigureAwait(false);
        if (completed == wakeTask)
        {
            delayCts.Cancel();
            while (_claimWakeSignal.CurrentCount > 0)
            {
                await _claimWakeSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task RunKafkaWakeLoopAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_kafkaOptions.BootstrapServers))
        {
            return;
        }

        var config = new ConsumerConfig
        {
            BootstrapServers = _kafkaOptions.BootstrapServers,
            GroupId = $"{_options.NodeId}-ci-agent",
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = true,
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(_kafkaOptions.JobAvailableTopic);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var message = consumer.Consume(stoppingToken);
                if (message is not null)
                {
                    if (_claimWakeSignal.CurrentCount == 0)
                    {
                        _claimWakeSignal.Release();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Kafka wake consumer error; retrying.");
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
