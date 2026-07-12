namespace OpenGitBase.ComputeAgent;

public sealed class HostEgressCheckResult
{
    public bool Allowed { get; init; }

    public string? DenialLogLine { get; init; }
}
