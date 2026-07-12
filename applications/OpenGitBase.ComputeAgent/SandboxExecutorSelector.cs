namespace OpenGitBase.ComputeAgent;

public sealed class SandboxExecutorSelector : ISandboxExecutor
{
    private readonly ComputeAgentOptions _options;
    private readonly ProcessSandboxExecutor _processSandbox;
    private readonly FirecrackerSandboxExecutor _firecrackerSandbox;

    public SandboxExecutorSelector(
        ComputeAgentOptions options,
        ProcessSandboxExecutor processSandbox,
        FirecrackerSandboxExecutor firecrackerSandbox
    )
    {
        _options = options;
        _processSandbox = processSandbox;
        _firecrackerSandbox = firecrackerSandbox;
    }

    public Task<SandboxExecutionResult> ExecuteAsync(
        string script,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        CancellationToken cancellationToken
    )
    {
        var executor = SelectExecutor();
        return executor.ExecuteAsync(script, workingDirectory, environment, cancellationToken);
    }

    private ISandboxExecutor SelectExecutor()
    {
        if (_options.PreferProcessSandbox || !File.Exists("/dev/kvm"))
        {
            return _processSandbox;
        }

        return _firecrackerSandbox;
    }
}
