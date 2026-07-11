using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
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

public class CommitReplicationWatermarkQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenPrimaryCommitsValidQuorum_IncrementsWatermark()
    {
        var repositoryId = Guid.NewGuid();
        var primaryNodeId = Guid.NewGuid();
        var replicaNodeId = Guid.NewGuid();
        var (handler, provider) = await CreateHandlerAsync(
            repositoryId,
            primaryNodeId,
            replicaNodeId,
            epoch: 1,
            primaryWatermark: 4
        );
        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new CommitReplicationWatermarkQuery
                {
                    RepositoryId = RepositoryId.From(repositoryId),
                    StorageNodeId = StorageNodeId.From(primaryNodeId),
                    ReplicationEpoch = 1,
                    NewWatermark = 5,
                    QuorumNodeIds =
                    [
                        StorageNodeId.From(primaryNodeId),
                        StorageNodeId.From(replicaNodeId),
                    ],
                },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            Assert.True(result.Get().Success);
            Assert.Equal(5, result.Get().PrimaryWatermark);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenEpochStale_RejectsCommit()
    {
        var repositoryId = Guid.NewGuid();
        var primaryNodeId = Guid.NewGuid();
        var replicaNodeId = Guid.NewGuid();
        var (handler, provider) = await CreateHandlerAsync(
            repositoryId,
            primaryNodeId,
            replicaNodeId,
            epoch: 2,
            primaryWatermark: 1
        );
        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new CommitReplicationWatermarkQuery
                {
                    RepositoryId = RepositoryId.From(repositoryId),
                    StorageNodeId = StorageNodeId.From(primaryNodeId),
                    ReplicationEpoch = 1,
                    NewWatermark = 2,
                    QuorumNodeIds =
                    [
                        StorageNodeId.From(primaryNodeId),
                        StorageNodeId.From(replicaNodeId),
                    ],
                },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            Assert.False(result.Get().Success);
            Assert.Contains("epoch", result.Get().Error!, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenNonPrimaryCommits_RejectsCommit()
    {
        var repositoryId = Guid.NewGuid();
        var primaryNodeId = Guid.NewGuid();
        var replicaNodeId = Guid.NewGuid();
        var (handler, provider) = await CreateHandlerAsync(
            repositoryId,
            primaryNodeId,
            replicaNodeId,
            epoch: 1,
            primaryWatermark: 0
        );
        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new CommitReplicationWatermarkQuery
                {
                    RepositoryId = RepositoryId.From(repositoryId),
                    StorageNodeId = StorageNodeId.From(replicaNodeId),
                    ReplicationEpoch = 1,
                    NewWatermark = 1,
                    QuorumNodeIds =
                    [
                        StorageNodeId.From(primaryNodeId),
                        StorageNodeId.From(replicaNodeId),
                    ],
                },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            Assert.False(result.Get().Success);
            Assert.Contains("primary", result.Get().Error!, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static async Task<(
        CommitReplicationWatermarkQueryHandler Handler,
        ServiceProvider Provider
    )> CreateHandlerAsync(
        Guid repositoryId,
        Guid primaryNodeId,
        Guid replicaNodeId,
        long epoch,
        long primaryWatermark
    )
    {
        var connection = SqliteTestConnection.OpenInMemory();

        var services = new ServiceCollection();
        services.AddSingleton(connection);
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(RepositoryMapsterConfig).Assembly])
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        var mapsterConfig = new TypeAdapterConfig();
        new RepositoryMapsterConfig().Register(mapsterConfig);
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(sp.GetRequiredService<TypeAdapterConfig>()));
        services.AddSingleton<CommitReplicationWatermarkQueryHandler>();

        var provider = services.BuildServiceProvider();
        var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
            context.Set<RepositoryEntity>().Add(
                new RepositoryEntity
                {
                    Id = repositoryId,
                    Name = "repo",
                    Slug = "repo",
                    OwnerUserId = Guid.NewGuid(),
                    PhysicalPath = $"/srv/git/{repositoryId}.git",
                    StorageNodeId = primaryNodeId,
                    PrimaryStorageNodeId = primaryNodeId,
                    ReplicationEpoch = epoch,
                    PrimaryWatermark = primaryWatermark,
                    Replicas =
                    [
                        new RepositoryReplicaEntity
                        {
                            RepositoryId = repositoryId,
                            StorageNodeId = primaryNodeId,
                            Role = RepositoryReplicaRole.Primary,
                            AppliedWatermark = primaryWatermark,
                        },
                        new RepositoryReplicaEntity
                        {
                            RepositoryId = repositoryId,
                            StorageNodeId = replicaNodeId,
                            Role = RepositoryReplicaRole.Replica,
                        },
                    ],
                }
            );
            await context.SaveChangesAsync();
        }

        return (provider.GetRequiredService<CommitReplicationWatermarkQueryHandler>(), provider);
    }
}
