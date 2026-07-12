namespace OpenGitBase.ComputeAgent;

public sealed class FirecrackerLaunchResult
{
    public bool Success { get; init; }

    public int ExitCode { get; init; }

    public long DurationMs { get; init; }

    public string StdOut { get; init; } = string.Empty;

    public string StdErr { get; init; } = string.Empty;

    public string ExecutorLabel { get; init; } = string.Empty;

    public string? TapInterface { get; init; }
}
