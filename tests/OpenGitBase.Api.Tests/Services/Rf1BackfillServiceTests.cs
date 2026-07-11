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

public class Rf1BackfillServiceTests
{
    [Fact]
    public async Task RunOnceAsync_WhenSingleReplica_AddsTwoAdditionalReplicas()
    {
        var repositoryId = Guid.NewGuid();
        var primaryNodeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var nodeTwoId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var nodeThreeId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var (service, provider) = await CreateServiceAsync(
            repositoryId,
            primaryNodeId,
            [primaryNodeId, nodeTwoId, nodeThreeId]
        );

        await using (provider)
        {
            await service.RunOnceAsync(CancellationToken.None);

            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            await using var context = await contextFactory.CreateDbContextAsync();
            var repository = await context
                .Set<RepositoryEntity>()
                .Include(entity => entity.Replicas)
                .SingleAsync();
            Assert.Equal(3, repository.Replicas.Count);
            Assert.Equal(ReplicationState.Rf3Healthy, repository.ReplicationState);
        }
    }

    private static async Task<(Rf1BackfillService Service, ServiceProvider Provider)>
        CreateServiceAsync(
        Guid repositoryId,
        Guid primaryNodeId,
        IReadOnlyList<Guid> healthyNodeIds
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
                        NodeId = $"storage-{index + 1}",
                        InternalHost = $"storage-{index + 1}",
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

        var provider = services.BuildServiceProvider();
        var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
            foreach (var nodeId in healthyNodeIds)
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
                    StorageNodeId = primaryNodeId,
                    PrimaryStorageNodeId = primaryNodeId,
                    ReplicationState = ReplicationState.Rf1Backfilling,
                    Replicas = [],
                }
            );
            await context.SaveChangesAsync();
        }

        return (provider.GetRequiredService<Rf1BackfillService>(), provider);
    }
}
