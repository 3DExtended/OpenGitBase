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
        CancellationToken cancellationToken
    )
    {
        var runAsUser = environment.TryGetValue("OGB_SANDBOX_USER", out var user) && !string.IsNullOrWhiteSpace(user)
            ? user
            : "ogb";

        var result = await _launcher
            .LaunchAsync(
                new FirecrackerLaunchRequest
                {
                    Script = script,
                    WorkingDirectory = workingDirectory,
                    Environment = environment,
                    RunAsUser = runAsUser,
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
}
