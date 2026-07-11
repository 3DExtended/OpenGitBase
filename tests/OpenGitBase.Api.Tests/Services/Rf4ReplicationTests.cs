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

public class Rf4ReplicationTests
{
    [Fact]
    public async Task RepositoryReplicationRoutingQueryHandler_Rf4Healthy_RequiresEncryptedQuorum()
    {
        var repositoryId = Guid.NewGuid();
        var primaryId = Guid.NewGuid();
        var readId = Guid.NewGuid();
        var encA = Guid.NewGuid();
        var encB = Guid.NewGuid();
        var (handler, provider) = await CreateRoutingHandlerAsync(
            repositoryId,
            primaryId,
            readId,
            encA,
            encB,
            encryptedHealthy: false
        );

        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new RepositoryReplicationRoutingQuery
                {
                    RepositoryId = RepositoryId.From(repositoryId),
                },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            Assert.False(result.Get().WriteQuorumAvailable);
            Assert.DoesNotContain(
                result.Get().Targets,
                target => target.Role == nameof(RepositoryReplicaRole.EncryptedReplica)
            );
        }
    }

    [Fact]
    public async Task RepositoryReplicationRoutingQueryHandler_Rf4Healthy_ExcludesEncryptedFromReadTargets()
    {
        var repositoryId = Guid.NewGuid();
        var primaryId = Guid.NewGuid();
        var readId = Guid.NewGuid();
        var encA = Guid.NewGuid();
        var encB = Guid.NewGuid();
        var (handler, provider) = await CreateRoutingHandlerAsync(
            repositoryId,
            primaryId,
            readId,
            encA,
            encB,
            encryptedHealthy: true
        );

        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new RepositoryReplicationRoutingQuery
                {
                    RepositoryId = RepositoryId.From(repositoryId),
                },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            Assert.True(result.Get().WriteQuorumAvailable);
            Assert.Equal(2, result.Get().Targets.Count);
            Assert.Contains(
                result.Get().Targets,
                target => target.Role == nameof(RepositoryReplicaRole.ReadReplica)
            );
            Assert.DoesNotContain(
                result.Get().Targets,
                target => target.Role == nameof(RepositoryReplicaRole.EncryptedReplica)
            );
        }
    }

    [Fact]
    public async Task QuorumReplicateRepositoryQueryHandler_Rf4Healthy_RequiresEncryptedConfirmation()
    {
        var repositoryId = Guid.NewGuid();
        var primaryId = Guid.NewGuid();
        var encId = Guid.NewGuid();
        var (handler, provider) = await CreateQuorumHandlerAsync(
            repositoryId,
            primaryId,
            encId,
            configureProcessor: null
        );

        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new QuorumReplicateRepositoryQuery
                {
                    RepositoryId = RepositoryId.From(repositoryId),
                    StorageNodeId = StorageNodeId.From(primaryId),
                    AppliedWatermark = 1,
                    ConfirmedEncryptedNodeIds = [],
                },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            Assert.False(result.Get().Success);
            Assert.Contains("encrypted", result.Get().Error!, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task QuorumReplicateRepositoryQueryHandler_Rf4Healthy_SucceedsWithEncryptedConfirmation()
    {
        var repositoryId = Guid.NewGuid();
        var primaryId = Guid.NewGuid();
        var encId = Guid.NewGuid();
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureRf4ContextQuery(queryProcessor, repositoryId, primaryId, encId, primaryWatermark: 0);
        queryProcessor
            .RunQueryAsync(
                Arg.Is<GetStorageNodeQuery>(query => query.ModelId.Value == encId),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Option.From(
                    new StorageNodeDto
                    {
                        Id = StorageNodeId.From(encId),
                        NodeId = "storage-enc",
                        InternalHost = "storage-enc",
                        InternalHttpPort = 8081,
                        IsHealthy = true,
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(
                Arg.Is<GetStorageNodeApiTokenQuery>(query => query.StorageNodeId.Value == encId),
                Arg.Any<CancellationToken>()
            )
            .Returns(Option.From("token"));
        queryProcessor
            .RunQueryAsync(
                Arg.Any<CommitReplicationWatermarkQuery>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Option.From(
                    new CommitReplicationWatermarkResult
                    {
                        Success = true,
                        PrimaryWatermark = 1,
                    }
                )
            );

        var (handler, provider) = await CreateQuorumHandlerAsync(
            repositoryId,
            primaryId,
            encId,
            configureProcessor: queryProcessor
        );

        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new QuorumReplicateRepositoryQuery
                {
                    RepositoryId = RepositoryId.From(repositoryId),
                    StorageNodeId = StorageNodeId.From(primaryId),
                    AppliedWatermark = 1,
                    ConfirmedEncryptedNodeIds = [encId],
                },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            Assert.True(result.Get().Success);
            Assert.Equal(1, result.Get().PrimaryWatermark);
        }
    }

    [Fact]
    public async Task QuorumReplicateRepositoryQueryHandler_Rf4Healthy_RejectsMissingArtifact()
    {
        var repositoryId = Guid.NewGuid();
        var primaryId = Guid.NewGuid();
        var encId = Guid.NewGuid();
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureRf4ContextQuery(queryProcessor, repositoryId, primaryId, encId, primaryWatermark: 0);
        queryProcessor
            .RunQueryAsync(
                Arg.Is<GetStorageNodeQuery>(query => query.ModelId.Value == encId),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Option.From(
                    new StorageNodeDto
                    {
                        Id = StorageNodeId.From(encId),
                        NodeId = "storage-enc",
                        InternalHost = "storage-enc",
                        InternalHttpPort = 8081,
                        IsHealthy = true,
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(
                Arg.Is<GetStorageNodeApiTokenQuery>(query => query.StorageNodeId.Value == encId),
                Arg.Any<CancellationToken>()
            )
            .Returns(Option.From("token"));

        var failingProvisioner = Substitute.For<IStorageProvisionerClient>();
        failingProvisioner
            .TryGetReplicationArtifactAsync(
                Arg.Any<StorageNodeDto>(),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<long>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(ReplicationArtifactFetchResult.Fail(404, "missing"));

        var (handler, provider) = await CreateQuorumHandlerAsync(
            repositoryId,
            primaryId,
            encId,
            configureProcessor: queryProcessor,
            provisioner: failingProvisioner
        );

        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new QuorumReplicateRepositoryQuery
                {
                    RepositoryId = RepositoryId.From(repositoryId),
                    StorageNodeId = StorageNodeId.From(primaryId),
                    AppliedWatermark = 1,
                    ConfirmedEncryptedNodeIds = [encId],
                },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            Assert.False(result.Get().Success);
            Assert.Contains("artifact", result.Get().Error!, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void ConfigureRf4ContextQuery(
        IQueryProcessor queryProcessor,
        Guid repositoryId,
        Guid primaryId,
        Guid encId,
        long primaryWatermark
    )
    {
        queryProcessor
            .RunQueryAsync(
                Arg.Any<GetRepositoryReplicationContextQuery>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Option.From(
                    new RepositoryReplicationContextDto
                    {
                        RepositoryId = repositoryId,
                        ReplicationEpoch = 1,
                        PrimaryWatermark = primaryWatermark,
                        IsPrimary = true,
                        PhysicalPath = $"/srv/git/{repositoryId}.git",
                        ReplicationState = nameof(ReplicationState.Rf4Healthy),
                        Peers =
                        [
                            new RepositoryReplicationPeerDto
                            {
                                StorageNodeId = primaryId,
                                InternalHost = "storage-1",
                                InternalHttpPort = 8081,
                                Role = nameof(RepositoryReplicaRole.Primary),
                                IsHealthy = true,
                            },
                            new RepositoryReplicationPeerDto
                            {
                                StorageNodeId = encId,
                                InternalHost = "storage-enc",
                                InternalHttpPort = 8081,
                                Role = nameof(RepositoryReplicaRole.EncryptedReplica),
                                IsHealthy = true,
                            },
                        ],
                    }
                )
            );
    }

    private static async Task<(RepositoryReplicationRoutingQueryHandler Handler, ServiceProvider Provider)>
        CreateRoutingHandlerAsync(
            Guid repositoryId,
            Guid primaryId,
            Guid readId,
            Guid encA,
            Guid encB,
            bool encryptedHealthy
        )
    {
        var connection = SqliteTestConnection.OpenInMemory();

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
        services.AddSingleton<RepositoryReplicationRoutingQueryHandler>();
        var provider = services.BuildServiceProvider();
        var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
            context.Set<StorageNodeEntity>().AddRange(
                Node(primaryId, "storage-1", true),
                Node(readId, "storage-2", true),
                Node(encA, "storage-3", encryptedHealthy),
                Node(encB, "storage-4", encryptedHealthy)
            );
            context.Set<RepositoryEntity>().Add(
                new RepositoryEntity
                {
                    Id = repositoryId,
                    Name = "repo",
                    Slug = "repo",
                    OwnerUserId = Guid.NewGuid(),
                    PhysicalPath = $"/srv/git/{repositoryId}.git",
                    StorageNodeId = primaryId,
                    PrimaryStorageNodeId = primaryId,
                    PrimaryWatermark = 0,
                    ReplicationState = ReplicationState.Rf4Healthy,
                    Replicas =
                    [
                        Replica(repositoryId, primaryId, RepositoryReplicaRole.Primary),
                        Replica(repositoryId, readId, RepositoryReplicaRole.ReadReplica),
                        Replica(repositoryId, encA, RepositoryReplicaRole.EncryptedReplica),
                        Replica(repositoryId, encB, RepositoryReplicaRole.EncryptedReplica),
                    ],
                }
            );
            await context.SaveChangesAsync();
        }

        return (provider.GetRequiredService<RepositoryReplicationRoutingQueryHandler>(), provider);
    }

    private static async Task<(QuorumReplicateRepositoryQueryHandler Handler, ServiceProvider Provider)>
        CreateQuorumHandlerAsync(
            Guid repositoryId,
            Guid primaryId,
            Guid encId,
            IQueryProcessor? configureProcessor,
            IStorageProvisionerClient? provisioner = null
        )
    {
        var connection = SqliteTestConnection.OpenInMemory();

        var queryProcessor = configureProcessor ?? Substitute.For<IQueryProcessor>();
        if (configureProcessor is null)
        {
            ConfigureRf4ContextQuery(queryProcessor, repositoryId, primaryId, encId, primaryWatermark: 0);
        }

        var services = new ServiceCollection();
        services.AddSingleton(connection);
        services.AddSingleton(queryProcessor);
        services.AddSingleton(provisioner ?? (IStorageProvisionerClient)new FakeStorageProvisionerClient());
        services.AddSingleton<QuorumReplicateRepositoryQueryHandler>();
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<QuorumReplicateRepositoryQueryHandler>(), provider);
    }

    private static StorageNodeEntity Node(Guid id, string nodeId, bool healthy) =>
        new()
        {
            Id = id,
            NodeId = nodeId,
            InternalHost = nodeId,
            InternalHttpPort = 8081,
            InternalGitHttpPort = 8082,
            IsHealthy = healthy,
            RegisteredAt = DateTimeOffset.UtcNow,
        };

    private static RepositoryReplicaEntity Replica(
        Guid repositoryId,
        Guid nodeId,
        RepositoryReplicaRole role
    ) =>
        new()
        {
            RepositoryId = repositoryId,
            StorageNodeId = nodeId,
            Role = role,
            AppliedWatermark = 0,
        };
}
