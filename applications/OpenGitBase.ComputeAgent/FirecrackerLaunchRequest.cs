namespace OpenGitBase.ComputeAgent;

public sealed class FirecrackerLaunchRequest
{
    public string Script { get; init; } = string.Empty;

    public string WorkingDirectory { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string> Environment { get; init; }
        = new Dictionary<string, string>();

    public string RunAsUser { get; init; } = "ogb";
}
