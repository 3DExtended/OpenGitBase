using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using OpenGitBase.Common.Options;
using OpenGitBase.Features.ComputeNode.Contracts;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Pipeline;

namespace OpenGitBase.ComputeAgent;

public sealed class ComputeAgentWorker : BackgroundService
{
    private const int MaxLogLinesPerUpdate = 200;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ComputeAgentOptions _options;
    private readonly ISandboxExecutor _sandboxExecutor;
    private readonly IBaseImageArtifactResolver _baseImageResolver;
    private readonly IOverlayFsStackAssembler _overlayStackAssembler;
    private readonly IHostEgressEnforcer _egressEnforcer;
    private readonly IPromotedDependencyLayerResolver _promotedLayerResolver;
    private readonly KafkaOptions _kafkaOptions;
    private readonly ILogger<ComputeAgentWorker> _logger;
    private readonly SemaphoreSlim _claimWakeSignal = new(0, 1);
    private bool _registered;
    private ComputeNodeDto? _node;
    private string? _nodeIdentityToken;
    private int _runningJobs;

    public ComputeAgentWorker(
        IHttpClientFactory httpClientFactory,
        IOptions<ComputeAgentOptions> options,
        IOptions<KafkaOptions> kafkaOptions,
        IBaseImageArtifactResolver baseImageResolver,
        IOverlayFsStackAssembler overlayStackAssembler,
        IHostEgressEnforcer egressEnforcer,
        IPromotedDependencyLayerResolver promotedLayerResolver,
        ISandboxExecutor sandboxExecutor,
        ILogger<ComputeAgentWorker> logger
    )
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _kafkaOptions = kafkaOptions.Value;
        _logger = logger;
        _baseImageResolver = baseImageResolver;
        _overlayStackAssembler = overlayStackAssembler;
        _egressEnforcer = egressEnforcer;
        _promotedLayerResolver = promotedLayerResolver;
        _sandboxExecutor = sandboxExecutor;
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

                ApplyNodeIdentity(client);
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

    private static string? TryGetImageSlug(string? resolvedSpecJson)
    {
        if (string.IsNullOrWhiteSpace(resolvedSpecJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(resolvedSpecJson);
        if (
            document.RootElement.TryGetProperty("Image", out var imageElement)
            && !string.IsNullOrWhiteSpace(imageElement.GetString())
        )
        {
            return imageElement.GetString();
        }

        return null;
    }

    private static Guid? TryGetOrganizationId(IReadOnlyDictionary<string, string> env)
    {
        if (
            env.TryGetValue("CI_ORGANIZATION_ID", out var organizationId)
            && Guid.TryParse(organizationId, out var parsed)
        )
        {
            return parsed;
        }

        return null;
    }

    private static IEnumerable<string> ExtractNetworkDomains(string script)
    {
        foreach (var token in script.Split([' ', '\t', '\r', '\n', '"', '\''], StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || token.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (Uri.TryCreate(token, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
                {
                    yield return uri.Host;
                }
            }
        }
    }

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

        var payload = await response.Content
            .ReadFromJsonAsync<RegisterComputeNodeResultDto>(cancellationToken)
            .ConfigureAwait(false);
        if (payload?.Node is null || string.IsNullOrWhiteSpace(payload.NodeIdentityToken))
        {
            return false;
        }

        _node = payload.Node;
        _nodeIdentityToken = payload.NodeIdentityToken;
        return true;
    }

    private void ApplyNodeIdentity(HttpClient client)
    {
        if (string.IsNullOrWhiteSpace(_nodeIdentityToken))
        {
            return;
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _nodeIdentityToken
        );
    }

    private Task SendHeartbeatAsync(HttpClient client, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(
            "api/v1/compute-nodes/heartbeat",
            new ComputeNodeHeartbeatQuery { NodeId = _options.NodeId, RunningJobs = _runningJobs },
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

        Interlocked.Increment(ref _runningJobs);
        try
        {
            await ExecuteClaimedJobAsync(client, payload, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref _runningJobs);
        }
    }

    private async Task ExecuteClaimedJobAsync(
        HttpClient client,
        ClaimPipelineJobResultDto payload,
        CancellationToken cancellationToken
    )
    {
        var env = ParseEnvironment(payload.Job.EnvironmentJson);
        var imageSlug = TryGetImageSlug(payload.Job.ResolvedSpecJson);
        string? overlayRoot = null;
        if (!string.IsNullOrWhiteSpace(imageSlug))
        {
            var fetch = await _baseImageResolver
                .FetchAsync(imageSlug, client, cancellationToken)
                .ConfigureAwait(false);
            if (!fetch.Success)
            {
                await PostJobStatusAsync(
                    client,
                    payload.Job.Id.Value,
                    PipelineJobStatus.Failed,
                    fetch.ErrorMessage ?? "Base image preparation failed.",
                    "layer",
                    [fetch.ErrorMessage ?? "Unknown base image slug."],
                    cancellationToken
                ).ConfigureAwait(false);
                return;
            }

            var dependencyLayers = await ResolvePromotedDependencyLayersAsync(
                client,
                payload.Job,
                cancellationToken
            ).ConfigureAwait(false);
            var stack = await _overlayStackAssembler
                .AssembleAsync(
                    new OverlayFsStackRequest
                    {
                        JobId = payload.Job.Id.Value,
                        BaseImageArtifactPath = fetch.LocalPath!,
                        DependencyLayerPaths = dependencyLayers,
                        WorkRoot = Path.Combine(Path.GetTempPath(), "opengitbase-agent"),
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (!stack.Success)
            {
                await PostJobStatusAsync(
                    client,
                    payload.Job.Id.Value,
                    PipelineJobStatus.Failed,
                    stack.ErrorMessage ?? "OverlayFS stack assembly failed.",
                    "layer",
                    stack.LogLines.Concat([stack.ErrorMessage ?? "layer mount failed"]).ToList(),
                    cancellationToken
                ).ConfigureAwait(false);
                return;
            }

            overlayRoot = stack.MergedRootPath;
            env["OGB_ROOTFS"] = overlayRoot!;
            await PostJobStatusAsync(
                client,
                payload.Job.Id.Value,
                PipelineJobStatus.Running,
                "OverlayFS stack assembled.",
                "layer",
                stack.LogLines,
                cancellationToken
            ).ConfigureAwait(false);
        }

        var organizationId = TryGetOrganizationId(env);
        var allowlist = await _egressEnforcer
            .ResolveAllowlistAsync(client, payload.Job.RunsOn, organizationId, cancellationToken)
            .ConfigureAwait(false);
        var egressViolation = await ValidateScriptEgressAsync(payload.Job.Script, allowlist, cancellationToken)
            .ConfigureAwait(false);
        if (egressViolation is not null)
        {
            await PostJobStatusAsync(
                client,
                payload.Job.Id.Value,
                PipelineJobStatus.Failed,
                "Egress policy blocked job script.",
                "script",
                [egressViolation],
                cancellationToken
            ).ConfigureAwait(false);
            if (overlayRoot is not null)
            {
                await _overlayStackAssembler.TeardownAsync(overlayRoot, cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        var workspacePath = await MaterializeWorkspaceAsync(
            client,
            payload.Job,
            payload.JobIdentityToken,
            env,
            cancellationToken
        )
            .ConfigureAwait(false);

        var installsSucceeded = await ExecuteDependencyInstallsAsync(
            client,
            payload.Job,
            payload.Job.Id.Value,
            workspacePath,
            env,
            allowlist,
            cancellationToken
        ).ConfigureAwait(false);
        if (!installsSucceeded)
        {
            if (overlayRoot is not null)
            {
                await _overlayStackAssembler.TeardownAsync(overlayRoot, cancellationToken).ConfigureAwait(false);
            }

            return;
        }

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
        var executorLabel = _options.PreferProcessSandbox ? "ProcessSandboxExecutor" : "FirecrackerSandboxExecutor";
        await PostJobStatusAsync(
            client,
            payload.Job.Id.Value,
            execution.Success ? PipelineJobStatus.Passed : PipelineJobStatus.Failed,
            execution.Success
                ? $"{executorLabel} completed successfully."
                : $"{executorLabel} failed with exit code {execution.ExitCode}.",
            "script",
            BuildExecutionLogLines(execution),
            cancellationToken
        ).ConfigureAwait(false);
        if (overlayRoot is not null)
        {
            await _overlayStackAssembler.TeardownAsync(overlayRoot, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<string> MaterializeWorkspaceAsync(
        HttpClient nodeClient,
        PipelineJobDto job,
        string jobIdentityToken,
        Dictionary<string, string> env,
        CancellationToken cancellationToken
    )
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), "opengitbase-agent", job.Id.Value.ToString("N"));
        Directory.CreateDirectory(workspaceRoot);
        var projectDir = Path.Combine(workspaceRoot, "repo");
        Directory.CreateDirectory(projectDir);

        if (string.IsNullOrWhiteSpace(jobIdentityToken))
        {
            env["CI_PROJECT_DIR"] = projectDir;
            return projectDir;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"pipeline/jobs/{job.Id.Value}/workspace-archive"
        );
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jobIdentityToken);
        var response = await nodeClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            env["CI_PROJECT_DIR"] = projectDir;
            return projectDir;
        }

        var archivePath = Path.Combine(workspaceRoot, "workspace.tar.gz");
        await using (var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false))
        await using (var archiveFile = File.Create(archivePath))
        {
            await responseStream.CopyToAsync(archiveFile, cancellationToken).ConfigureAwait(false);
        }

        await RunGitCommandAsync($"tar -xzf \"{archivePath}\" -C \"{projectDir}\"", workspaceRoot, cancellationToken)
            .ConfigureAwait(false);
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

    private async Task<bool> ExecuteDependencyInstallsAsync(
        HttpClient client,
        PipelineJobDto job,
        Guid jobId,
        string workspacePath,
        IReadOnlyDictionary<string, string> env,
        IReadOnlyList<string> allowlist,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(job.ResolvedSpecJson))
        {
            return true;
        }

        using var document = JsonDocument.Parse(job.ResolvedSpecJson);
        if (
            !document.RootElement.TryGetProperty("Dependencies", out var dependencies)
            || dependencies.ValueKind != JsonValueKind.Array
        )
        {
            return true;
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
            var promoted = await _promotedLayerResolver
                .FetchAsync(recipeKey, client, cancellationToken)
                .ConfigureAwait(false);
            if (promoted.Success)
            {
                await PostJobStatusAsync(
                    client,
                    jobId,
                    PipelineJobStatus.Running,
                    $"Promoted layer cache hit for {recipeKey}.",
                    "install",
                    [$"Skipping live installscript; using layer {promoted.Artifact?.ContentHash}."],
                    cancellationToken
                ).ConfigureAwait(false);
                continue;
            }

            var egressViolation = await ValidateScriptEgressAsync(
                installScript.GetString()!,
                allowlist,
                cancellationToken
            ).ConfigureAwait(false);
            if (egressViolation is not null)
            {
                await PostJobStatusAsync(
                    client,
                    jobId,
                    PipelineJobStatus.Failed,
                    "Egress policy blocked dependency install.",
                    "install",
                    [egressViolation],
                    cancellationToken
                ).ConfigureAwait(false);
                return false;
            }

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
                result.Success ? PipelineJobStatus.Running : PipelineJobStatus.Failed,
                result.Success
                    ? $"Dependency install succeeded for {recipeKey}."
                    : $"Dependency install failed for {recipeKey} (exit {result.ExitCode}).",
                "install",
                BuildExecutionLogLines(result),
                cancellationToken
            ).ConfigureAwait(false);

            if (!result.Success)
            {
                return false;
            }
        }

        return true;
    }

    private async Task<IReadOnlyList<string>> ResolvePromotedDependencyLayersAsync(
        HttpClient client,
        PipelineJobDto job,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(job.ResolvedSpecJson))
        {
            return [];
        }

        using var document = JsonDocument.Parse(job.ResolvedSpecJson);
        if (
            !document.RootElement.TryGetProperty("Dependencies", out var dependencies)
            || dependencies.ValueKind != JsonValueKind.Array
        )
        {
            return [];
        }

        var paths = new List<string>();
        foreach (var dependency in dependencies.EnumerateArray())
        {
            var recipeKey = BuildRecipeKey(job, dependency);
            var promoted = await _promotedLayerResolver
                .FetchAsync(recipeKey, client, cancellationToken)
                .ConfigureAwait(false);
            if (promoted.Success && !string.IsNullOrWhiteSpace(promoted.LocalPath))
            {
                paths.Add(promoted.LocalPath);
            }
        }

        return paths;
    }

    private async Task<string?> ValidateScriptEgressAsync(
        string script,
        IReadOnlyList<string> allowlist,
        CancellationToken cancellationToken
    )
    {
        foreach (var domain in ExtractNetworkDomains(script))
        {
            var check = await _egressEnforcer
                .ValidateDomainAsync(domain, allowlist, cancellationToken)
                .ConfigureAwait(false);
            if (!check.Allowed)
            {
                return check.DenialLogLine;
            }
        }

        return null;
    }

    private string ResolveBaseSlug(PipelineJobDto job)
    {
        if (string.IsNullOrWhiteSpace(job.ResolvedSpecJson))
        {
            return "unknown";
        }

        using var document = JsonDocument.Parse(job.ResolvedSpecJson);
        if (
            document.RootElement.TryGetProperty("Image", out var image)
            && !string.IsNullOrWhiteSpace(image.GetString())
        )
        {
            return image.GetString()!;
        }

        return "unknown";
    }

    private string BuildRecipeKey(PipelineJobDto job, JsonElement dependency)
    {
        var installScript = dependency.TryGetProperty("InstallScript", out var scriptElement)
            ? scriptElement.GetString() ?? string.Empty
            : string.Empty;
        return DependencyRecipeKeys.Compute(ResolveBaseSlug(job), installScript);
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
