namespace OpenGitBase.Dispatcher.Models;

public sealed class RepositoryAccessCheckResponse
{
    public bool Allowed { get; init; }
    public string? Reason { get; init; }

    public string? PhysicalPath { get; init; }

    public string? StorageNodeInternalHost { get; init; }

    public int? StorageNodeInternalSshPort { get; init; }
}
