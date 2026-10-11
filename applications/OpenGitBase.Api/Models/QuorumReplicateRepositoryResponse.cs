namespace OpenGitBase.Api.Models;

public sealed class QuorumReplicateRepositoryResponse
{
    public bool Success { get; init; }

    public string? Error { get; init; }

    public long PrimaryWatermark { get; init; }

    /// <summary>
    /// The requested watermark was already committed, by an earlier push or by the reconciler.
    /// The hook re-reads the replication context and retries once with the next watermark.
    /// </summary>
    public bool Stale { get; init; }
}
