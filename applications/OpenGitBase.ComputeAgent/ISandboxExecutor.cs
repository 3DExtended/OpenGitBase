namespace OpenGitBase.ComputeAgent;

public interface ISandboxExecutor
{
    Task<bool> ExecuteAsync(string script, CancellationToken cancellationToken);
}
