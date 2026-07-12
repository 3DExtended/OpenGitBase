namespace OpenGitBase.ComputeAgent;

public sealed class FirecrackerLaunchRequest
{
    public string Script { get; init; } = string.Empty;

    public string WorkingDirectory { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string> Environment { get; init; }
        = new Dictionary<string, string>();

    public string RunAsUser { get; init; } = "ogb";

    public string? RootFsPath { get; init; }

    public string? WorkspaceSharePath { get; init; }

    public FirecrackerResourceLimits ResourceLimits { get; init; } = new();

    public IReadOnlyList<string> EgressAllowlist { get; init; } = [];

    public Action<string>? OnOutputLine { get; init; }
}
