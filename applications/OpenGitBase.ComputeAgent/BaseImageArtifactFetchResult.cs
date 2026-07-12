using OpenGitBase.Features.Pipeline.Contracts;

namespace OpenGitBase.ComputeAgent;

public sealed class BaseImageArtifactFetchResult
{
    public bool Success { get; init; }

    public string? LocalPath { get; init; }

    public string? ErrorMessage { get; init; }

    public BaseImageArtifactDto? Artifact { get; init; }
}
