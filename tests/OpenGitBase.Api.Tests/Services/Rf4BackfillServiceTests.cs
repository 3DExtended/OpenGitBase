using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Common.Storage;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Api.Tests.Services;

public class Rf4BackfillServiceTests
{
    [Fact]
    public async Task RunOnceAsync_WhenRf3Healthy_MigratesToRf4Healthy()
    {
        var repositoryId = Guid.NewGuid();
        var primaryId = Guid.NewGuid();
        var replicaId = Guid.NewGuid();
        var readId = primaryId;
        var encAId = Guid.NewGuid();
        var encBId = Guid.NewGuid();
        var (service, provider) = await CreateServiceAsync(
            repositoryId,
            primaryId,
            replicaId,
            [primaryId, replicaId, encAId, encBId]
        );

        await using (provider)
        {
            await service.RunOnceAsync(CancellationToken.None);

            var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var repository = await context
                .Set<RepositoryEntity>()
                .Include(entity => entity.Replicas)
                .SingleAsync();
            Assert.Equal(ReplicationState.Rf4Healthy, repository.ReplicationState);
            Assert.NotNull(repository.ReadReplicaStorageNodeId);
            Assert.Equal(
                2,
                repository.Replicas.Count(replica => replica.Role == RepositoryReplicaRole.EncryptedReplica)
            );
            Assert.True(repository.Replicas.Count >= 3);
            Assert.All(
                repository.Replicas.Where(replica =>
                    replica.Role == RepositoryReplicaRole.EncryptedReplica
                ),
                replica => Assert.Equal(0, replica.ArtifactWatermark)
            );
        }
    }

    [Fact]
    public async Task RunOnceAsync_WhenPrimaryHasContent_CreatesAndUploadsEncryptedArtifacts()
    {
        var repositoryId = Guid.NewGuid();
        var primaryId = Guid.NewGuid();
        var replicaId = Guid.NewGuid();
        var encAId = Guid.NewGuid();
        var encBId = Guid.NewGuid();
        var provisioner = Substitute.For<IStorageProvisionerClient>();
        provisioner
            .ProvisionRepositoryAsync(
                Arg.Any<StorageNodeDto>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(StorageProvisionerResult.Ok(201));
        provisioner
            .CreateReplicationArtifactAsync(
                Arg.Any<StorageNodeDto>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<long>(),
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                ReplicationArtifactFetchResult.Ok(
                    "{\"epoch\":0,\"watermark\":12,\"bundleSha256\":\"ABC\",\"keyVersion\":1}",
                    [1, 2, 3]
                )
            );
        provisioner
            .UploadReplicationArtifactAsync(
                Arg.Any<StorageNodeDto>(),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(StorageProvisionerResult.Ok(201));

        var (service, provider) = await CreateServiceAsync(
            repositoryId,
            primaryId,
            replicaId,
            [primaryId, replicaId, encAId, encBId],
            primaryWatermark: 12,
            provisioner: provisioner
        );

        await using (provider)
        {
            await service.RunOnceAsync(CancellationToken.None);

            var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var repository = await context
                .Set<RepositoryEntity>()
                .Include(entity => entity.Replicas)
                .SingleAsync();
            Assert.Equal(ReplicationState.Rf4Healthy, repository.ReplicationState);
            Assert.All(
                repository.Replicas.Where(replica =>
                    replica.Role == RepositoryReplicaRole.EncryptedReplica
                ),
                replica => Assert.Equal(12, replica.ArtifactWatermark)
            );
            await provisioner
                .Received(1)
                .CreateReplicationArtifactAsync(
                    Arg.Is<StorageNodeDto>(node => node.Id.Value == primaryId),
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    repositoryId,
                    12,
                    Arg.Any<long>(),
                    Arg.Any<string>(),
                    Arg.Any<int>(),
                    Arg.Any<CancellationToken>()
                );
            await provisioner
                .Received(2)
                .UploadReplicationArtifactAsync(
                    Arg.Any<StorageNodeDto>(),
                    Arg.Any<string>(),
                    repositoryId,
                    12,
                    Arg.Any<string>(),
                    Arg.Any<byte[]>(),
                    Arg.Any<CancellationToken>()
                );
        }
    }

    [Fact]
    public async Task RunOnceAsync_WhenCreateArtifactFails_LeavesRepositoryMigrating()
    {
        var repositoryId = Guid.NewGuid();
        var primaryId = Guid.NewGuid();
        var replicaId = Guid.NewGuid();
        var encAId = Guid.NewGuid();
        var encBId = Guid.NewGuid();
        var provisioner = Substitute.For<IStorageProvisionerClient>();
        provisioner
            .ProvisionRepositoryAsync(
                Arg.Any<StorageNodeDto>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(StorageProvisionerResult.Ok(201));
        provisioner
            .CreateReplicationArtifactAsync(
                Arg.Any<StorageNodeDto>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<long>(),
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(ReplicationArtifactFetchResult.Fail(500, "bundle failed"));

        var (service, provider) = await CreateServiceAsync(
            repositoryId,
            primaryId,
            replicaId,
            [primaryId, replicaId, encAId, encBId],
            primaryWatermark: 5,
            provisioner: provisioner
        );

        await using (provider)
        {
            await service.RunOnceAsync(CancellationToken.None);

            var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var repository = await context.Set<RepositoryEntity>().SingleAsync();
            Assert.Equal(ReplicationState.Rf4Migrating, repository.ReplicationState);
        }
    }

    [Fact]
    public async Task RunOnceAsync_WhenThePlannerPrefersAnotherPrimary_KeepsTheCurrentPrimary()
    {
        // Production, "Notes": after a failover the primary was storage-3. The planner always wants storage-1 as the
        // primary and put storage-3 among the encrypted replicas, so the backfill made storage-1's row Primary and
        // storage-3's row EncryptedReplica but left PrimaryStorageNodeId on storage-3. Routing then found no plaintext
        // primary and denied every clone and push.
        var fleet = await CreateFleetServiceAsync(
            ["storage-1", "storage-2", "storage-3", "storage-4"],
            new FleetCopy("storage-3", RepositoryReplicaRole.Primary),
            new FleetCopy("storage-1", RepositoryReplicaRole.Replica),
            new FleetCopy("storage-4", RepositoryReplicaRole.Replica)
        );
        var plan = ReplicaSetPlanner.SelectReplicaSet(fleet.HealthyNodes)!;
        Assert.Equal("storage-1", plan.Primary.NodeId);
        Assert.Equal(new[] { "storage-2", "storage-3" }, new[] { plan.EncryptedReplicaA.NodeId, plan.EncryptedReplicaB.NodeId });

        await using (fleet.Provider)
        {
            await fleet.Service.RunOnceAsync(CancellationToken.None);

            var repository = await LoadRepositoryAsync(fleet.Provider);
            Assert.Equal(ReplicationState.Rf4Healthy, repository.ReplicationState);
            AssertOnePrimaryAt(repository, fleet.Nodes["storage-3"]);
            Assert.Equal(fleet.Nodes["storage-3"], repository.ReadReplicaStorageNodeId);
            Assert.Equal(
                2,
                repository.Replicas.Count(replica => replica.Role == RepositoryReplicaRole.EncryptedReplica)
            );
        }
    }

    [Fact]
    public async Task RunOnceAsync_WhenTheCurrentPrimaryIsOutsideThePlan_LeavesOnlyItPrimary()
    {
        var fleet = await CreateFleetServiceAsync(
            ["storage-1", "storage-2", "storage-3", "storage-4"],
            new FleetCopy("storage-4", RepositoryReplicaRole.Primary),
            new FleetCopy("storage-1", RepositoryReplicaRole.Replica),
            new FleetCopy("storage-2", RepositoryReplicaRole.Replica)
        );

        await using (fleet.Provider)
        {
            await fleet.Service.RunOnceAsync(CancellationToken.None);

            var repository = await LoadRepositoryAsync(fleet.Provider);
            AssertOnePrimaryAt(repository, fleet.Nodes["storage-4"]);
            Assert.Equal(
                2,
                repository.Replicas.Count(replica => replica.Role == RepositoryReplicaRole.EncryptedReplica)
            );
        }
    }

    [Fact]
    public async Task RunOnceAsync_WhenTheCurrentPrimaryIsNotHealthy_LeavesTheRepositoryOnRf3()
    {
        // The plan cannot keep a primary it cannot see; planning around it would move the Primary row away from
        // PrimaryStorageNodeId. Failover deals with the primary first.
        var fleet = await CreateFleetServiceAsync(
            ["storage-1", "storage-2", "storage-4"],
            new FleetCopy("storage-3", RepositoryReplicaRole.Primary),
            new FleetCopy("storage-1", RepositoryReplicaRole.Replica),
            new FleetCopy("storage-4", RepositoryReplicaRole.Replica)
        );

        await using (fleet.Provider)
        {
            await fleet.Service.RunOnceAsync(CancellationToken.None);

            var repository = await LoadRepositoryAsync(fleet.Provider);
            Assert.Equal(ReplicationState.Rf3Healthy, repository.ReplicationState);
            AssertOnePrimaryAt(repository, fleet.Nodes["storage-3"]);
            Assert.DoesNotContain(
                repository.Replicas,
                replica => replica.Role == RepositoryReplicaRole.EncryptedReplica
            );
        }
    }

    [Fact]
    public async Task RunOnceAsync_WhenThePrimaryPointerIsOnAnEncryptedReplica_LeavesTheRepositoryAlone()
    {
        // The split "Notes" ended up in: PrimaryStorageNodeId names a node that holds only encrypted artifacts. Making
        // that row Primary would route pushes to a node without the repository, so the backfill stays out of it.
        var fleet = await CreateFleetServiceAsync(
            ["storage-1", "storage-2", "storage-3", "storage-4"],
            new FleetCopy("storage-3", RepositoryReplicaRole.EncryptedReplica),
            new FleetCopy("storage-1", RepositoryReplicaRole.Primary),
            new FleetCopy("storage-4", RepositoryReplicaRole.Replica)
        );

        await using (fleet.Provider)
        {
            await fleet.Service.RunOnceAsync(CancellationToken.None);

            var repository = await LoadRepositoryAsync(fleet.Provider);
            Assert.Equal(ReplicationState.Rf3Healthy, repository.ReplicationState);
            var roles = repository.Replicas.ToDictionary(replica => replica.StorageNodeId, replica => replica.Role);
            Assert.Equal(RepositoryReplicaRole.EncryptedReplica, roles[fleet.Nodes["storage-3"]]);
            Assert.Equal(RepositoryReplicaRole.Primary, roles[fleet.Nodes["storage-1"]]);
            Assert.Equal(RepositoryReplicaRole.Replica, roles[fleet.Nodes["storage-4"]]);
            Assert.Equal(3, repository.Replicas.Count);
        }
    }

    /// <summary>
    /// Routing and the push gate find the primary through <c>PrimaryStorageNodeId</c>; the storage node learns it is
    /// the primary from its row's role. Both have to name the same node, or every clone and push is denied.
    /// </summary>
    private static void AssertOnePrimaryAt(RepositoryEntity repository, Guid expectedPrimary)
    {
        Assert.Equal(expectedPrimary, repository.PrimaryStorageNodeId);
        Assert.Equal(repository.PrimaryStorageNodeId, repository.StorageNodeId);
        var primary = Assert.Single(repository.Replicas, replica => replica.Role == RepositoryReplicaRole.Primary);
        Assert.Equal(expectedPrimary, primary.StorageNodeId);
    }

    private static async Task<RepositoryEntity> LoadRepositoryAsync(ServiceProvider provider)
    {
        var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Set<RepositoryEntity>().AsNoTracking().Include(entity => entity.Replicas).SingleAsync();
    }

    /// <summary>
    /// An Rf3Healthy repository with one row per <paramref name="copies"/>; the first copy is its
    /// <c>PrimaryStorageNodeId</c>. Healthy nodes have free space in list order, so the planner's encrypted replicas
    /// are the first two after storage-1 (which it always takes as primary).
    /// </summary>
    private static async Task<Fleet> CreateFleetServiceAsync(string[] healthyNodeIds, params FleetCopy[] copies)
    {
        var connection = SqliteTestConnection.OpenInMemory();
        var repositoryId = Guid.NewGuid();
        var nodes = healthyNodeIds
            .Concat(copies.Select(copy => copy.NodeId))
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(nodeId => nodeId, _ => Guid.NewGuid(), StringComparer.Ordinal);
        var healthyNodes = healthyNodeIds
            .Select(
                (nodeId, index) =>
                    new StorageNodeDto
                    {
                        Id = StorageNodeId.From(nodes[nodeId]),
                        NodeId = nodeId,
                        InternalHost = nodeId,
                        InternalHttpPort = 8081,
                        IsHealthy = true,
                        FreeBytesAvailable = 1_000_000 - (index * 100_000),
                    }
            )
            .ToList();

        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<ListHealthyStorageNodesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From((IReadOnlyList<StorageNodeDto>)healthyNodes));
        queryProcessor
            .RunQueryAsync(Arg.Any<GetStorageNodeApiTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From("token"));

        var provider = BuildProvider(connection, queryProcessor, provisioner: null);
        var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
            context.Set<StorageNodeEntity>().AddRange(
                nodes.Select(node => new StorageNodeEntity
                {
                    Id = node.Value,
                    NodeId = node.Key,
                    InternalHost = node.Key,
                    InternalHttpPort = 8081,
                    IsHealthy = healthyNodeIds.Contains(node.Key),
                    RegisteredAt = DateTimeOffset.UtcNow,
                    FreeBytesAvailable = 1_000_000,
                })
            );
            var primaryId = nodes[copies[0].NodeId];
            context.Set<RepositoryEntity>().Add(
                new RepositoryEntity
                {
                    Id = repositoryId,
                    Name = "notes",
                    Slug = "notes",
                    OwnerUserId = Guid.NewGuid(),
                    PhysicalPath = $"/srv/git/{repositoryId}.git",
                    StorageNodeId = primaryId,
                    PrimaryStorageNodeId = primaryId,
                    ReplicationState = ReplicationState.Rf3Healthy,
                    Replicas =
                    [
                        .. copies.Select(copy => new RepositoryReplicaEntity
                        {
                            RepositoryId = repositoryId,
                            StorageNodeId = nodes[copy.NodeId],
                            Role = copy.Role,
                        }),
                    ],
                }
            );
            await context.SaveChangesAsync();
        }

        return new Fleet(provider.GetRequiredService<Rf4BackfillService>(), provider, healthyNodes, nodes);
    }

    private static async Task<(Rf4BackfillService Service, ServiceProvider Provider)>
        CreateServiceAsync(
            Guid repositoryId,
            Guid primaryId,
            Guid replicaId,
            IReadOnlyList<Guid> healthyNodeIds,
            long primaryWatermark = 0,
            IStorageProvisionerClient? provisioner = null
        )
    {
        var connection = SqliteTestConnection.OpenInMemory();

        var queryProcessor = Substitute.For<IQueryProcessor>();
        var healthyNodes = healthyNodeIds
            .Select(
                (nodeId, index) =>
                    new StorageNodeDto
                    {
                        Id = StorageNodeId.From(nodeId),
                        NodeId = index switch
                        {
                            0 => PlatformRf4FleetLayout.PrimaryAndReadNodeId,
                            1 => "storage-replica",
                            2 => PlatformRf4FleetLayout.EncryptedReplicaNodeIdA,
                            _ => PlatformRf4FleetLayout.EncryptedReplicaNodeIdB,
                        },
                        InternalHost = index switch
                        {
                            0 => PlatformRf4FleetLayout.PrimaryAndReadNodeId,
                            1 => "storage-replica",
                            2 => PlatformRf4FleetLayout.EncryptedReplicaNodeIdA,
                            _ => PlatformRf4FleetLayout.EncryptedReplicaNodeIdB,
                        },
                        InternalHttpPort = 8081,
                        IsHealthy = true,
                        FreeBytesAvailable = 1_000_000 - index,
                    }
            )
            .ToList();
        queryProcessor
            .RunQueryAsync(Arg.Any<ListHealthyStorageNodesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From((IReadOnlyList<StorageNodeDto>)healthyNodes));
        foreach (var node in healthyNodes)
        {
            queryProcessor
                .RunQueryAsync(
                    Arg.Is<GetStorageNodeApiTokenQuery>(query => query.StorageNodeId == node.Id),
                    Arg.Any<CancellationToken>()
                )
                .Returns(Option.From("token"));
        }

        var provider = BuildProvider(connection, queryProcessor, provisioner);
        var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
            foreach (var (nodeId, index) in healthyNodeIds.Select((id, i) => (id, i)))
            {
                context.Set<StorageNodeEntity>().Add(
                    new StorageNodeEntity
                    {
                        Id = nodeId,
                        NodeId = healthyNodes[index].NodeId,
                        InternalHost = healthyNodes[index].InternalHost,
                        InternalHttpPort = 8081,
                        IsHealthy = true,
                        RegisteredAt = DateTimeOffset.UtcNow,
                        FreeBytesAvailable = 1_000_000,
                    }
                );
            }

            context.Set<RepositoryEntity>().Add(
                new RepositoryEntity
                {
                    Id = repositoryId,
                    Name = "legacy",
                    Slug = "legacy",
                    OwnerUserId = Guid.NewGuid(),
                    PhysicalPath = $"/srv/git/{repositoryId}.git",
                    StorageNodeId = primaryId,
                    PrimaryStorageNodeId = primaryId,
                    PrimaryWatermark = primaryWatermark,
                    ReplicationState = ReplicationState.Rf3Healthy,
                    Replicas =
                    [
                        new RepositoryReplicaEntity
                        {
                            RepositoryId = repositoryId,
                            StorageNodeId = primaryId,
                            Role = RepositoryReplicaRole.Primary,
                            AppliedWatermark = primaryWatermark,
                        },
                        new RepositoryReplicaEntity
                        {
                            RepositoryId = repositoryId,
                            StorageNodeId = replicaId,
                            Role = RepositoryReplicaRole.Replica,
                            AppliedWatermark = primaryWatermark,
                        },
                    ],
                }
            );
            await context.SaveChangesAsync();
        }

        return (provider.GetRequiredService<Rf4BackfillService>(), provider);
    }

    private static ServiceProvider BuildProvider(
        SqliteConnection connection,
        IQueryProcessor queryProcessor,
        IStorageProvisionerClient? provisioner
    )
    {
        var services = new ServiceCollection();
        services.AddSingleton(connection);
        services.AddSingleton(queryProcessor);
        services.AddSingleton(provisioner ?? (IStorageProvisionerClient)new FakeStorageProvisionerClient());
        services.AddSingleton<IRepositoryKeyService, RepositoryKeyService>();
        services.AddSingleton(
            Options.Create(new RepositoryStorageQuotaOptions { Enabled = false })
        );
        services.AddSingleton<IRepositoryKeyProtectionService>(
            new RepositoryKeyProtectionService(
                new EmailProtectionService(
                    new EncryptionOptions
                    {
                        DataKey = Convert.ToBase64String(new byte[32]),
                        Pepper = "test-pepper",
                    }
                )
            )
        );
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
        services.AddSingleton<Rf4BackfillService>();

        return services.BuildServiceProvider();
    }

    /// <summary>One row of the repository in <see cref="CreateFleetServiceAsync"/>.</summary>
    private sealed record FleetCopy(string NodeId, RepositoryReplicaRole Role);

    private sealed record Fleet(
        Rf4BackfillService Service,
        ServiceProvider Provider,
        IReadOnlyList<StorageNodeDto> HealthyNodes,
        IReadOnlyDictionary<string, Guid> Nodes
    );
}
