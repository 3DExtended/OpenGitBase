namespace OpenGitBase.ComputeAgent;

public interface ISandboxExecutor
{
    Task<SandboxExecutionResult> ExecuteAsync(
        string script,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        CancellationToken cancellationToken
    );
}
