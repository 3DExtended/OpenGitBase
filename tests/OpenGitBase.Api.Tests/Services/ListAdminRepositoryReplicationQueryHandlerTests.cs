using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode;
using OpenGitBase.Features.StorageNode.Entities;
using OpenGitBase.Features.Users;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Api.Tests.Services;

public class ListAdminRepositoryReplicationQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_SeveritySort_PlacesNoQuorumFirst()
    {
        var healthyId = Guid.NewGuid();
        var degradedId = Guid.NewGuid();
        var (handler, provider) = await CreateHandlerAsync(
            SeedHealthyRepository(healthyId, "healthy"),
            SeedRepository(
                degradedId,
                "degraded",
                ReplicationState.Degraded,
                writeQuorum: false
            )
        );

        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new ListAdminRepositoryReplicationQuery
                {
                    Sort = AdminRepositoryReplicationSort.Severity,
                },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            var items = result.Get().Items;
            Assert.Equal(2, items.Count);
            Assert.Equal(degradedId, items[0].RepositoryId);
        }
    }

    [Fact]
    public async Task RunQueryAsync_AttentionBackfilling_FiltersResults()
    {
        var backfillId = Guid.NewGuid();
        var healthyId = Guid.NewGuid();
        var (handler, provider) = await CreateHandlerAsync(
            SeedRepository(
                backfillId,
                "backfill",
                ReplicationState.Rf1Backfilling,
                replicaCount: 2
            ),
            SeedHealthyRepository(healthyId, "healthy")
        );

        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new ListAdminRepositoryReplicationQuery
                {
                    Attention = ReplicationAttentionPreset.Backfilling,
                },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            var items = result.Get().Items;
            Assert.Single(items);
            Assert.Equal(backfillId, items[0].RepositoryId);
        }
    }

    [Fact]
    public async Task RunQueryAsync_SearchMatchesOwnerSlug()
    {
        var repoId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var (handler, provider) = await CreateHandlerAsync(
            (context, nodes) =>
            {
                context.Set<UserEntity>().Add(
                    new UserEntity
                    {
                        Id = ownerId,
                        Username = "demo-operator",
                        NormalizedUsername = "DEMO-OPERATOR",
                        CreatedAt = DateTimeOffset.UtcNow,
                    }
                );
                SeedRepository(
                    repoId,
                    "search-me",
                    ReplicationState.Rf3Healthy,
                    ownerId: ownerId
                )(context, nodes);
            }
        );

        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new ListAdminRepositoryReplicationQuery { Search = "demo-operator" },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            Assert.Single(result.Get().Items);
            Assert.Equal("demo-operator", result.Get().Items[0].OwnerSlug);
        }
    }

    [Fact]
    public async Task RunQueryAsync_PaginatesResults()
    {
        var repos = Enumerable.Range(0, 3)
            .Select(index => SeedHealthyRepository(Guid.NewGuid(), $"repo-{index}"))
            .ToArray();

        var (handler, provider) = await CreateHandlerAsync(repos);

        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new ListAdminRepositoryReplicationQuery { Page = 2, PageSize = 1 },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            var payload = result.Get();
            Assert.Equal(3, payload.TotalCount);
            Assert.Single(payload.Items);
            Assert.Equal(2, payload.Page);
        }
    }

    private static Action<OpenGitBaseDbContext, IReadOnlyList<StorageNodeEntity>> SeedHealthyRepository(
        Guid repositoryId,
        string name
    ) =>
        SeedRepository(repositoryId, name, ReplicationState.Rf3Healthy);

    private static Action<OpenGitBaseDbContext, IReadOnlyList<StorageNodeEntity>> SeedRepository(
        Guid repositoryId,
        string name,
        ReplicationState state,
        int replicaCount = 3,
        bool writeQuorum = true,
        Guid? ownerId = null
    ) =>
        (context, nodes) =>
        {
            var primaryNode = nodes[0];
            var replicas = nodes
                .Take(replicaCount)
                .Select(
                    (node, index) =>
                        new RepositoryReplicaEntity
                        {
                            RepositoryId = repositoryId,
                            StorageNodeId = node.Id,
                            Role = index == 0
                                ? RepositoryReplicaRole.Primary
                                : RepositoryReplicaRole.Replica,
                            AppliedWatermark = writeQuorum ? 2 : 1,
                            LastSyncedAt = DateTimeOffset.UtcNow.AddMinutes(-index),
                        }
                )
                .ToList();

            context.Set<RepositoryEntity>().Add(
                new RepositoryEntity
                {
                    Id = repositoryId,
                    Name = name,
                    Slug = name,
                    OwnerUserId = ownerId ?? Guid.NewGuid(),
                    PhysicalPath = $"/srv/git/{repositoryId}.git",
                    StorageNodeId = primaryNode.Id,
                    PrimaryStorageNodeId = primaryNode.Id,
                    PrimaryWatermark = 2,
                    ReplicationEpoch = 1,
                    ReplicationState = state,
                    Replicas = replicas,
                }
            );
        };

    private static async Task<(
        ListAdminRepositoryReplicationQueryHandler Handler,
        ServiceProvider Provider
    )> CreateHandlerAsync(
        params Action<OpenGitBaseDbContext, IReadOnlyList<StorageNodeEntity>>[] seedActions
    )
    {
        var connection = SqliteTestConnection.OpenInMemory();

        var services = new ServiceCollection();
        services.AddSingleton(connection);
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider(
                [
                    typeof(RepositoryMapsterConfig).Assembly,
                    typeof(StorageNodeMapsterConfig).Assembly,
                    typeof(UsersMapsterConfig).Assembly,
                ]
            )
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        services.AddSingleton<ListAdminRepositoryReplicationQueryHandler>();

        var provider = services.BuildServiceProvider();
        var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
            var nodes = CreateNodes(context);
            await context.SaveChangesAsync();

            foreach (var seed in seedActions)
            {
                seed(context, nodes);
            }

            await context.SaveChangesAsync();
        }

        return (provider.GetRequiredService<ListAdminRepositoryReplicationQueryHandler>(), provider);
    }

    private static IReadOnlyList<StorageNodeEntity> CreateNodes(OpenGitBaseDbContext context)
    {
        var nodes = Enumerable
            .Range(1, 3)
            .Select(index => new StorageNodeEntity
            {
                Id = Guid.NewGuid(),
                NodeId = $"storage-{index}",
                InternalHost = $"storage-{index}",
                InternalHttpPort = 8081,
                InternalGitHttpPort = 8082,
                IsHealthy = true,
            })
            .ToList();

        context.Set<StorageNodeEntity>().AddRange(nodes);
        return nodes;
    }
}
