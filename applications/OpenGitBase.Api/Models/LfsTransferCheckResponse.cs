namespace OpenGitBase.Api.Models;

public sealed class LfsTransferCheckResponse
{
    public Guid RepositoryId { get; init; }

    public string Oid { get; init; } = string.Empty;

    /// <summary>Exact size of the object; an upload must send this many bytes.</summary>
    public long Size { get; init; }

    /// <summary>Storage nodes to use, in order: an upload goes to the first, a download tries them in turn.</summary>
    public IReadOnlyList<StorageRoutingTarget> Targets { get; init; } = [];
}
