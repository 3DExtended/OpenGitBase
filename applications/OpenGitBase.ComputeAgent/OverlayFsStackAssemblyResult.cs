namespace OpenGitBase.ComputeAgent;

public sealed class OverlayFsStackAssemblyResult
{
    public bool Success { get; init; }

    public string? MergedRootPath { get; init; }

    public IReadOnlyList<string> LogLines { get; init; } = [];

    public string? ErrorMessage { get; init; }
}
