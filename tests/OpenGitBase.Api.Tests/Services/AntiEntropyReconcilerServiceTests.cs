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

public class AntiEntropyReconcilerServiceTests
{
    [Fact]
    public async Task RunOnceAsync_WhenReplicaLags_UpdatesAppliedWatermark()
    {
        var repositoryId = Guid.NewGuid();
        var primaryNodeId = Guid.NewGuid();
        var laggingReplicaId = Guid.NewGuid();
        var (service, provider) = await CreateRf3ServiceAsync(
            repositoryId,
            primaryNodeId,
            laggingReplicaId,
            primaryWatermark: 4,
            replicaWatermark: 2
        );

        await using (provider)
        {
            await service.RunOnceAsync(CancellationToken.None);

            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            await using var context = await contextFactory.CreateDbContextAsync();
            var replica = await context
                .Set<RepositoryReplicaEntity>()
                .SingleAsync(entity => entity.StorageNodeId == laggingReplicaId);
            Assert.Equal(4, replica.AppliedWatermark);
        }
    }

    [Fact]
    public async Task RunOnceAsync_WhenEncryptedArtifactsMissing_BootstrapsFromPrimary()
    {
        var repositoryId = Guid.NewGuid();
        var primaryNodeId = Guid.NewGuid();
        var encAId = Guid.NewGuid();
        var encBId = Guid.NewGuid();
        var provisioner = Substitute.For<IStorageProvisionerClient>();
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
                    "{\"epoch\":0,\"watermark\":47,\"bundleSha256\":\"ABC\",\"keyVersion\":1}",
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

        var repositoryKeyService = Substitute.For<IRepositoryKeyService>();
        repositoryKeyService
            .TryGetRepositoryKeyAsync(repositoryId, Arg.Any<CancellationToken>())
            .Returns(new EphemeralRepositoryKey(new byte[32], 1));

        var (service, provider) = await CreateRf4ServiceAsync(
            repositoryId,
            primaryNodeId,
            encAId,
            encBId,
            primaryWatermark: 47,
            provisioner,
            repositoryKeyService
        );

        await using (provider)
        {
            await service.RunOnceAsync(CancellationToken.None);

            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            await using var context = await contextFactory.CreateDbContextAsync();
            var encrypted = await context
                .Set<RepositoryReplicaEntity>()
                .Where(entity => entity.Role == RepositoryReplicaRole.EncryptedReplica)
                .ToListAsync();
            Assert.Equal(2, encrypted.Count);
            Assert.All(encrypted, replica => Assert.Equal(47, replica.ArtifactWatermark));
            await provisioner
                .Received(1)
                .CreateReplicationArtifactAsync(
                    Arg.Is<StorageNodeDto>(node => node.Id.Value == primaryNodeId),
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    repositoryId,
                    47,
                    0,
                    Arg.Any<string>(),
                    1,
                    Arg.Any<CancellationToken>()
                );
            await provisioner
                .Received(2)
                .UploadReplicationArtifactAsync(
                    Arg.Any<StorageNodeDto>(),
                    Arg.Any<string>(),
                    repositoryId,
                    47,
                    Arg.Any<string>(),
                    Arg.Any<byte[]>(),
                    Arg.Any<CancellationToken>()
                );
        }
    }

    [Fact]
    public async Task RunOnceAsync_WhenOneEncryptedHasArtifact_CopiesFromPeerWithoutPrimaryCreate()
    {
        var repositoryId = Guid.NewGuid();
        var primaryNodeId = Guid.NewGuid();
        var encAId = Guid.NewGuid();
        var encBId = Guid.NewGuid();
        var provisioner = Substitute.For<IStorageProvisionerClient>();
        provisioner
            .TryGetReplicationArtifactAsync(
                Arg.Is<StorageNodeDto>(node => node.Id.Value == encAId),
                Arg.Any<string>(),
                repositoryId,
                47,
                Arg.Any<CancellationToken>()
            )
            .Returns(
                ReplicationArtifactFetchResult.Ok(
                    "{\"epoch\":0,\"watermark\":47,\"bundleSha256\":\"ABC\",\"keyVersion\":1}",
                    [9, 8, 7]
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

        var (service, provider) = await CreateRf4ServiceAsync(
            repositoryId,
            primaryNodeId,
            encAId,
            encBId,
            primaryWatermark: 47,
            provisioner,
            Substitute.For<IRepositoryKeyService>(),
            encAArtifactWatermark: 47,
            encBArtifactWatermark: null
        );

        await using (provider)
        {
            await service.RunOnceAsync(CancellationToken.None);

            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            await using var context = await contextFactory.CreateDbContextAsync();
            var encB = await context
                .Set<RepositoryReplicaEntity>()
                .SingleAsync(entity => entity.StorageNodeId == encBId);
            Assert.Equal(47, encB.ArtifactWatermark);
            await provisioner
                .DidNotReceive()
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
                );
            await provisioner
                .Received(1)
                .UploadReplicationArtifactAsync(
                    Arg.Is<StorageNodeDto>(node => node.Id.Value == encBId),
                    Arg.Any<string>(),
                    repositoryId,
                    47,
                    Arg.Any<string>(),
                    Arg.Any<byte[]>(),
                    Arg.Any<CancellationToken>()
                );
        }
    }

    [Fact]
    public async Task RunOnceAsync_WhenPrimaryWatermarkIsZero_MarksEncryptedInSyncWithoutCreate()
    {
        var repositoryId = Guid.NewGuid();
        var primaryNodeId = Guid.NewGuid();
        var encAId = Guid.NewGuid();
        var encBId = Guid.NewGuid();
        var provisioner = Substitute.For<IStorageProvisionerClient>();

        var (service, provider) = await CreateRf4ServiceAsync(
            repositoryId,
            primaryNodeId,
            encAId,
            encBId,
            primaryWatermark: 0,
            provisioner,
            Substitute.For<IRepositoryKeyService>()
        );

        await using (provider)
        {
            await service.RunOnceAsync(CancellationToken.None);

            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            await using var context = await contextFactory.CreateDbContextAsync();
            var encrypted = await context
                .Set<RepositoryReplicaEntity>()
                .Where(entity => entity.Role == RepositoryReplicaRole.EncryptedReplica)
                .ToListAsync();
            Assert.All(encrypted, replica => Assert.Equal(0, replica.ArtifactWatermark));
            await provisioner
                .DidNotReceive()
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
                );
            await provisioner
                .DidNotReceive()
                .UploadReplicationArtifactAsync(
                    Arg.Any<StorageNodeDto>(),
                    Arg.Any<string>(),
                    Arg.Any<Guid>(),
                    Arg.Any<long>(),
                    Arg.Any<string>(),
                    Arg.Any<byte[]>(),
                    Arg.Any<CancellationToken>()
                );
        }
    }

    private static async Task<(AntiEntropyReconcilerService Service, ServiceProvider Provider)>
        CreateRf3ServiceAsync(
            Guid repositoryId,
            Guid primaryNodeId,
            Guid laggingReplicaId,
            long primaryWatermark,
            long replicaWatermark
        )
    {
        var connection = SqliteTestConnection.OpenInMemory();

        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<ListHealthyStorageNodesQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From<IReadOnlyList<StorageNodeDto>>(
                    [CreateNode(primaryNodeId), CreateNode(laggingReplicaId)]
                )
            );
        foreach (var nodeId in new[] { primaryNodeId, laggingReplicaId })
        {
            var storageNodeId = StorageNodeId.From(nodeId);
            queryProcessor
                .RunQueryAsync(
                    Arg.Is<GetStorageNodeApiTokenQuery>(query =>
                        query.StorageNodeId == storageNodeId
                    ),
                    Arg.Any<CancellationToken>()
                )
                .Returns(Option.From("token"));
        }

        var services = new ServiceCollection();
        services.AddSingleton(connection);
        services.AddSingleton(queryProcessor);
        services.AddSingleton<IStorageProvisionerClient, FakeStorageProvisionerClient>();
        services.AddSingleton(Substitute.For<IRepositoryKeyService>());
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
        services.AddSingleton<Rf1BackfillService>();
        services.AddSingleton<AntiEntropyReconcilerService>();

        var provider = services.BuildServiceProvider();
        var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
            foreach (var nodeId in new[] { primaryNodeId, laggingReplicaId })
            {
                context.Set<StorageNodeEntity>().Add(
                    new StorageNodeEntity
                    {
                        Id = nodeId,
                        NodeId = nodeId.ToString(),
                        InternalHost = nodeId.ToString(),
                        InternalHttpPort = 8081,
                        IsHealthy = true,
                        RegisteredAt = DateTimeOffset.UtcNow,
                    }
                );
            }

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
                    PrimaryWatermark = primaryWatermark,
                    ReplicationState = ReplicationState.Rf3Healthy,
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
                            StorageNodeId = laggingReplicaId,
                            Role = RepositoryReplicaRole.Replica,
                            AppliedWatermark = replicaWatermark,
                        },
                    ],
                }
            );
            await context.SaveChangesAsync();
        }

        return (provider.GetRequiredService<AntiEntropyReconcilerService>(), provider);
    }

    private static async Task<(AntiEntropyReconcilerService Service, ServiceProvider Provider)>
        CreateRf4ServiceAsync(
            Guid repositoryId,
            Guid primaryNodeId,
            Guid encAId,
            Guid encBId,
            long primaryWatermark,
            IStorageProvisionerClient provisioner,
            IRepositoryKeyService repositoryKeyService,
            long? encAArtifactWatermark = null,
            long? encBArtifactWatermark = null
        )
    {
        var connection = SqliteTestConnection.OpenInMemory();
        var nodeIds = new[] { primaryNodeId, encAId, encBId };

        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<ListHealthyStorageNodesQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From<IReadOnlyList<StorageNodeDto>>(
                    nodeIds.Select(CreateNode).ToList()
                )
            );
        foreach (var nodeId in nodeIds)
        {
            var storageNodeId = StorageNodeId.From(nodeId);
            queryProcessor
                .RunQueryAsync(
                    Arg.Is<GetStorageNodeApiTokenQuery>(query =>
                        query.StorageNodeId == storageNodeId
                    ),
                    Arg.Any<CancellationToken>()
                )
                .Returns(Option.From("token"));
        }

        var services = new ServiceCollection();
        services.AddSingleton(connection);
        services.AddSingleton(queryProcessor);
        services.AddSingleton(provisioner);
        services.AddSingleton(repositoryKeyService);
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
        services.AddSingleton<Rf1BackfillService>();
        services.AddSingleton<AntiEntropyReconcilerService>();

        var provider = services.BuildServiceProvider();
        var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
            foreach (var nodeId in nodeIds)
            {
                context.Set<StorageNodeEntity>().Add(
                    new StorageNodeEntity
                    {
                        Id = nodeId,
                        NodeId = nodeId.ToString(),
                        InternalHost = nodeId.ToString(),
                        InternalHttpPort = 8081,
                        IsHealthy = true,
                        RegisteredAt = DateTimeOffset.UtcNow,
                    }
                );
            }

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
                    ReadReplicaStorageNodeId = primaryNodeId,
                    PrimaryWatermark = primaryWatermark,
                    ReplicationEpoch = 0,
                    ReplicationState = ReplicationState.Rf4Healthy,
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
                            StorageNodeId = encAId,
                            Role = RepositoryReplicaRole.EncryptedReplica,
                            AppliedWatermark = primaryWatermark,
                            ArtifactWatermark = encAArtifactWatermark,
                            LastSyncedAt = DateTimeOffset.Parse("2026-07-10T20:27:06Z"),
                        },
                        new RepositoryReplicaEntity
                        {
                            RepositoryId = repositoryId,
                            StorageNodeId = encBId,
                            Role = RepositoryReplicaRole.EncryptedReplica,
                            AppliedWatermark = primaryWatermark,
                            ArtifactWatermark = encBArtifactWatermark,
                            LastSyncedAt = DateTimeOffset.Parse("2026-07-10T20:27:19Z"),
                        },
                    ],
                }
            );
            await context.SaveChangesAsync();
        }

        return (provider.GetRequiredService<AntiEntropyReconcilerService>(), provider);
    }

    private static StorageNodeDto CreateNode(Guid nodeId) =>
        new()
        {
            Id = StorageNodeId.From(nodeId),
            NodeId = nodeId.ToString(),
            InternalHost = nodeId.ToString(),
            InternalHttpPort = 8081,
            IsHealthy = true,
            FreeBytesAvailable = 1_000_000,
        };
}
