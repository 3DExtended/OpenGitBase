using System.Data.Common;
using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Tests.Services;

public class CommitReplicationWatermarkQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenPrimaryCommitsValidQuorum_IncrementsWatermark()
    {
        var repositoryId = Guid.NewGuid();
        var primaryNodeId = Guid.NewGuid();
        var replicaNodeId = Guid.NewGuid();
        var (handler, provider) = await CreateHandlerAsync(
            repositoryId,
            primaryNodeId,
            replicaNodeId,
            epoch: 1,
            primaryWatermark: 4
        );
        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new CommitReplicationWatermarkQuery
                {
                    RepositoryId = RepositoryId.From(repositoryId),
                    StorageNodeId = StorageNodeId.From(primaryNodeId),
                    ReplicationEpoch = 1,
                    NewWatermark = 5,
                    QuorumNodeIds =
                    [
                        StorageNodeId.From(primaryNodeId),
                        StorageNodeId.From(replicaNodeId),
                    ],
                },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            Assert.True(result.Get().Success);
            Assert.Equal(5, result.Get().PrimaryWatermark);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenEpochStale_RejectsCommit()
    {
        var repositoryId = Guid.NewGuid();
        var primaryNodeId = Guid.NewGuid();
        var replicaNodeId = Guid.NewGuid();
        var (handler, provider) = await CreateHandlerAsync(
            repositoryId,
            primaryNodeId,
            replicaNodeId,
            epoch: 2,
            primaryWatermark: 1
        );
        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new CommitReplicationWatermarkQuery
                {
                    RepositoryId = RepositoryId.From(repositoryId),
                    StorageNodeId = StorageNodeId.From(primaryNodeId),
                    ReplicationEpoch = 1,
                    NewWatermark = 2,
                    QuorumNodeIds =
                    [
                        StorageNodeId.From(primaryNodeId),
                        StorageNodeId.From(replicaNodeId),
                    ],
                },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            Assert.False(result.Get().Success);
            Assert.Contains("epoch", result.Get().Error!, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenNonPrimaryCommits_RejectsCommit()
    {
        var repositoryId = Guid.NewGuid();
        var primaryNodeId = Guid.NewGuid();
        var replicaNodeId = Guid.NewGuid();
        var (handler, provider) = await CreateHandlerAsync(
            repositoryId,
            primaryNodeId,
            replicaNodeId,
            epoch: 1,
            primaryWatermark: 0
        );
        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new CommitReplicationWatermarkQuery
                {
                    RepositoryId = RepositoryId.From(repositoryId),
                    StorageNodeId = StorageNodeId.From(replicaNodeId),
                    ReplicationEpoch = 1,
                    NewWatermark = 1,
                    QuorumNodeIds =
                    [
                        StorageNodeId.From(primaryNodeId),
                        StorageNodeId.From(replicaNodeId),
                    ],
                },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            Assert.False(result.Get().Success);
            Assert.Contains("primary", result.Get().Error!, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenAnotherCommitLandsFirst_RejectsWithoutLoweringTheWatermark()
    {
        // Two API instances (or a hook and the reconciler) both read PrimaryWatermark 4 and both
        // try to commit 5. The interceptor lets the other writer move it to 6 between this
        // handler's read and its write; a blind write would put it back to 5.
        var repositoryId = Guid.NewGuid();
        var primaryNodeId = Guid.NewGuid();
        var replicaNodeId = Guid.NewGuid();
        var concurrentCommit = new ConcurrentCommitInterceptor(bumpPrimaryWatermarkTo: 6);
        var (handler, provider) = await CreateHandlerAsync(
            repositoryId,
            primaryNodeId,
            replicaNodeId,
            epoch: 1,
            primaryWatermark: 4,
            interceptor: concurrentCommit
        );
        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new CommitReplicationWatermarkQuery
                {
                    RepositoryId = RepositoryId.From(repositoryId),
                    StorageNodeId = StorageNodeId.From(primaryNodeId),
                    ReplicationEpoch = 1,
                    NewWatermark = 5,
                    QuorumNodeIds =
                    [
                        StorageNodeId.From(primaryNodeId),
                        StorageNodeId.From(replicaNodeId),
                    ],
                },
                CancellationToken.None
            );

            Assert.True(concurrentCommit.Fired);
            Assert.True(result.IsSome);
            Assert.False(result.Get().Success);
            var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var repository = await context.Set<RepositoryEntity>().SingleAsync(entity => entity.Id == repositoryId);
            Assert.Equal(6, repository.PrimaryWatermark);
        }
    }

    [Fact]
    public async Task RunQueryAsync_NeverLowersAReplicaWatermark()
    {
        // The heartbeat can already report the primary ahead of the watermark being committed.
        var repositoryId = Guid.NewGuid();
        var primaryNodeId = Guid.NewGuid();
        var replicaNodeId = Guid.NewGuid();
        var (handler, provider) = await CreateHandlerAsync(
            repositoryId,
            primaryNodeId,
            replicaNodeId,
            epoch: 1,
            primaryWatermark: 4,
            primaryAppliedWatermark: 7
        );
        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new CommitReplicationWatermarkQuery
                {
                    RepositoryId = RepositoryId.From(repositoryId),
                    StorageNodeId = StorageNodeId.From(primaryNodeId),
                    ReplicationEpoch = 1,
                    NewWatermark = 5,
                    QuorumNodeIds =
                    [
                        StorageNodeId.From(primaryNodeId),
                        StorageNodeId.From(replicaNodeId),
                    ],
                },
                CancellationToken.None
            );

            Assert.True(result.Get().Success);
            var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var replicas = await context
                .Set<RepositoryReplicaEntity>()
                .Where(entity => entity.RepositoryId == repositoryId)
                .ToDictionaryAsync(entity => entity.StorageNodeId);
            Assert.Equal(7, replicas[primaryNodeId].AppliedWatermark);
            Assert.Equal(5, replicas[replicaNodeId].AppliedWatermark);
        }
    }

    private static async Task<(
        CommitReplicationWatermarkQueryHandler Handler,
        ServiceProvider Provider
    )> CreateHandlerAsync(
        Guid repositoryId,
        Guid primaryNodeId,
        Guid replicaNodeId,
        long epoch,
        long primaryWatermark,
        long? primaryAppliedWatermark = null,
        IInterceptor? interceptor = null
    )
    {
        var connection = SqliteTestConnection.OpenInMemory();

        var services = new ServiceCollection();
        services.AddSingleton(connection);
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(RepositoryMapsterConfig).Assembly])
        );
        services.AddDbContextFactory<OpenGitBaseDbContext>(options =>
        {
            options.UseSqlite(connection).EnableServiceProviderCaching(false);
            if (interceptor is not null)
            {
                options.AddInterceptors(interceptor);
            }
        });
        var mapsterConfig = new TypeAdapterConfig();
        new RepositoryMapsterConfig().Register(mapsterConfig);
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(sp.GetRequiredService<TypeAdapterConfig>()));
        services.AddSingleton<CommitReplicationWatermarkQueryHandler>();

        var provider = services.BuildServiceProvider();
        var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
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
                    ReplicationEpoch = epoch,
                    PrimaryWatermark = primaryWatermark,
                    Replicas =
                    [
                        new RepositoryReplicaEntity
                        {
                            RepositoryId = repositoryId,
                            StorageNodeId = primaryNodeId,
                            Role = RepositoryReplicaRole.Primary,
                            AppliedWatermark = primaryAppliedWatermark ?? primaryWatermark,
                        },
                        new RepositoryReplicaEntity
                        {
                            RepositoryId = repositoryId,
                            StorageNodeId = replicaNodeId,
                            Role = RepositoryReplicaRole.Replica,
                        },
                    ],
                }
            );
            await context.SaveChangesAsync();
        }

        return (provider.GetRequiredService<CommitReplicationWatermarkQueryHandler>(), provider);
    }

    /// <summary>
    /// Simulates a second writer committing between the handler's read and its write: just before
    /// the first UPDATE of the Repository row runs, it moves PrimaryWatermark itself.
    /// </summary>
    private sealed class ConcurrentCommitInterceptor : DbCommandInterceptor
    {
        private readonly long _bumpPrimaryWatermarkTo;

        public ConcurrentCommitInterceptor(long bumpPrimaryWatermarkTo)
        {
            _bumpPrimaryWatermarkTo = bumpPrimaryWatermarkTo;
        }

        public bool Fired { get; private set; }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result
        )
        {
            Bump(command);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default
        )
        {
            Bump(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result
        )
        {
            Bump(command);
            return base.NonQueryExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            Bump(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void Bump(DbCommand command)
        {
            if (Fired || !command.CommandText.Contains("UPDATE \"Repository\"", StringComparison.Ordinal))
            {
                return;
            }

            Fired = true;
            using var bump = command.Connection!.CreateCommand();
            bump.Transaction = command.Transaction;
            bump.CommandText =
                $"UPDATE \"Repository\" SET \"PrimaryWatermark\" = {_bumpPrimaryWatermarkTo}";
            bump.ExecuteNonQuery();
        }
    }
}
