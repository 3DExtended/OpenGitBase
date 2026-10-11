namespace OpenGitBase.Features.Repository.Contracts;

public sealed class QuorumReplicateRepositoryResult
{
    public bool Success { get; init; }

    public string? Error { get; init; }

    public long PrimaryWatermark { get; init; }

    /// <summary>
    /// The watermark the caller asked for was committed by someone else first (another push or
    /// the reconciler). The caller's push may not be in that commit, so it should re-read the
    /// replication context and try once more with the next watermark.
    /// </summary>
    public bool Stale { get; init; }

    public static QuorumReplicateRepositoryResult Replicated(long primaryWatermark) =>
        new() { Success = true, PrimaryWatermark = primaryWatermark };

    public static QuorumReplicateRepositoryResult Failed(string error) =>
        new() { Success = false, Error = error };

    public static QuorumReplicateRepositoryResult StaleWatermark(string error) =>
        new() { Success = false, Error = error, Stale = true };
}
