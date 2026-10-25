using OpenGitBase.Api.Models;

namespace OpenGitBase.Api.Services;

/// <summary>
/// A raw file download: the blob's <see cref="Bytes"/>, or for an LFS pointer the object's <see cref="Content"/>
/// stream with its <see cref="ContentLength"/>.
/// </summary>
public sealed class StorageRawResult
{
    public StorageRawStatus Status { get; init; } = StorageRawStatus.Ok;

    public byte[]? Bytes { get; init; }

    public Stream? Content { get; init; }

    public long? ContentLength { get; init; }

    public string ContentType { get; init; } = "application/octet-stream";

    public required string FileName { get; init; }

    public RepositoryReplicationLagDto? ReplicationLag { get; init; }
}
