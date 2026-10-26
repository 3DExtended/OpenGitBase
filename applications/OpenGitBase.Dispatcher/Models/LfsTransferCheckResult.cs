namespace OpenGitBase.Dispatcher.Models;

/// <summary>The API's go-ahead for one object transfer.</summary>
public sealed class LfsTransferCheckResult
{
    public Guid RepositoryId { get; init; }

    public string Oid { get; init; } = string.Empty;

    public long Size { get; init; }

    public List<StorageRoutingTarget> Targets { get; init; } = [];
}
