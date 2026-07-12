namespace OpenGitBase.ComputeAgent;

public sealed class FirecrackerSandboxExecutor : ISandboxExecutor
{
    private readonly IFirecrackerLauncher _launcher;

    public FirecrackerSandboxExecutor(IFirecrackerLauncher launcher)
    {
        _launcher = launcher;
    }

    public async Task<SandboxExecutionResult> ExecuteAsync(
        string script,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        CancellationToken cancellationToken,
        Action<string>? onLogLine = null
    )
    {
        var runAsUser = ResolveExecutionUser(environment);
        environment.TryGetValue("OGB_ROOTFS", out var rootFs);
        environment.TryGetValue("OGB_WORKSPACE_SHARE", out var workspaceShare);
        var resourceLimits = FirecrackerResourceLimits.FromEnvironment(environment);
        var egressAllowlist = environment.TryGetValue("OGB_EGRESS_ALLOWLIST", out var allowlistRaw)
            ? allowlistRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : Array.Empty<string>();

        var result = await _launcher
            .LaunchAsync(
                new FirecrackerLaunchRequest
                {
                    Script = script,
                    WorkingDirectory = workingDirectory,
                    Environment = environment,
                    RunAsUser = runAsUser,
                    RootFsPath = rootFs,
                    WorkspaceSharePath = workspaceShare,
                    ResourceLimits = resourceLimits,
                    EgressAllowlist = egressAllowlist,
                    OnOutputLine = onLogLine,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return new SandboxExecutionResult
        {
            Success = result.Success,
            ExitCode = result.ExitCode,
            DurationMs = result.DurationMs,
            StdOut = result.StdOut,
            StdErr = result.StdErr,
        };
    }

    private static string ResolveExecutionUser(IReadOnlyDictionary<string, string> environment)
    {
        if (
            environment.TryGetValue("CI_JOB_EXECUTION_USER", out var executionUser)
            && !string.IsNullOrWhiteSpace(executionUser)
        )
        {
            return executionUser;
        }

        if (
            environment.TryGetValue("OGB_SANDBOX_USER", out var legacyUser)
            && !string.IsNullOrWhiteSpace(legacyUser)
        )
        {
            return legacyUser;
        }

        return "ogb";
    }
}
