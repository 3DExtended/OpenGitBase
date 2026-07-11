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
        var (service, provider) = await CreateServiceAsync(
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

    private static async Task<(AntiEntropyReconcilerService Service, ServiceProvider Provider)>
        CreateServiceAsync(
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
