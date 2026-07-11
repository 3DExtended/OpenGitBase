namespace OpenGitBase.ComputeAgent;

public sealed class SandboxExecutionResult
{
    public bool Success { get; init; }

    public int ExitCode { get; init; }

    public long DurationMs { get; init; }
}
