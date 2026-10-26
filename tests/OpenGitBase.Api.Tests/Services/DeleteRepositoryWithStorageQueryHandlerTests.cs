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

public class DeleteRepositoryWithStorageQueryHandlerTests
{
    private const string Oid = "1111111111111111111111111111111111111111111111111111111111111111";

    private readonly FakeLfsStorageNodes _lfsNodes = new();

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

    [Fact]
    public async Task RunQueryAsync_DeletesTheLfsRowsAndTheLfsDataOnEveryNode()
    {
        var repositoryId = Guid.NewGuid();
        var nodeIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var (handler, provider, _, provisioner) = await CreateHandlerAsync(repositoryId, nodeIds);
        GitDeleteAnswers(provisioner, _ => 200);
        await SeedLfsAsync(provider, repositoryId, nodeIds);
        var otherRepository = Guid.NewGuid();
        _lfsNodes.Store(nodeIds[0].ToString(), otherRepository, Oid, LfsObjectCopyKind.Plaintext, [1]);

        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new DeleteRepositoryWithStorageQuery { Id = RepositoryId.From(repositoryId) },
                CancellationToken.None
            );

            Assert.True(result.Get().Success);
            await using var context = await provider
                .GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>()
                .CreateDbContextAsync();
            Assert.False(await context.Set<LfsObjectEntity>().AnyAsync());
            Assert.False(await context.Set<LfsObjectCopyEntity>().AnyAsync());
            Assert.All(nodeIds, nodeId => Assert.False(_lfsNodes.Holds(nodeId.ToString(), repositoryId)));
            Assert.True(_lfsNodes.Holds(nodeIds[0].ToString(), otherRepository));
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenAnLfsDeleteFails_StillDeletesTheRepositoryAndCleansTheOtherNodes()
    {
        var repositoryId = Guid.NewGuid();
        var nodeIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var (handler, provider, _, provisioner) = await CreateHandlerAsync(repositoryId, nodeIds);
        GitDeleteAnswers(provisioner, _ => 200);
        await SeedLfsAsync(provider, repositoryId, nodeIds);
        _lfsNodes.FailingHosts.Add(nodeIds[1].ToString());

        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new DeleteRepositoryWithStorageQuery { Id = RepositoryId.From(repositoryId) },
                CancellationToken.None
            );

            Assert.True(result.Get().Success);
            await using var context = await provider
                .GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>()
                .CreateDbContextAsync();
            Assert.False(await context.Set<RepositoryEntity>().AnyAsync());
            Assert.False(_lfsNodes.Holds(nodeIds[0].ToString(), repositoryId));
            Assert.True(_lfsNodes.Holds(nodeIds[1].ToString(), repositoryId));
            Assert.False(_lfsNodes.Holds(nodeIds[2].ToString(), repositoryId));
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenQuorumDeleteFails_KeepsTheLfsRowsAndData()
    {
        var repositoryId = Guid.NewGuid();
        var nodeIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var (handler, provider, _, provisioner) = await CreateHandlerAsync(repositoryId, nodeIds);
        GitDeleteAnswers(provisioner, _ => 503);
        await SeedLfsAsync(provider, repositoryId, nodeIds);

        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new DeleteRepositoryWithStorageQuery { Id = RepositoryId.From(repositoryId) },
                CancellationToken.None
            );

            Assert.False(result.Get().Success);
            await using var context = await provider
                .GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>()
                .CreateDbContextAsync();
            Assert.True(await context.Set<LfsObjectEntity>().AnyAsync());
            Assert.DoesNotContain(_lfsNodes.Requests, request => request.Method == HttpMethod.Delete);
            Assert.All(nodeIds, nodeId => Assert.True(_lfsNodes.Holds(nodeId.ToString(), repositoryId)));
        }
    }

    [Fact]
    public async Task RunQueryAsync_DeletesLfsDataOnANodeThatOnlyHasCopyRows()
    {
        var repositoryId = Guid.NewGuid();
        var nodeIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var formerNode = Guid.NewGuid();
        var (handler, provider, _, provisioner) = await CreateHandlerAsync(
            repositoryId,
            [.. nodeIds, formerNode],
            replicaCount: 3
        );
        GitDeleteAnswers(provisioner, _ => 200);
        await SeedLfsAsync(provider, repositoryId, [.. nodeIds, formerNode]);

        await using (provider)
        {
            await handler.RunQueryAsync(
                new DeleteRepositoryWithStorageQuery { Id = RepositoryId.From(repositoryId) },
                CancellationToken.None
            );

            Assert.False(_lfsNodes.Holds(formerNode.ToString(), repositoryId));
        }
    }

    private static void GitDeleteAnswers(IStorageProvisionerClient provisioner, Func<Guid, int> status) =>
        provisioner
            .DeleteRepositoryAsync(
                Arg.Any<StorageNodeDto>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(callInfo =>
            {
                var code = status(callInfo.ArgAt<StorageNodeDto>(0).Id.Value);
                return Task.FromResult(
                    code == 200 ? StorageProvisionerResult.Ok(code) : StorageProvisionerResult.Fail(code, "unavailable")
                );
            });

    private async Task SeedLfsAsync(ServiceProvider provider, Guid repositoryId, IReadOnlyList<Guid> nodeIds)
    {
        await using var context = await provider
            .GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>()
            .CreateDbContextAsync();
        context.Set<LfsObjectEntity>().Add(
            new LfsObjectEntity { RepositoryId = repositoryId, Oid = Oid, Size = 1, CreatedAt = DateTimeOffset.UtcNow }
        );
        foreach (var nodeId in nodeIds)
        {
            context.Set<LfsObjectCopyEntity>().Add(
                new LfsObjectCopyEntity
                {
                    RepositoryId = repositoryId,
                    Oid = Oid,
                    StorageNodeId = nodeId,
                    Kind = LfsObjectCopyKind.Plaintext,
                    StoredAt = DateTimeOffset.UtcNow,
                }
            );
            _lfsNodes.Store(nodeId.ToString(), repositoryId, Oid, LfsObjectCopyKind.Plaintext, [1]);
            _lfsNodes.Store(nodeId.ToString(), repositoryId, Oid, LfsObjectCopyKind.Encrypted, [2]);
        }

        await context.SaveChangesAsync();
    }

    private async Task<(
        DeleteRepositoryWithStorageQueryHandler Handler,
        ServiceProvider Provider,
        IQueryProcessor QueryProcessor,
        IStorageProvisionerClient Provisioner
    )> CreateHandlerAsync(Guid repositoryId, IReadOnlyList<Guid> nodeIds, int? replicaCount = null)
    {
        var connection = SqliteTestConnection.OpenInMemory();

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
        services.AddSingleton<IStorageLfsClient>(_lfsNodes.CreateClient());
        services.AddLogging();
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
                        .Take(replicaCount ?? nodeIds.Count)
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
