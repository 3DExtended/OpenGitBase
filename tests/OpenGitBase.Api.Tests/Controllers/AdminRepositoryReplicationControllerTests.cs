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

    private static async Task<(
        AdminRepositoryReplicationController Controller,
        ServiceProvider Provider
    )> CreateControllerAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

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
