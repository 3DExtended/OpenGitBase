using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Features.StorageNode.Tests.Testing;

public static class StorageNodeTestData
{
    public const string SampleNodeId = "storage-1";
    public const string UpdatedNodeId = "storage-1-updated";
    public const string SampleCertificateThumbprint = "AABBCCDDEEFF00112233445566778899AABBCCDDEEFF00112233445566778899";

    public static StorageNodeEntity CreateEntity(
        string nodeId = SampleNodeId,
        bool isHealthy = true,
        long freeBytes = 1_000_000,
        DateTimeOffset? lastHeartbeatAt = null
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            NodeId = nodeId,
            InternalHost = nodeId,
            InternalSshPort = 22,
            InternalHttpPort = 8081,
            ApiTokenHash = "hash",
            FreeBytesAvailable = freeBytes,
            TotalBytesAvailable = 10_000_000,
            LastHeartbeatAt = lastHeartbeatAt ?? DateTimeOffset.UtcNow,
            IsHealthy = isHealthy,
            RegisteredAt = DateTimeOffset.UtcNow,
            CertificateThumbprint = SampleCertificateThumbprint,
        };

    public static async Task<(StorageNodeId Id, StorageNodeEntity Entity)> SeedAsync(
        OpenGitBaseDbContext context,
        string nodeId = SampleNodeId,
        bool isHealthy = true,
        long freeBytes = 1_000_000
    )
    {
        var entity = CreateEntity(nodeId, isHealthy, freeBytes);
        context.Set<StorageNodeEntity>().Add(entity);
        await context.SaveChangesAsync().ConfigureAwait(false);
        return (StorageNodeId.From(entity.Id), entity);
    }
}
