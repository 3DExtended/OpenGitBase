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

public class DeleteRepositoryWithStorageQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenTwoOfThreeDeleteSucceeds_RemovesDatabaseRow()
    {
        var repositoryId = Guid.NewGuid();
        var nodeIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var (handler, provider, _, provisioner) = await CreateHandlerAsync(repositoryId, nodeIds);

        provisioner
            .DeleteRepositoryAsync(
                Arg.Any<StorageNodeDto>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                callInfo =>
                {
                    var node = callInfo.ArgAt<StorageNodeDto>(0);
                    return Task.FromResult(
                        StorageProvisionerResult.Ok(
                            node.Id.Value == nodeIds[2] ? 503 : 200
                        )
                    );
                }
            );

        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new DeleteRepositoryWithStorageQuery
                {
                    Id = RepositoryId.From(repositoryId),
                },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            Assert.True(result.Get().Success);

            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            await using var context = await contextFactory.CreateDbContextAsync();
            Assert.False(await context.Set<RepositoryEntity>().AnyAsync());
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenQuorumDeleteFails_DoesNotRemoveDatabaseRow()
    {
        var repositoryId = Guid.NewGuid();
        var nodeIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var (handler, provider, _, provisioner) = await CreateHandlerAsync(repositoryId, nodeIds);

        provisioner
            .DeleteRepositoryAsync(
                Arg.Any<StorageNodeDto>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Task.FromResult(StorageProvisionerResult.Fail(503, "unavailable")));

        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new DeleteRepositoryWithStorageQuery
                {
                    Id = RepositoryId.From(repositoryId),
                },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            Assert.False(result.Get().Success);

            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            await using var context = await contextFactory.CreateDbContextAsync();
            Assert.True(await context.Set<RepositoryEntity>().AnyAsync());
        }
    }

    private static async Task<(
        DeleteRepositoryWithStorageQueryHandler Handler,
        ServiceProvider Provider,
        IQueryProcessor QueryProcessor,
        IStorageProvisionerClient Provisioner
    )> CreateHandlerAsync(Guid repositoryId, IReadOnlyList<Guid> nodeIds)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var queryProcessor = Substitute.For<IQueryProcessor>();
        var provisioner = Substitute.For<IStorageProvisionerClient>();

        foreach (var nodeId in nodeIds)
        {
            var storageNodeId = StorageNodeId.From(nodeId);
            queryProcessor
                .RunQueryAsync(
                    Arg.Is<GetStorageNodeQuery>(query => query.ModelId == storageNodeId),
                    Arg.Any<CancellationToken>()
                )
                .Returns(
                    Option.From(
                        new StorageNodeDto
                        {
                            Id = storageNodeId,
                            NodeId = nodeId.ToString(),
                            InternalHost = nodeId.ToString(),
                            InternalHttpPort = 8081,
                            IsHealthy = true,
                        }
                    )
                );
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
        services.AddSingleton<DeleteRepositoryWithStorageQueryHandler>();

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
                    StorageNodeId = nodeIds[0],
                    PrimaryStorageNodeId = nodeIds[0],
                    Replicas = nodeIds
                        .Select(
                            (nodeId, index) =>
                                new RepositoryReplicaEntity
                                {
                                    RepositoryId = repositoryId,
                                    StorageNodeId = nodeId,
                                    Role = index == 0
                                        ? RepositoryReplicaRole.Primary
                                        : RepositoryReplicaRole.Replica,
                                }
                        )
                        .ToList(),
                }
            );
            await context.SaveChangesAsync();
        }

        return (
            provider.GetRequiredService<DeleteRepositoryWithStorageQueryHandler>(),
            provider,
            queryProcessor,
            provisioner
        );
    }
}
