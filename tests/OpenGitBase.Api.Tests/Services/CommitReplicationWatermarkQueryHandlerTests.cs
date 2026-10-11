using System.Data.Common;
using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
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

    [Fact]
    public async Task RunQueryAsync_WhenCancelledAfterTheWatermarkMoved_LeavesNothingHalfCommitted()
    {
        // The request can be aborted (HAProxy timeout, API shutdown) between moving
        // PrimaryWatermark and recording which replicas hold the artifact. Both must move
        // together or not at all: a PrimaryWatermark of 5 next to replicas still at 4 makes
        // cold recovery restore the older artifact.
        var repositoryId = Guid.NewGuid();
        var primaryNodeId = Guid.NewGuid();
        var replicaNodeId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        var cancelAfterCas = new CancelAfterWatermarkUpdateInterceptor(cancellation);
        var (handler, provider) = await CreateHandlerAsync(
            repositoryId,
            primaryNodeId,
            replicaNodeId,
            epoch: 1,
            primaryWatermark: 4,
            interceptor: cancelAfterCas
        );
        await using (provider)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                handler.RunQueryAsync(
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
                    cancellation.Token
                )
            );

            Assert.True(cancelAfterCas.Fired);
            var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var repository = await context
                .Set<RepositoryEntity>()
                .AsNoTracking()
                .Include(entity => entity.Replicas)
                .SingleAsync(entity => entity.Id == repositoryId);
            Assert.Equal(4, repository.PrimaryWatermark);
            Assert.Equal(0, repository.Replicas.Single(replica => replica.StorageNodeId == replicaNodeId).AppliedWatermark);
            Assert.Equal(4, repository.Replicas.Single(replica => replica.StorageNodeId == primaryNodeId).AppliedWatermark);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WorksUnderARetryingExecutionStrategy()
    {
        // Production runs Npgsql with EnableRetryOnFailure, which rejects a transaction the
        // handler opens itself unless it runs inside the execution strategy.
        var repositoryId = Guid.NewGuid();
        var primaryNodeId = Guid.NewGuid();
        var replicaNodeId = Guid.NewGuid();
        var (handler, provider) = await CreateHandlerAsync(
            repositoryId,
            primaryNodeId,
            replicaNodeId,
            epoch: 1,
            primaryWatermark: 4,
            retryingExecutionStrategy: true
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

            Assert.True(result.Get().Success, result.Get().Error);
            var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var repository = await context
                .Set<RepositoryEntity>()
                .AsNoTracking()
                .Include(entity => entity.Replicas)
                .SingleAsync(entity => entity.Id == repositoryId);
            Assert.Equal(5, repository.PrimaryWatermark);
            Assert.Equal(5, repository.Replicas.Single(replica => replica.StorageNodeId == replicaNodeId).AppliedWatermark);
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
        IInterceptor? interceptor = null,
        bool retryingExecutionStrategy = false
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
            options
                .UseSqlite(
                    connection,
                    sqlite =>
                    {
                        if (retryingExecutionStrategy)
                        {
                            sqlite.ExecutionStrategy(dependencies => new RetryingTestExecutionStrategy(dependencies));
                        }
                    }
                )
                .EnableServiceProviderCaching(false);
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
    /// Simulates a second writer committing between the handler's read and its write: as soon as
    /// the handler has read the Repository row, it moves PrimaryWatermark itself.
    /// </summary>
    private sealed class ConcurrentCommitInterceptor : DbCommandInterceptor
    {
        private readonly long _bumpPrimaryWatermarkTo;

        public ConcurrentCommitInterceptor(long bumpPrimaryWatermarkTo)
        {
            _bumpPrimaryWatermarkTo = bumpPrimaryWatermarkTo;
        }

        public bool Fired { get; private set; }

        public override InterceptionResult DataReaderDisposing(
            DbCommand command,
            DataReaderDisposingEventData eventData,
            InterceptionResult result
        )
        {
            if (
                !Fired
                && command.Transaction is null
                && command.CommandText.Contains("FROM \"Repository\"", StringComparison.Ordinal)
            )
            {
                Fired = true;
                using var bump = command.Connection!.CreateCommand();
                bump.CommandText =
                    $"UPDATE \"Repository\" SET \"PrimaryWatermark\" = {_bumpPrimaryWatermarkTo}";
                bump.ExecuteNonQuery();
            }

            return base.DataReaderDisposing(command, eventData, result);
        }
    }

    /// <summary>Cancels the request right after the PrimaryWatermark compare-and-set has run.</summary>
    private sealed class CancelAfterWatermarkUpdateInterceptor : DbCommandInterceptor
    {
        private readonly CancellationTokenSource _cancellation;

        public CancelAfterWatermarkUpdateInterceptor(CancellationTokenSource cancellation)
        {
            _cancellation = cancellation;
        }

        public bool Fired { get; private set; }

        public override ValueTask<int> NonQueryExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            int result,
            CancellationToken cancellationToken = default
        )
        {
            Cancel(command);
            return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default
        )
        {
            Cancel(command);
            return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
        }

        private void Cancel(DbCommand command)
        {
            if (Fired || !command.CommandText.Contains("UPDATE \"Repository\"", StringComparison.Ordinal))
            {
                return;
            }

            Fired = true;
            _cancellation.Cancel();
        }
    }

    /// <summary>Behaves like Npgsql's EnableRetryOnFailure towards transactions the code opens itself.</summary>
    private sealed class RetryingTestExecutionStrategy : ExecutionStrategy
    {
        public RetryingTestExecutionStrategy(ExecutionStrategyDependencies dependencies)
            : base(dependencies, maxRetryCount: 3, maxRetryDelay: TimeSpan.FromMilliseconds(10))
        {
        }

        protected override bool ShouldRetryOn(Exception exception) => false;
    }
}
