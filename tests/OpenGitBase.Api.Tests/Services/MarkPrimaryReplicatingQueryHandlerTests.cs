using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Tests.Services;

public class MarkPrimaryReplicatingQueryHandlerTests
{
    private static readonly DateTimeOffset LongAgo = DateTimeOffset.UtcNow.AddDays(-2);

    [Fact]
    public async Task RunQueryAsync_MovesOnlyThePrimaryReplicasLastSyncedAt()
    {
        var repositoryId = Guid.NewGuid();
        var primaryId = Guid.NewGuid();
        var encryptedId = Guid.NewGuid();
        await using var provider = await CreateAsync(repositoryId, primaryId, encryptedId);
        var handler = provider.GetRequiredService<MarkPrimaryReplicatingQueryHandler>();

        var result = await handler.RunQueryAsync(
            new MarkPrimaryReplicatingQuery
            {
                RepositoryId = RepositoryId.From(repositoryId),
                StorageNodeId = StorageNodeId.From(primaryId),
            },
            CancellationToken.None
        );

        Assert.True(result.Get());
        var replicas = await ReadLastSyncedAtAsync(provider, repositoryId);
        Assert.True(replicas[primaryId] > DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(LongAgo, replicas[encryptedId]);
    }

    [Fact]
    public async Task RunQueryAsync_ForANodeThatIsNotThePrimary_MarksNothing()
    {
        var repositoryId = Guid.NewGuid();
        var primaryId = Guid.NewGuid();
        var encryptedId = Guid.NewGuid();
        await using var provider = await CreateAsync(repositoryId, primaryId, encryptedId);
        var handler = provider.GetRequiredService<MarkPrimaryReplicatingQueryHandler>();

        var result = await handler.RunQueryAsync(
            new MarkPrimaryReplicatingQuery
            {
                RepositoryId = RepositoryId.From(repositoryId),
                StorageNodeId = StorageNodeId.From(encryptedId),
            },
            CancellationToken.None
        );

        Assert.False(result.Get());
        var replicas = await ReadLastSyncedAtAsync(provider, repositoryId);
        Assert.Equal(LongAgo, replicas[primaryId]);
        Assert.Equal(LongAgo, replicas[encryptedId]);
    }

    private static async Task<Dictionary<Guid, DateTimeOffset?>> ReadLastSyncedAtAsync(
        ServiceProvider provider,
        Guid repositoryId
    )
    {
        var factory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        return await context
            .Set<RepositoryReplicaEntity>()
            .AsNoTracking()
            .Where(replica => replica.RepositoryId == repositoryId)
            .ToDictionaryAsync(replica => replica.StorageNodeId, replica => replica.LastSyncedAt);
    }

    private static async Task<ServiceProvider> CreateAsync(Guid repositoryId, Guid primaryId, Guid encryptedId)
    {
        var connection = SqliteTestConnection.OpenInMemory();
        var services = new ServiceCollection();
        services.AddSingleton(connection);
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(RepositoryMapsterConfig).Assembly])
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        services.AddSingleton<MarkPrimaryReplicatingQueryHandler>();
        var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
        context.Set<RepositoryEntity>().Add(
            new RepositoryEntity
            {
                Id = repositoryId,
                Name = "repo",
                Slug = "repo",
                OwnerUserId = Guid.NewGuid(),
                PhysicalPath = $"/srv/git/{repositoryId}.git",
                StorageNodeId = primaryId,
                PrimaryStorageNodeId = primaryId,
                PrimaryWatermark = 5,
                ReplicationState = ReplicationState.Rf4Healthy,
                Replicas =
                [
                    new RepositoryReplicaEntity
                    {
                        RepositoryId = repositoryId,
                        StorageNodeId = primaryId,
                        Role = RepositoryReplicaRole.Primary,
                        AppliedWatermark = 6,
                        LastSyncedAt = LongAgo,
                    },
                    new RepositoryReplicaEntity
                    {
                        RepositoryId = repositoryId,
                        StorageNodeId = encryptedId,
                        Role = RepositoryReplicaRole.EncryptedReplica,
                        ArtifactWatermark = 5,
                        LastSyncedAt = LongAgo,
                    },
                ],
            }
        );
        await context.SaveChangesAsync();
        return provider;
    }
}
