using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Tests.Services;

public class CreateRepositoryWithStorageQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenFewerThanThreeHealthyNodes_ReturnsFailed()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(
                Arg.Any<ListHealthyStorageNodesQuery>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Option.From<IReadOnlyList<StorageNodeDto>>(CreateNodes(2)));

        var handler = CreateHandler(queryProcessor, Substitute.For<IStorageProvisionerClient>());

        var result = await handler.RunQueryAsync(
            CreateQuery(),
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Null(result.Get().RepositoryId);
        Assert.NotNull(result.Get().Error);
        Assert.Contains("three healthy storage nodes", result.Get().Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunQueryAsync_ProvisionsAllThreeNodesAndPersistsReplicaRows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var nodes = CreateNodes(3);
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(
                Arg.Any<ListHealthyStorageNodesQuery>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Option.From<IReadOnlyList<StorageNodeDto>>(nodes));

        foreach (var node in nodes)
        {
            queryProcessor
                .RunQueryAsync(
                    Arg.Is<GetStorageNodeApiTokenQuery>(query =>
                        query.StorageNodeId == node.Id
                    ),
                    Arg.Any<CancellationToken>()
                )
                .Returns(Option.From($"token-{node.NodeId}"));
        }

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
            .Returns(new StorageProvisionerResult { Success = true });

        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(RepositoryMapsterConfig).Assembly])
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        var mapsterConfig = new TypeAdapterConfig();
        new RepositoryMapsterConfig().Register(mapsterConfig);
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(
            sp.GetRequiredService<TypeAdapterConfig>()
        ));
        var repositoryKeyService = Substitute.For<IRepositoryKeyService>();
        repositoryKeyService
            .GenerateAndStoreKeyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(1);

        services.AddSingleton(queryProcessor);
        services.AddSingleton(provisioner);
        services.AddSingleton(repositoryKeyService);
        services.AddSingleton(new RepositoryStorageQuotaOptions { Enabled = false });
        services.AddSingleton<CreateRepositoryWithStorageQueryHandler>();

        await using var serviceProvider = services.BuildServiceProvider();
        var contextFactory = serviceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
        }

        var handler = serviceProvider.GetRequiredService<CreateRepositoryWithStorageQueryHandler>();
        var result = await handler.RunQueryAsync(CreateQuery(), CancellationToken.None);

        Assert.True(result.IsSome);
        Assert.NotNull(result.Get().RepositoryId);
        Assert.Null(result.Get().Error);

        await provisioner.Received(3).ProvisionRepositoryAsync(
            Arg.Any<StorageNodeDto>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<long>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()
        );

        await using var verifyContext = await contextFactory.CreateDbContextAsync();
        var repository = await verifyContext
            .Set<RepositoryEntity>()
            .Include(entity => entity.Replicas)
            .SingleAsync();
        Assert.Equal(ReplicationState.Rf4Healthy, repository.ReplicationState);
        Assert.Equal(4, repository.Replicas.Count);
        Assert.Equal(1, repository.Replicas.Count(replica => replica.Role == RepositoryReplicaRole.Primary));
        Assert.Equal(1, repository.Replicas.Count(replica => replica.Role == RepositoryReplicaRole.ReadReplica));
        Assert.Equal(2, repository.Replicas.Count(replica => replica.Role == RepositoryReplicaRole.EncryptedReplica));
    }

    [Fact]
    public async Task RunQueryAsync_WhenSecondProvisionFails_RollsBackFirst()
    {
        var nodes = CreateNodes(3);
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(
                Arg.Any<ListHealthyStorageNodesQuery>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Option.From<IReadOnlyList<StorageNodeDto>>(nodes));

        foreach (var node in nodes)
        {
            queryProcessor
                .RunQueryAsync(
                    Arg.Is<GetStorageNodeApiTokenQuery>(query =>
                        query.StorageNodeId == node.Id
                    ),
                    Arg.Any<CancellationToken>()
                )
                .Returns(Option.From($"token-{node.NodeId}"));
        }

        var provisioner = Substitute.For<IStorageProvisionerClient>();
        var provisionCalls = 0;
        provisioner
            .ProvisionRepositoryAsync(
                Arg.Any<StorageNodeDto>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(_ =>
            {
                provisionCalls++;
                return provisionCalls == 1
                    ? new StorageProvisionerResult { Success = true }
                    : new StorageProvisionerResult { Success = false, Error = "disk full" };
            });

        var handler = CreateHandler(queryProcessor, provisioner);

        var result = await handler.RunQueryAsync(CreateQuery(), CancellationToken.None);

        Assert.True(result.IsSome);
        Assert.Null(result.Get().RepositoryId);
        Assert.NotNull(result.Get().Error);
        await provisioner.Received(1).DeleteRepositoryAsync(
            Arg.Any<StorageNodeDto>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()
        );
    }

    private static CreateRepositoryWithStorageQueryHandler CreateHandler(
        IQueryProcessor queryProcessor,
        IStorageProvisionerClient provisioner
    )
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(RepositoryMapsterConfig).Assembly])
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        var mapsterConfig = new TypeAdapterConfig();
        new RepositoryMapsterConfig().Register(mapsterConfig);
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(
            sp.GetRequiredService<TypeAdapterConfig>()
        ));
        services.AddSingleton(queryProcessor);
        services.AddSingleton(provisioner);
        services.AddSingleton<IRepositoryKeyService>(Substitute.For<IRepositoryKeyService>());
        services.AddSingleton(new RepositoryStorageQuotaOptions { Enabled = false });
        var serviceProvider = services.BuildServiceProvider();
        return new CreateRepositoryWithStorageQueryHandler(
            queryProcessor,
            provisioner,
            serviceProvider.GetRequiredService<IRepositoryKeyService>(),
            serviceProvider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>(),
            serviceProvider.GetRequiredService<IMapper>(),
            new RepositoryStorageQuotaOptions { Enabled = false }
        );
    }

    private static CreateRepositoryWithStorageQuery CreateQuery()
    {
        var ownerUserId = Guid.NewGuid();
        return new CreateRepositoryWithStorageQuery
        {
            ModelToCreate = new RepositoryDto
            {
                Name = "Sample",
                OwnerUserId = UserId.From(ownerUserId),
                Slug = "sample",
                PhysicalPath = string.Empty,
            },
        };
    }

    private static IReadOnlyList<StorageNodeDto> CreateNodes(int count)
    {
        return Enumerable
            .Range(1, count)
            .Select(index =>
            {
                var nodeId = $"storage-{index}";
                return new StorageNodeDto
                {
                    Id = StorageNodeId.From(Guid.NewGuid()),
                    NodeId = nodeId,
                    InternalHost = nodeId,
                    InternalHttpPort = 8081,
                    FreeBytesAvailable = 1000 - index,
                    IsHealthy = true,
                };
            })
            .ToList();
    }
}
