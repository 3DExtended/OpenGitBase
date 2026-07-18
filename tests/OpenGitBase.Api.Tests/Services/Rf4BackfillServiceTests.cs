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

        var provider = services.BuildServiceProvider();
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
}
