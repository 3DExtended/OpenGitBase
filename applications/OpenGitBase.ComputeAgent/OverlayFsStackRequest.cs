namespace OpenGitBase.ComputeAgent;

public sealed class OverlayFsStackRequest
{
    public Guid JobId { get; init; }

    public string BaseImageArtifactPath { get; init; } = string.Empty;

    public IReadOnlyList<string> DependencyLayerPaths { get; init; } = [];

    public string WorkRoot { get; init; } = string.Empty;
}
