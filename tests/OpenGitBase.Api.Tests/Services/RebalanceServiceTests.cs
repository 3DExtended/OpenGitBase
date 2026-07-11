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

    private static async Task<(RebalanceService Service, ServiceProvider Provider)>
        CreateServiceAsync(
            Guid repositoryId,
            Guid primaryNodeId,
            Guid originalReplicaId,
            Guid replacementNodeId
        )
    {
        var connection = SqliteTestConnection.OpenInMemory();

        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<ListHealthyStorageNodesQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From<IReadOnlyList<StorageNodeDto>>(
                    [
                        CreateNode(primaryNodeId, "storage-1"),
                        CreateNode(originalReplicaId, "storage-2"),
                        CreateNode(replacementNodeId, "storage-3"),
                    ]
                )
            );

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
            context.Set<StorageNodeEntity>().AddRange(
                new StorageNodeEntity
                {
                    Id = primaryNodeId,
                    NodeId = "storage-1",
                    InternalHost = "storage-1",
                    InternalHttpPort = 8081,
                    IsHealthy = true,
                    RegisteredAt = DateTimeOffset.UtcNow,
                },
                new StorageNodeEntity
                {
                    Id = originalReplicaId,
                    NodeId = "storage-2",
                    InternalHost = "storage-2",
                    InternalHttpPort = 8081,
                    IsHealthy = false,
                    RegisteredAt = DateTimeOffset.UtcNow,
                },
                new StorageNodeEntity
                {
                    Id = replacementNodeId,
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
                    StorageNodeId = primaryNodeId,
                    PrimaryStorageNodeId = primaryNodeId,
                    PrimaryWatermark = 3,
                    ReplicationState = ReplicationState.Degraded,
                    Replicas =
                    [
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
                        },
                    ],
                }
            );
            await context.SaveChangesAsync();
        }

        return (provider.GetRequiredService<RebalanceService>(), provider);
    }

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
