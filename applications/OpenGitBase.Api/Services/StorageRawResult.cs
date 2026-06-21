using OpenGitBase.Api.Models;

namespace OpenGitBase.Api.Services;

public sealed class StorageRawResult
{
    public required byte[] Bytes { get; init; }

    public required string FileName { get; init; }

    public RepositoryReplicationLagDto? ReplicationLag { get; init; }
}
