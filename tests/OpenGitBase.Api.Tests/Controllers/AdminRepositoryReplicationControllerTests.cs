using Mapster;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Models;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Api.Tests.Controllers;

public class AdminRepositoryReplicationControllerTests
{
    [Fact]
    public async Task GetRepositoryStatus_WhenRepositoryMissing_ReturnsNotFound()
    {
        var (controller, provider) = await CreateControllerAsync();
        await using (provider)
        {
            var result = await controller.GetRepositoryStatus(
                Guid.NewGuid(),
                CancellationToken.None
            );

            Assert.IsType<NotFoundResult>(result.Result);
        }
    }

    [Fact]
    public async Task GetRepositoryStatus_WhenRepositoryExists_ReturnsReplicationDetail()
    {
        var repositoryId = Guid.NewGuid();
        var primaryNodeId = Guid.NewGuid();
        var (controller, provider) = await CreateControllerAsync();
        await using (provider)
        {
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            await using (var context = await contextFactory.CreateDbContextAsync())
            {
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
                        PrimaryWatermark = 2,
                        ReplicationEpoch = 1,
                        ReplicationState = ReplicationState.Rf3Healthy,
                        Replicas =
                        [
                            new RepositoryReplicaEntity
                            {
                                RepositoryId = repositoryId,
                                StorageNodeId = primaryNodeId,
                                Role = RepositoryReplicaRole.Primary,
                                AppliedWatermark = 2,
                            },
                        ],
                    }
                );
                await context.SaveChangesAsync();
            }

            var result = await controller.GetRepositoryStatus(
                repositoryId,
                CancellationToken.None
            );

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<AdminRepositoryReplicationStatusResponse>(ok.Value);
            Assert.Equal("Rf3Healthy", body.ReplicationState);
            Assert.Single(body.Replicas);
        }
    }

    [Fact]
    public async Task GetRepositoryStatus_WhenRf4Healthy_ReturnsFourCopyReplicas()
    {
        var repositoryId = Guid.NewGuid();
        var primaryNodeId = Guid.NewGuid();
        var readNodeId = Guid.NewGuid();
        var encAId = Guid.NewGuid();
        var encBId = Guid.NewGuid();
        var (controller, provider) = await CreateControllerAsync();
        await using (provider)
        {
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            await using (var context = await contextFactory.CreateDbContextAsync())
            {
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
                        Id = readNodeId,
                        NodeId = "storage-2",
                        InternalHost = "storage-2",
                        InternalHttpPort = 8081,
                        IsHealthy = true,
                        RegisteredAt = DateTimeOffset.UtcNow,
                    },
                    new StorageNodeEntity
                    {
                        Id = encAId,
                        NodeId = "storage-3",
                        InternalHost = "storage-3",
                        InternalHttpPort = 8081,
                        IsHealthy = true,
                        RegisteredAt = DateTimeOffset.UtcNow,
                    },
                    new StorageNodeEntity
                    {
                        Id = encBId,
                        NodeId = "storage-4",
                        InternalHost = "storage-4",
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
                        ReplicationEpoch = 2,
                        ReplicationState = ReplicationState.Rf4Healthy,
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
                                StorageNodeId = readNodeId,
                                Role = RepositoryReplicaRole.ReadReplica,
                                AppliedWatermark = 3,
                            },
                            new RepositoryReplicaEntity
                            {
                                RepositoryId = repositoryId,
                                StorageNodeId = encAId,
                                Role = RepositoryReplicaRole.EncryptedReplica,
                                AppliedWatermark = 0,
                                ArtifactWatermark = 3,
                            },
                            new RepositoryReplicaEntity
                            {
                                RepositoryId = repositoryId,
                                StorageNodeId = encBId,
                                Role = RepositoryReplicaRole.EncryptedReplica,
                                AppliedWatermark = 0,
                                ArtifactWatermark = 2,
                            },
                        ],
                    }
                );
                await context.SaveChangesAsync();
            }

            var result = await controller.GetRepositoryStatus(
                repositoryId,
                CancellationToken.None
            );

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<AdminRepositoryReplicationStatusResponse>(ok.Value);
            Assert.Equal("Rf4Healthy", body.ReplicationState);
            Assert.Equal(4, body.Replicas.Count);
            Assert.Contains(
                body.Replicas,
                replica => replica.Role == nameof(RepositoryReplicaRole.EncryptedReplica)
                    && replica.ArtifactWatermark == 3
            );
        }
    }

    [Fact]
    public async Task GetRepositoryStatus_ReportsLfsObjectsAndTheCopiesEachNodeMisses()
    {
        var repositoryId = Guid.NewGuid();
        var primaryNodeId = Guid.NewGuid();
        var encryptedNodeId = Guid.NewGuid();
        var (controller, provider) = await CreateControllerAsync();
        await using (provider)
        {
            await SeedRepositoryAsync(provider, repositoryId, primaryNodeId, ("storage-1", primaryNodeId), ("storage-3", encryptedNodeId));
            provider
                .GetRequiredService<IQueryProcessor>()
                .RunQueryAsync(
                    Arg.Is<GetLfsCopyStatusQuery>(query => query.RepositoryId.Value == repositoryId),
                    Arg.Any<CancellationToken>()
                )
                .Returns(
                    Option.From(
                        new LfsCopyStatusDto
                        {
                            ObjectCount = 3,
                            TotalBytes = 3_000,
                            Nodes =
                            [
                                new LfsNodeCopyStatusDto
                                {
                                    StorageNodeId = primaryNodeId,
                                    Kind = LfsObjectCopyKind.Plaintext,
                                    IsPrimary = true,
                                    Copies = 1,
                                    Missing = 2,
                                },
                                new LfsNodeCopyStatusDto
                                {
                                    StorageNodeId = encryptedNodeId,
                                    Kind = LfsObjectCopyKind.Encrypted,
                                    Copies = 3,
                                },
                            ],
                        }
                    )
                );

            var result = await controller.GetRepositoryStatus(repositoryId, CancellationToken.None);

            var body = Assert.IsType<AdminRepositoryReplicationStatusResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
            Assert.NotNull(body.Lfs);
            Assert.Equal((3, 3_000L, 2), (body.Lfs.ObjectCount, body.Lfs.TotalBytes, body.Lfs.MissingCopies));
            Assert.Collection(
                body.Lfs.Nodes,
                node => Assert.Equal(("storage-1", "Plaintext", true, 1, 2), (node.NodeId, node.Kind, node.IsPrimary, node.Copies, node.Missing)),
                node => Assert.Equal(("storage-3", "Encrypted", false, 3, 0), (node.NodeId, node.Kind, node.IsPrimary, node.Copies, node.Missing))
            );
        }
    }

    [Fact]
    public async Task GetRepositoryStatus_OfARepositoryWithoutLfs_ReportsNoLfsObjects()
    {
        var repositoryId = Guid.NewGuid();
        var primaryNodeId = Guid.NewGuid();
        var (controller, provider) = await CreateControllerAsync();
        await using (provider)
        {
            await SeedRepositoryAsync(provider, repositoryId, primaryNodeId, ("storage-1", primaryNodeId));

            var result = await controller.GetRepositoryStatus(repositoryId, CancellationToken.None);

            var body = Assert.IsType<AdminRepositoryReplicationStatusResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
            Assert.NotNull(body.Lfs);
            Assert.Equal((0, 0L, 0), (body.Lfs.ObjectCount, body.Lfs.TotalBytes, body.Lfs.MissingCopies));
        }
    }

    [Fact]
    public async Task GetLfsSummary_ReturnsTheFleetTotals()
    {
        var (controller, provider) = await CreateControllerAsync();
        await using (provider)
        {
            provider
                .GetRequiredService<IQueryProcessor>()
                .RunQueryAsync(Arg.Any<GetLfsFleetTotalsQuery>(), Arg.Any<CancellationToken>())
                .Returns(
                    Option.From(
                        new LfsFleetTotalsDto
                        {
                            ObjectCount = 12,
                            TotalBytes = 9_000_000,
                            MissingCopies = 4,
                            RepositoriesWithMissingCopies = 2,
                        }
                    )
                );

            var result = await controller.GetLfsSummary(CancellationToken.None);

            var body = Assert.IsType<AdminLfsFleetSummaryResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
            Assert.Equal((12, 9_000_000L, 4, 2), (body.ObjectCount, body.TotalBytes, body.MissingCopies, body.RepositoriesWithMissingCopies));
        }
    }

    private static async Task SeedRepositoryAsync(
        ServiceProvider provider,
        Guid repositoryId,
        Guid primaryNodeId,
        params (string Name, Guid Id)[] nodes
    )
    {
        await using var context = await provider
            .GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>()
            .CreateDbContextAsync();
        foreach (var (name, id) in nodes)
        {
            context.Set<StorageNodeEntity>().Add(
                new StorageNodeEntity
                {
                    Id = id,
                    NodeId = name,
                    InternalHost = name,
                    InternalHttpPort = 8081,
                    IsHealthy = true,
                    RegisteredAt = DateTimeOffset.UtcNow,
                }
            );
        }

        // Only the primary has a replica row: the LFS figures name nodes from the copy status, not from the rows.
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
                ReplicationState = ReplicationState.Rf3Healthy,
                Replicas =
                [
                    new RepositoryReplicaEntity
                    {
                        RepositoryId = repositoryId,
                        StorageNodeId = primaryNodeId,
                        Role = RepositoryReplicaRole.Primary,
                    },
                ],
            }
        );
        await context.SaveChangesAsync();
    }

    private static async Task<(
        AdminRepositoryReplicationController Controller,
        ServiceProvider Provider
    )> CreateControllerAsync()
    {
        var connection = SqliteTestConnection.OpenInMemory();

        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(
                Arg.Any<RepositoryReplicationRoutingQuery>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Option.From(
                    new RepositoryReplicationRoutingDto { WriteQuorumAvailable = true }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetLfsCopyStatusQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(new LfsCopyStatusDto()));

        var services = new ServiceCollection();
        services.AddSingleton(connection);
        services.AddSingleton(queryProcessor);
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
        }

        var controller = new AdminRepositoryReplicationController(contextFactory, queryProcessor);
        return (controller, provider);
    }
}
