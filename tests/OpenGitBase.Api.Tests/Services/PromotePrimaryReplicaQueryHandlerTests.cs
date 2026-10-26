using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OpenGitBase.Api.Services;
using OpenGitBase.Api.Tests.Services.Lfs;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Api.Tests.Services;

public class PromotePrimaryReplicaQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenPrimaryUnhealthy_PromotesHighestWatermarkReplica()
    {
        var repositoryId = Guid.NewGuid();
        var oldPrimaryId = Guid.NewGuid();
        var replicaId = Guid.NewGuid();
        var (handler, provider) = await CreateHandlerAsync(
            repositoryId,
            oldPrimaryId,
            replicaId,
            primaryHealthy: false,
            replicaWatermark: 5,
            primaryWatermark: 2
        );

        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new PromotePrimaryReplicaQuery
                {
                    RepositoryId = RepositoryId.From(repositoryId),
                },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            Assert.True(result.Get().Promoted);
            Assert.Equal(replicaId, result.Get().NewPrimaryStorageNodeId);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenPrimaryUnhealthy_IncrementsReplicationEpoch()
    {
        var repositoryId = Guid.NewGuid();
        var oldPrimaryId = Guid.NewGuid();
        var replicaId = Guid.NewGuid();
        var (handler, provider) = await CreateHandlerAsync(
            repositoryId,
            oldPrimaryId,
            replicaId,
            primaryHealthy: false,
            replicaWatermark: 5,
            primaryWatermark: 2,
            replicationEpoch: 3
        );

        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new PromotePrimaryReplicaQuery
                {
                    RepositoryId = RepositoryId.From(repositoryId),
                },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            Assert.Equal(4, result.Get().ReplicationEpoch);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenRf4PrimaryUnhealthy_PromotesInSyncReadReplica()
    {
        var repositoryId = Guid.NewGuid();
        var oldPrimaryId = Guid.NewGuid();
        var readReplicaId = Guid.NewGuid();
        var encId = Guid.NewGuid();
        var (handler, provider) = await CreateRf4HandlerAsync(
            repositoryId,
            oldPrimaryId,
            readReplicaId,
            encId,
            primaryWatermark: 5,
            readWatermark: 5
        );

        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new PromotePrimaryReplicaQuery
                {
                    RepositoryId = RepositoryId.From(repositoryId),
                },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            Assert.True(result.Get().Promoted);
            Assert.Equal(readReplicaId, result.Get().NewPrimaryStorageNodeId);
            Assert.Equal(2, result.Get().ReplicationEpoch);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenRf4ReadReplicaIsPromoted_SchedulesAnLfsRepair()
    {
        var repositoryId = Guid.NewGuid();
        var (handler, provider) = await CreateRf4HandlerAsync(
            repositoryId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            primaryWatermark: 5,
            readWatermark: 5
        );

        await using (provider)
        {
            await handler.RunQueryAsync(
                new PromotePrimaryReplicaQuery { RepositoryId = RepositoryId.From(repositoryId) },
                CancellationToken.None
            );

            Assert.Equal([repositoryId], provider.GetRequiredService<RecordingLfsRepairQueue>().Scheduled);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenRf3ReplicaIsPromoted_SchedulesAnLfsRepair()
    {
        var repositoryId = Guid.NewGuid();
        var (handler, provider) = await CreateHandlerAsync(
            repositoryId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            primaryHealthy: false,
            replicaWatermark: 5,
            primaryWatermark: 2
        );

        await using (provider)
        {
            await handler.RunQueryAsync(
                new PromotePrimaryReplicaQuery { RepositoryId = RepositoryId.From(repositoryId) },
                CancellationToken.None
            );

            Assert.Equal([repositoryId], provider.GetRequiredService<RecordingLfsRepairQueue>().Scheduled);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenThePrimaryIsHealthy_SchedulesNoLfsRepair()
    {
        var repositoryId = Guid.NewGuid();
        var (handler, provider) = await CreateHandlerAsync(
            repositoryId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            primaryHealthy: true,
            replicaWatermark: 5,
            primaryWatermark: 5
        );

        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new PromotePrimaryReplicaQuery { RepositoryId = RepositoryId.From(repositoryId) },
                CancellationToken.None
            );

            Assert.False(result.Get().Promoted);
            Assert.Empty(provider.GetRequiredService<RecordingLfsRepairQueue>().Scheduled);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenTheReadReplicaLagsAndColdRecoveryFails_SchedulesNoLfsRepair()
    {
        var repositoryId = Guid.NewGuid();
        var (handler, provider) = await CreateRf4HandlerAsync(
            repositoryId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            primaryWatermark: 5,
            readWatermark: 3
        );

        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new PromotePrimaryReplicaQuery { RepositoryId = RepositoryId.From(repositoryId) },
                CancellationToken.None
            );

            Assert.False(result.Get().Promoted);
            Assert.Empty(provider.GetRequiredService<RecordingLfsRepairQueue>().Scheduled);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenTheUnhealthyPrimaryStillAnswers_KeepsItAsThePrimary()
    {
        // Heartbeats also stop when the API or the database cannot take them (2026-10-18: a database stall left
        // every node without a recent heartbeat). A primary that answers directly is up; moving off it is wrong.
        var repositoryId = Guid.NewGuid();
        var oldPrimaryId = Guid.NewGuid();
        var (handler, provider) = await CreateHandlerAsync(
            repositoryId,
            oldPrimaryId,
            Guid.NewGuid(),
            primaryHealthy: false,
            replicaWatermark: 5,
            primaryWatermark: 2,
            replicationEpoch: 3,
            primaryAnswers: true
        );

        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new PromotePrimaryReplicaQuery { RepositoryId = RepositoryId.From(repositoryId) },
                CancellationToken.None
            );

            Assert.False(result.Get().Promoted);
            var repository = await LoadRepositoryAsync(provider, repositoryId);
            Assert.Equal(oldPrimaryId, repository.PrimaryStorageNodeId);
            Assert.Equal(3, repository.ReplicationEpoch);
            Assert.Empty(provider.GetRequiredService<RecordingLfsRepairQueue>().Scheduled);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenTheUnhealthyPrimaryStillAnswers_StartsNoColdRecovery()
    {
        var repositoryId = Guid.NewGuid();
        var (handler, provider) = await CreateRf4HandlerAsync(
            repositoryId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            primaryWatermark: 5,
            readWatermark: 5,
            readReplicaHealthy: false,
            primaryAnswers: true
        );

        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new PromotePrimaryReplicaQuery { RepositoryId = RepositoryId.From(repositoryId) },
                CancellationToken.None
            );

            Assert.False(result.Get().Promoted);
            await provider.GetRequiredService<IColdRecoveryService>()
                .DidNotReceiveWithAnyArgs()
                .TryRecoverAsync(default, default);
            Assert.Equal(ReplicationState.Rf4Healthy, (await LoadRepositoryAsync(provider, repositoryId)).ReplicationState);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenNoPlaintextCopyIsHealthyAndThePrimaryIsDown_StartsColdRecovery()
    {
        var repositoryId = Guid.NewGuid();
        var (handler, provider) = await CreateRf4HandlerAsync(
            repositoryId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            primaryWatermark: 5,
            readWatermark: 5,
            readReplicaHealthy: false
        );

        await using (provider)
        {
            await handler.RunQueryAsync(
                new PromotePrimaryReplicaQuery { RepositoryId = RepositoryId.From(repositoryId) },
                CancellationToken.None
            );

            await provider.GetRequiredService<IColdRecoveryService>()
                .Received(1)
                .TryRecoverAsync(repositoryId, Arg.Any<CancellationToken>());
        }
    }

    private static async Task<RepositoryEntity> LoadRepositoryAsync(ServiceProvider provider, Guid repositoryId)
    {
        var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Set<RepositoryEntity>().AsNoTracking().SingleAsync(repository => repository.Id == repositoryId);
    }

    private static async Task<(PromotePrimaryReplicaQueryHandler Handler, ServiceProvider Provider)>
        CreateRf4HandlerAsync(
            Guid repositoryId,
            Guid oldPrimaryId,
            Guid readReplicaId,
            Guid encId,
            long primaryWatermark,
            long readWatermark,
            bool readReplicaHealthy = true,
            bool primaryAnswers = false
        )
    {
        var connection = SqliteTestConnection.OpenInMemory();

        var services = new ServiceCollection();
        services.AddSingleton(connection);
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider(
                [
                    typeof(RepositoryMapsterConfig).Assembly,
                    typeof(global::OpenGitBase.Features.StorageNode.StorageNodeMapsterConfig).Assembly,
                ]
            )
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        services.AddSingleton<IColdRecoveryService>(_ => Substitute.For<IColdRecoveryService>());
        services.AddSingleton(LivenessProbe(primaryAnswers));
        services.AddLogging();
        services.AddSingleton<RecordingLfsRepairQueue>();
        services.AddSingleton<ILfsRepairQueue>(sp => sp.GetRequiredService<RecordingLfsRepairQueue>());
        services.AddSingleton<PromotePrimaryReplicaQueryHandler>();

        var provider = services.BuildServiceProvider();
        var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
            context.Set<StorageNodeEntity>().AddRange(
                new StorageNodeEntity
                {
                    Id = oldPrimaryId,
                    NodeId = "storage-1",
                    InternalHost = "storage-1",
                    InternalHttpPort = 8081,
                    IsHealthy = false,
                    RegisteredAt = DateTimeOffset.UtcNow,
                },
                new StorageNodeEntity
                {
                    Id = readReplicaId,
                    NodeId = "storage-2",
                    InternalHost = "storage-2",
                    InternalHttpPort = 8081,
                    IsHealthy = readReplicaHealthy,
                    RegisteredAt = DateTimeOffset.UtcNow,
                },
                new StorageNodeEntity
                {
                    Id = encId,
                    NodeId = "storage-3",
                    InternalHost = "storage-3",
                    InternalHttpPort = 8081,
                    IsHealthy = true,
                    RegisteredAt = DateTimeOffset.UtcNow,
                }
            );
            context.Set<RepositoryEntity>().Add(
                new RepositoryEntity
                {
                    Id = repositoryId,
                    Name = "repo",
                    Slug = "repo",
                    OwnerUserId = Guid.NewGuid(),
                    PhysicalPath = $"/srv/git/{repositoryId}.git",
                    StorageNodeId = oldPrimaryId,
                    PrimaryStorageNodeId = oldPrimaryId,
                    PrimaryWatermark = primaryWatermark,
                    ReplicationEpoch = 1,
                    ReplicationState = ReplicationState.Rf4Healthy,
                    Replicas =
                    [
                        new RepositoryReplicaEntity
                        {
                            RepositoryId = repositoryId,
                            StorageNodeId = oldPrimaryId,
                            Role = RepositoryReplicaRole.Primary,
                            AppliedWatermark = primaryWatermark,
                        },
                        new RepositoryReplicaEntity
                        {
                            RepositoryId = repositoryId,
                            StorageNodeId = readReplicaId,
                            Role = RepositoryReplicaRole.ReadReplica,
                            AppliedWatermark = readWatermark,
                        },
                        new RepositoryReplicaEntity
                        {
                            RepositoryId = repositoryId,
                            StorageNodeId = encId,
                            Role = RepositoryReplicaRole.EncryptedReplica,
                            ArtifactWatermark = primaryWatermark,
                        },
                    ],
                }
            );
            await context.SaveChangesAsync();
        }

        return (provider.GetRequiredService<PromotePrimaryReplicaQueryHandler>(), provider);
    }

    private static async Task<(PromotePrimaryReplicaQueryHandler Handler, ServiceProvider Provider)>
        CreateHandlerAsync(
            Guid repositoryId,
            Guid oldPrimaryId,
            Guid replicaId,
            bool primaryHealthy,
            long replicaWatermark,
            long primaryWatermark,
            long replicationEpoch = 1,
            bool primaryAnswers = false
        )
    {
        var connection = SqliteTestConnection.OpenInMemory();

        var services = new ServiceCollection();
        services.AddSingleton(connection);
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider(
                [
                    typeof(RepositoryMapsterConfig).Assembly,
                    typeof(global::OpenGitBase.Features.StorageNode.StorageNodeMapsterConfig).Assembly,
                ]
            )
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        services.AddSingleton<IColdRecoveryService>(_ => Substitute.For<IColdRecoveryService>());
        services.AddSingleton(LivenessProbe(primaryAnswers));
        services.AddLogging();
        services.AddSingleton<RecordingLfsRepairQueue>();
        services.AddSingleton<ILfsRepairQueue>(sp => sp.GetRequiredService<RecordingLfsRepairQueue>());
        services.AddSingleton<PromotePrimaryReplicaQueryHandler>();

        var provider = services.BuildServiceProvider();
        var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
            context.Set<StorageNodeEntity>().AddRange(
                new StorageNodeEntity
                {
                    Id = oldPrimaryId,
                    NodeId = "storage-1",
                    InternalHost = "storage-1",
                    InternalHttpPort = 8081,
                    IsHealthy = primaryHealthy,
                    RegisteredAt = DateTimeOffset.UtcNow,
                },
                new StorageNodeEntity
                {
                    Id = replicaId,
                    NodeId = "storage-2",
                    InternalHost = "storage-2",
                    InternalHttpPort = 8081,
                    IsHealthy = true,
                    RegisteredAt = DateTimeOffset.UtcNow,
                }
            );
            context.Set<RepositoryEntity>().Add(
                new RepositoryEntity
                {
                    Id = repositoryId,
                    Name = "repo",
                    Slug = "repo",
                    OwnerUserId = Guid.NewGuid(),
                    PhysicalPath = $"/srv/git/{repositoryId}.git",
                    StorageNodeId = oldPrimaryId,
                    PrimaryStorageNodeId = oldPrimaryId,
                    PrimaryWatermark = 5,
                    ReplicationEpoch = replicationEpoch,
                    Replicas =
                    [
                        new RepositoryReplicaEntity
                        {
                            RepositoryId = repositoryId,
                            StorageNodeId = oldPrimaryId,
                            Role = RepositoryReplicaRole.Primary,
                            AppliedWatermark = primaryWatermark,
                        },
                        new RepositoryReplicaEntity
                        {
                            RepositoryId = repositoryId,
                            StorageNodeId = replicaId,
                            Role = RepositoryReplicaRole.Replica,
                            AppliedWatermark = replicaWatermark,
                        },
                    ],
                }
            );
            await context.SaveChangesAsync();
        }

        return (provider.GetRequiredService<PromotePrimaryReplicaQueryHandler>(), provider);
    }

    /// <summary>A probe on which only storage-1, the old primary in both fixtures, may answer.</summary>
    private static IStorageNodeLivenessProbe LivenessProbe(bool primaryAnswers)
    {
        var probe = Substitute.For<IStorageNodeLivenessProbe>();
        probe.AnswersAsync("storage-1", 8081, Arg.Any<CancellationToken>()).Returns(primaryAnswers);
        return probe;
    }
}
