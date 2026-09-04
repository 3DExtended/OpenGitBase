using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Api.Tests.Services;

public class GetFleetDriftReportQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_DetectsOrphanOnDiskAndMissingOnDisk()
    {
        var nodeAId = Guid.NewGuid();
        var nodeBId = Guid.NewGuid();
        // repoTracked is recorded on both nodes; repoOrphan lives only on disk on node A.
        var repoTracked = Guid.NewGuid();
        var repoOrphan = Guid.NewGuid();

        var provisioner = new FakeStorageProvisionerClient
        {
            InventoryProvider = node =>
                node.Id.Value == nodeAId
                    // Node A has the tracked repo AND an unrecorded orphan on disk.
                    ? StorageInventoryResult.Ok([repoTracked, repoOrphan], [])
                    // Node B is recorded for the tracked repo but the repo is not on disk.
                    : StorageInventoryResult.Ok([], []),
        };

        var (handler, provider) = await CreateHandlerAsync(
            provisioner,
            [nodeAId, nodeBId],
            configure: context =>
            {
                context.Set<RepositoryEntity>().Add(
                    new RepositoryEntity
                    {
                        Id = repoTracked,
                        Name = "tracked",
                        Slug = "tracked",
                        OwnerUserId = Guid.NewGuid(),
                        PhysicalPath = $"/srv/git/{repoTracked}.git",
                        Replicas =
                        [
                            new RepositoryReplicaEntity
                            {
                                RepositoryId = repoTracked,
                                StorageNodeId = nodeAId,
                                Role = RepositoryReplicaRole.Primary,
                            },
                            new RepositoryReplicaEntity
                            {
                                RepositoryId = repoTracked,
                                StorageNodeId = nodeBId,
                                Role = RepositoryReplicaRole.Replica,
                            },
                        ],
                    }
                );
            }
        );

        await using (provider)
        {
            var result = await handler.RunQueryAsync(new GetFleetDriftReportQuery(), default);

            Assert.True(result.IsSome);
            var report = result.Get();
            Assert.All(report.Nodes, node => Assert.True(node.Reachable));

            var orphan = Assert.Single(
                report.Drift,
                entry => entry.Kind == FleetDriftKind.OrphanOnDisk
            );
            Assert.Equal(repoOrphan, orphan.RepositoryId);
            Assert.Equal(nodeAId, orphan.StorageNodeId);
            Assert.Null(orphan.RepositoryName); // untracked repo has no Repository row

            var missing = Assert.Single(
                report.Drift,
                entry => entry.Kind == FleetDriftKind.MissingOnDisk
            );
            Assert.Equal(repoTracked, missing.RepositoryId);
            Assert.Equal(nodeBId, missing.StorageNodeId);
            Assert.Equal("tracked", missing.RepositoryName);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenNodeUnreachable_MarksItAndSkipsDrift()
    {
        var nodeId = Guid.NewGuid();
        var repoId = Guid.NewGuid();

        var provisioner = new FakeStorageProvisionerClient
        {
            InventoryProvider = _ => StorageInventoryResult.Fail(502, "unreachable"),
        };

        var (handler, provider) = await CreateHandlerAsync(
            provisioner,
            [nodeId],
            configure: context =>
            {
                context.Set<RepositoryEntity>().Add(
                    new RepositoryEntity
                    {
                        Id = repoId,
                        Name = "repo",
                        Slug = "repo",
                        OwnerUserId = Guid.NewGuid(),
                        PhysicalPath = $"/srv/git/{repoId}.git",
                        Replicas =
                        [
                            new RepositoryReplicaEntity
                            {
                                RepositoryId = repoId,
                                StorageNodeId = nodeId,
                                Role = RepositoryReplicaRole.Primary,
                            },
                        ],
                    }
                );
            }
        );

        await using (provider)
        {
            var result = await handler.RunQueryAsync(new GetFleetDriftReportQuery(), default);

            Assert.True(result.IsSome);
            var report = result.Get();
            var node = Assert.Single(report.Nodes);
            Assert.False(node.Reachable);
            Assert.Equal("unreachable", node.Error);
            // A probe failure must not be reported as MissingOnDisk drift.
            Assert.Empty(report.Drift);
        }
    }

    private static async Task<(GetFleetDriftReportQueryHandler Handler, ServiceProvider Provider)>
        CreateHandlerAsync(
            IStorageProvisionerClient provisioner,
            Guid[] nodeIds,
            Action<OpenGitBaseDbContext> configure
        )
    {
        var connection = SqliteTestConnection.OpenInMemory();

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
                    Arg.Is<GetStorageNodeApiTokenQuery>(query => query.StorageNodeId == storageNodeId),
                    Arg.Any<CancellationToken>()
                )
                .Returns(Option.From("token"));
        }

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

            configure(context);
            await context.SaveChangesAsync();
        }

        var handler = new GetFleetDriftReportQueryHandler(
            contextFactory,
            queryProcessor,
            provisioner
        );
        return (handler, provider);
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
