namespace OpenGitBase.Features.Repository.Contracts;

public sealed class RelayReplicationArtifactResult
{
    public bool Success { get; init; }

    public string? Error { get; init; }

    public int StatusCode { get; init; }

    public static RelayReplicationArtifactResult Ok() =>
        new() { Success = true, StatusCode = 200 };

    public static RelayReplicationArtifactResult Failed(int statusCode, string error) =>
        new() { Success = false, StatusCode = statusCode, Error = error };
}
