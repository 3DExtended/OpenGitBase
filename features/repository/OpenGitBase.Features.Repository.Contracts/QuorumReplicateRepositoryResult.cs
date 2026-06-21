namespace OpenGitBase.Features.Repository.Contracts;

public sealed class QuorumReplicateRepositoryResult
{
    public bool Success { get; init; }

    public string? Error { get; init; }

    public long PrimaryWatermark { get; init; }

    public static QuorumReplicateRepositoryResult Replicated(long primaryWatermark) =>
        new() { Success = true, PrimaryWatermark = primaryWatermark };

    public static QuorumReplicateRepositoryResult Failed(string error) =>
        new() { Success = false, Error = error };
}
