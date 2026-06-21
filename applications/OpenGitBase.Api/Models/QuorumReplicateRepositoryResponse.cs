namespace OpenGitBase.Api.Models;

public sealed class QuorumReplicateRepositoryResponse
{
    public bool Success { get; init; }

    public string? Error { get; init; }

    public long PrimaryWatermark { get; init; }
}
