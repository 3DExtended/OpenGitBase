using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Api.Tests.Services;

public class RebalanceServiceTests
{
    [Fact]
    public async Task RunOnceAsync_WhenRecoveredBeforeReplacementInSync_RestoresOriginalTrio()
    {
        var repositoryId = Guid.NewGuid();
        var primaryNodeId = Guid.NewGuid();
        var originalReplicaId = Guid.NewGuid();
        var replacementNodeId = Guid.NewGuid();
        var (service, provider) = await CreateServiceAsync(
            repositoryId,
            primaryNodeId,
            originalReplicaId,
            replacementNodeId
        );

        RebalanceService.TrackPendingReplacement(
            repositoryId,
            replacementNodeId,
            originalReplicaId
        );

        await using (provider)
        {
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            await using (var context = await contextFactory.CreateDbContextAsync())
            {
                var originalNode = await context
                    .Set<StorageNodeEntity>()
                    .SingleAsync(node => node.Id == originalReplicaId);
                originalNode.IsHealthy = true;
                await context.SaveChangesAsync();
            }

            await service.RunOnceAsync(CancellationToken.None);

            await using var verifyContext = await contextFactory.CreateDbContextAsync();
            var repository = await verifyContext
                .Set<RepositoryEntity>()
                .Include(entity => entity.Replicas)
                .SingleAsync();
            Assert.Contains(
                repository.Replicas,
                replica => replica.StorageNodeId == originalReplicaId
            );
            Assert.DoesNotContain(
                repository.Replicas,
                replica => replica.StorageNodeId == replacementNodeId
            );
        }
    }

    [Fact]
    public async Task RunOnceAsync_Rf4_WhenDeadEncryptedReplicaIsReplaced_StaysRf4Healthy()
    {
        // The RF4 layout ReplicaSetPlanner builds: primary + two encrypted replicas, so only one
        // plaintext copy. Replacing the encrypted replica of a dead node restores that layout in
        // full; requiring two plaintext copies marked the repo Degraded instead, which also
        // refuses every write (#413 storage roll).
        var repositoryId = Guid.NewGuid();
        var primaryNodeId = Guid.NewGuid();
        var deadNodeId = Guid.NewGuid();
        var encryptedNodeId = Guid.NewGuid();
        var replacementNodeId = Guid.NewGuid();
        var (service, provider) = await BuildServiceAsync(
            healthyNodes:
            [
                CreateNode(primaryNodeId, "storage-1"),
                CreateNode(encryptedNodeId, "storage-3"),
                CreateNode(replacementNodeId, "storage-4"),
            ],
            nodes:
            [
                CreateNodeEntity(primaryNodeId, "storage-1", isHealthy: true),
                CreateNodeEntity(deadNodeId, "storage-2", isHealthy: false),
                CreateNodeEntity(encryptedNodeId, "storage-3", isHealthy: true),
                CreateNodeEntity(replacementNodeId, "storage-4", isHealthy: true),
            ],
            repository: CreateRepository(
                repositoryId,
                primaryNodeId,
                primaryWatermark: 28,
                ReplicationState.Rf4Healthy,
                new RepositoryReplicaEntity
                {
                    RepositoryId = repositoryId,
                    StorageNodeId = primaryNodeId,
                    Role = RepositoryReplicaRole.Primary,
                    AppliedWatermark = 28,
                },
                new RepositoryReplicaEntity
                {
                    RepositoryId = repositoryId,
                    StorageNodeId = deadNodeId,
                    Role = RepositoryReplicaRole.EncryptedReplica,
                    ArtifactWatermark = 28,
                },
                new RepositoryReplicaEntity
                {
                    RepositoryId = repositoryId,
                    StorageNodeId = encryptedNodeId,
                    Role = RepositoryReplicaRole.EncryptedReplica,
                    ArtifactWatermark = 28,
                }
            )
        );

        await using (provider)
        {
            try
            {
                await service.RunOnceAsync(CancellationToken.None);

                var contextFactory = provider.GetRequiredService<
                    IDbContextFactory<OpenGitBaseDbContext>
                >();
                await using var verifyContext = await contextFactory.CreateDbContextAsync();
                var repository = await verifyContext
                    .Set<RepositoryEntity>()
                    .Include(entity => entity.Replicas)
                    .SingleAsync();
                Assert.Contains(
                    repository.Replicas,
                    replica =>
                        replica.StorageNodeId == replacementNodeId
                        && replica.Role == RepositoryReplicaRole.EncryptedReplica
                        && replica.ArtifactWatermark == 28
                );
                Assert.DoesNotContain(
                    repository.Replicas,
                    replica => replica.StorageNodeId == deadNodeId
                );
                Assert.Equal(ReplicationState.Rf4Healthy, repository.ReplicationState);
            }
            finally
            {
                RebalanceService.ClearPendingReplacement(repositoryId, replacementNodeId);
            }
        }
    }

    private static Task<(RebalanceService Service, ServiceProvider Provider)> CreateServiceAsync(
        Guid repositoryId,
        Guid primaryNodeId,
        Guid originalReplicaId,
        Guid replacementNodeId
    ) =>
        BuildServiceAsync(
            healthyNodes:
            [
                CreateNode(primaryNodeId, "storage-1"),
                CreateNode(originalReplicaId, "storage-2"),
                CreateNode(replacementNodeId, "storage-3"),
            ],
            nodes:
            [
                CreateNodeEntity(primaryNodeId, "storage-1", isHealthy: true),
                CreateNodeEntity(originalReplicaId, "storage-2", isHealthy: false),
                CreateNodeEntity(replacementNodeId, "storage-3", isHealthy: true),
            ],
            repository: CreateRepository(
                repositoryId,
                primaryNodeId,
                primaryWatermark: 3,
                ReplicationState.Degraded,
                new RepositoryReplicaEntity
                {
                    RepositoryId = repositoryId,
                    StorageNodeId = primaryNodeId,
                    Role = RepositoryReplicaRole.Primary,
                    AppliedWatermark = 3,
                },
                new RepositoryReplicaEntity
                {
                    RepositoryId = repositoryId,
                    StorageNodeId = replacementNodeId,
                    Role = RepositoryReplicaRole.Replica,
                    AppliedWatermark = 1,
                }
            )
        );

    private static async Task<(RebalanceService Service, ServiceProvider Provider)>
        BuildServiceAsync(
            IReadOnlyList<StorageNodeDto> healthyNodes,
            IReadOnlyList<StorageNodeEntity> nodes,
            RepositoryEntity repository
        )
    {
        var connection = SqliteTestConnection.OpenInMemory();

        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<ListHealthyStorageNodesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(healthyNodes));
        queryProcessor
            .RunQueryAsync(Arg.Any<GetStorageNodeApiTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From("node-token"));

        var services = new ServiceCollection();
        services.AddSingleton(connection);
        services.AddSingleton(queryProcessor);
        services.AddSingleton<IStorageProvisionerClient, FakeStorageProvisionerClient>();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider(
                [
                    typeof(RepositoryMapsterConfig).Assembly,
                    typeof(global::OpenGitBase.Features.StorageNode.StorageNodeMapsterConfig).Assembly,
                ]
            )
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        var mapsterConfig = new TypeAdapterConfig();
        new RepositoryMapsterConfig().Register(mapsterConfig);
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(sp.GetRequiredService<TypeAdapterConfig>()));
        services.AddSingleton<RebalanceService>();

        var provider = services.BuildServiceProvider();
        var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
            context.Set<StorageNodeEntity>().AddRange(nodes);
            context.Set<RepositoryEntity>().Add(repository);
            await context.SaveChangesAsync();
        }

        return (provider.GetRequiredService<RebalanceService>(), provider);
    }

    private static RepositoryEntity CreateRepository(
        Guid repositoryId,
        Guid primaryNodeId,
        long primaryWatermark,
        ReplicationState replicationState,
        params RepositoryReplicaEntity[] replicas
    ) =>
        new()
        {
            Id = repositoryId,
            Name = "repo",
            Slug = "repo",
            OwnerUserId = Guid.NewGuid(),
            PhysicalPath = $"/srv/git/{repositoryId}.git",
            StorageNodeId = primaryNodeId,
            PrimaryStorageNodeId = primaryNodeId,
            PrimaryWatermark = primaryWatermark,
            ReplicationState = replicationState,
            Replicas = [.. replicas],
        };

    private static StorageNodeEntity CreateNodeEntity(Guid nodeId, string nodeName, bool isHealthy) =>
        new()
        {
            Id = nodeId,
            NodeId = nodeName,
            InternalHost = nodeName,
            InternalHttpPort = 8081,
            IsHealthy = isHealthy,
            RegisteredAt = DateTimeOffset.UtcNow,
        };

    private static StorageNodeDto CreateNode(Guid nodeId, string nodeName) =>
        new()
        {
            Id = StorageNodeId.From(nodeId),
            NodeId = nodeName,
            InternalHost = nodeName,
            InternalHttpPort = 8081,
            IsHealthy = true,
            FreeBytesAvailable = 1_000_000,
        };
}
