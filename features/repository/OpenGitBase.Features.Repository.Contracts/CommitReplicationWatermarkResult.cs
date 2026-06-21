namespace OpenGitBase.Features.Repository.Contracts;

public sealed class CommitReplicationWatermarkResult
{
    public bool Success { get; init; }

    public string? Error { get; init; }

    public long PrimaryWatermark { get; init; }

    public static CommitReplicationWatermarkResult Committed(long primaryWatermark) =>
        new() { Success = true, PrimaryWatermark = primaryWatermark };

    public static CommitReplicationWatermarkResult Failed(string error) =>
        new() { Success = false, Error = error };
}
