using System.Data.Common;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Api.Tests.Services;

/// <summary>
/// A push whose replication failed leaves the primary ahead of the quorum: its heartbeat raises
/// AppliedWatermark from the hook's watermark file, PrimaryWatermark stays (#413). The reconciler
/// replicates such a write once the push has had its grace period, through the same
/// quorum-replicate path the push uses, with the real handlers wired in.
/// </summary>
public class AntiEntropyReconcilerPendingWritesTests
{
    [Fact]
    public async Task RunOnceAsync_WhenThePrimaryIsAheadPastTheGrace_ReplicatesTheWriteAndCommitsIt()
    {
        await using var fixture = await Fixture.CreateAsync(
            primaryWatermark: 5,
            primaryAppliedWatermark: 6,
            primaryLastSyncedAt: DateTimeOffset.UtcNow.AddMinutes(-20)
        );

        await fixture.NewReconciler().RunOnceAsync(CancellationToken.None);

        var state = await fixture.ReadStateAsync();
        Assert.Equal(6, state.PrimaryWatermark);
        Assert.Equal(6, state.ArtifactWatermarks[fixture.EncAId]);
        Assert.Equal(6, state.ArtifactWatermarks[fixture.EncBId]);
        Assert.Equal(1, fixture.Creates);
        Assert.Equal(2, fixture.Uploads);
        await fixture.Provisioner
            .Received(1)
            .CreateReplicationArtifactAsync(
                Arg.Is<StorageNodeDto>(node => node.Id.Value == fixture.PrimaryId),
                "token",
                fixture.PhysicalPath,
                fixture.RepositoryId,
                6,
                1,
                Arg.Any<string>(),
                1,
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task RunOnceAsync_WhenThePrimaryHasNeverSynced_ReplicatesTheWrite()
    {
        await using var fixture = await Fixture.CreateAsync(
            primaryWatermark: 5,
            primaryAppliedWatermark: 6,
            primaryLastSyncedAt: null
        );

        await fixture.NewReconciler().RunOnceAsync(CancellationToken.None);

        Assert.Equal(6, (await fixture.ReadStateAsync()).PrimaryWatermark);
    }

    [Fact]
    public async Task RunOnceAsync_WithinTheGrace_LeavesTheWriteToThePush()
    {
        await using var fixture = await Fixture.CreateAsync(
            primaryWatermark: 5,
            primaryAppliedWatermark: 6,
            primaryLastSyncedAt: DateTimeOffset.UtcNow.AddMinutes(-2)
        );

        await fixture.NewReconciler().RunOnceAsync(CancellationToken.None);

        var state = await fixture.ReadStateAsync();
        Assert.Equal(5, state.PrimaryWatermark);
        Assert.Equal(5, state.ArtifactWatermarks[fixture.EncAId]);
        Assert.Equal(0, fixture.Creates);
    }

    [Fact]
    public async Task RunOnceAsync_OnBothApiInstancesAtOnce_ReplicatesTheWriteOnce()
    {
        await using var fixture = await Fixture.CreateAsync(
            primaryWatermark: 5,
            primaryAppliedWatermark: 6,
            primaryLastSyncedAt: DateTimeOffset.UtcNow.AddMinutes(-20)
        );
        var secondInstance = fixture.NewReconciler();
        var nested = false;
        // api-2's reconciler runs while api-1's is building the artifact.
        fixture.OnCreate = async () =>
        {
            if (nested)
            {
                return;
            }

            nested = true;
            await secondInstance.RunOnceAsync(CancellationToken.None);
        };

        await fixture.NewReconciler().RunOnceAsync(CancellationToken.None);

        Assert.True(nested);
        Assert.Equal(1, fixture.Creates);
        Assert.Equal(2, fixture.Uploads);
        Assert.Equal(1, fixture.Commits);
        Assert.Equal(6, (await fixture.ReadStateAsync()).PrimaryWatermark);
    }

    [Fact]
    public async Task RunOnceAsync_WhenBothInstancesReadTheWriteBeforeEitherClaims_ReplicatesItOnce()
    {
        await using var fixture = await Fixture.CreateAsync(
            primaryWatermark: 5,
            primaryAppliedWatermark: 6,
            primaryLastSyncedAt: DateTimeOffset.UtcNow.AddMinutes(-20)
        );
        var secondInstance = fixture.NewReconciler();
        var secondIsBuilding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var letSecondFinish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? secondRun = null;
        // api-1 and api-2 both read the pending write with the same LastSyncedAt. api-2 claims
        // first and is building the artifact when api-1's claim reaches the database: only the
        // compare-and-set on LastSyncedAt keeps api-1 from claiming it as well and building and
        // uploading the write a second time.
        fixture.OnCreate = async () =>
        {
            if (!secondIsBuilding.Task.IsCompleted)
            {
                secondIsBuilding.SetResult();
                await letSecondFinish.Task;
            }
        };
        fixture.Claims.BeforeNextClaim = async () =>
        {
            secondRun = secondInstance.RunOnceAsync(CancellationToken.None);
            await secondIsBuilding.Task;
        };

        await fixture.NewReconciler().RunOnceAsync(CancellationToken.None);
        letSecondFinish.SetResult();
        await secondRun!;

        Assert.Equal(1, fixture.Creates);
        Assert.Equal(2, fixture.Uploads);
        Assert.Equal(1, fixture.Commits);
        Assert.Equal(6, (await fixture.ReadStateAsync()).PrimaryWatermark);
    }

    [Fact]
    public async Task RunOnceAsync_NeverLowersAWatermark()
    {
        await using var fixture = await Fixture.CreateAsync(
            primaryWatermark: 5,
            primaryAppliedWatermark: 8,
            primaryLastSyncedAt: DateTimeOffset.UtcNow.AddMinutes(-20)
        );

        await fixture.NewReconciler().RunOnceAsync(CancellationToken.None);

        var state = await fixture.ReadStateAsync();
        // One committed step per pass, and the primary keeps what its heartbeat reported.
        Assert.Equal(6, state.PrimaryWatermark);
        Assert.Equal(8, state.PrimaryAppliedWatermark);
        Assert.Equal(6, state.ArtifactWatermarks[fixture.EncAId]);
        Assert.Equal(6, state.ArtifactWatermarks[fixture.EncBId]);
    }

    [Fact]
    public async Task RunOnceAsync_WhenNoEncryptedReplicaTakesTheArtifact_CommitsNothing()
    {
        await using var fixture = await Fixture.CreateAsync(
            primaryWatermark: 5,
            primaryAppliedWatermark: 6,
            primaryLastSyncedAt: DateTimeOffset.UtcNow.AddMinutes(-20)
        );
        fixture.UploadResult = StorageProvisionerResult.Fail(413, "Payload Too Large");

        await fixture.NewReconciler().RunOnceAsync(CancellationToken.None);

        var state = await fixture.ReadStateAsync();
        Assert.Equal(5, state.PrimaryWatermark);
        Assert.Equal(6, state.PrimaryAppliedWatermark);
        Assert.Equal(5, state.ArtifactWatermarks[fixture.EncAId]);
        Assert.Equal(0, fixture.Commits);
    }

    [Fact]
    public async Task RunOnceAsync_WhenNoEncryptedReplicaCanBeProbed_StillReplicatesThePendingWrite()
    {
        await using var fixture = await Fixture.CreateAsync(
            primaryWatermark: 5,
            primaryAppliedWatermark: 6,
            primaryLastSyncedAt: DateTimeOffset.UtcNow.AddMinutes(-20)
        );
        fixture.Provisioner
            .TryGetArtifactWatermarkStatusAsync(
                Arg.Any<StorageNodeDto>(),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>()
            )
            .Returns<Task<ArtifactWatermarkStatusResult>>(_ => throw new HttpRequestException("connection refused"));

        await fixture.NewReconciler().RunOnceAsync(CancellationToken.None);

        Assert.Equal(6, (await fixture.ReadStateAsync()).PrimaryWatermark);
    }

    [Fact]
    public async Task RunOnceAsync_WhenTheOtherPassesCannotListTheNodes_StillReplicatesThePendingWrite()
    {
        await using var fixture = await Fixture.CreateAsync(
            primaryWatermark: 5,
            primaryAppliedWatermark: 6,
            primaryLastSyncedAt: DateTimeOffset.UtcNow.AddMinutes(-20)
        );
        fixture.QueryProcessor
            .RunQueryAsync(Arg.Any<ListHealthyStorageNodesQuery>(), Arg.Any<CancellationToken>())
            .Returns<Task<Option<IReadOnlyList<StorageNodeDto>>>>(_ =>
                throw new InvalidOperationException("database is gone")
            );

        await fixture.NewReconciler().RunOnceAsync(CancellationToken.None);

        Assert.Equal(6, (await fixture.ReadStateAsync()).PrimaryWatermark);
    }

    [Fact]
    public async Task RunOnceAsync_WhenOneEncryptedReplicaCannotBeProbed_StillChecksTheOther()
    {
        await using var fixture = await Fixture.CreateAsync(
            primaryWatermark: 5,
            primaryAppliedWatermark: 5,
            primaryLastSyncedAt: DateTimeOffset.UtcNow.AddMinutes(-20)
        );
        fixture.Provisioner
            .TryGetArtifactWatermarkStatusAsync(
                Arg.Is<StorageNodeDto>(node => node.Id.Value == fixture.EncAId),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>()
            )
            .Returns<Task<ArtifactWatermarkStatusResult>>(_ => throw new HttpRequestException("connection refused"));
        fixture.Provisioner
            .TryGetArtifactWatermarkStatusAsync(
                Arg.Is<StorageNodeDto>(node => node.Id.Value == fixture.EncBId),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(ArtifactWatermarkStatusResult.Ok(null));
        fixture.Provisioner
            .TryGetReplicationArtifactAsync(
                Arg.Is<StorageNodeDto>(node => node.Id.Value == fixture.EncAId),
                Arg.Any<string>(),
                fixture.RepositoryId,
                5,
                Arg.Any<CancellationToken>()
            )
            .Returns(ReplicationArtifactFetchResult.Ok("{\"epoch\":1,\"watermark\":5,\"bundleSha256\":\"ABC\",\"keyVersion\":1}", [1, 2, 3]));

        await fixture.NewReconciler().RunOnceAsync(CancellationToken.None);

        // encB lost its artifacts: found despite encA not answering, and repaired from encA's copy.
        await fixture.Provisioner
            .Received(1)
            .UploadReplicationArtifactAsync(
                Arg.Is<StorageNodeDto>(node => node.Id.Value == fixture.EncBId),
                Arg.Any<string>(),
                fixture.RepositoryId,
                5,
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<CancellationToken>()
            );
        Assert.Equal(5, (await fixture.ReadStateAsync()).ArtifactWatermarks[fixture.EncBId]);
    }

    [Fact]
    public async Task RunOnceAsync_WhenTheOldestPendingWriteThrows_StillReplicatesTheNextOne()
    {
        await using var fixture = await Fixture.CreateAsync(
            primaryWatermark: 5,
            primaryAppliedWatermark: 6,
            primaryLastSyncedAt: DateTimeOffset.UtcNow.AddMinutes(-20)
        );
        var broken = Guid.NewGuid();
        await fixture.AddPendingRepositoryAsync(broken, DateTimeOffset.UtcNow.AddHours(-3));
        fixture.RepositoryKeys
            .TryGetRepositoryKeyAsync(broken, Arg.Any<CancellationToken>())
            .Returns<Task<EphemeralRepositoryKey?>>(_ => throw new InvalidOperationException("key store timed out"));

        await fixture.NewReconciler().RunOnceAsync(CancellationToken.None);

        Assert.Equal(6, (await fixture.ReadStateAsync()).PrimaryWatermark);
    }

    private sealed record ReplicationRow(
        long PrimaryWatermark,
        long PrimaryAppliedWatermark,
        IReadOnlyDictionary<Guid, long?> ArtifactWatermarks
    );

    private sealed class Fixture : IAsyncDisposable
    {
        private const string Manifest = "{\"epoch\":1,\"watermark\":6,\"bundleSha256\":\"ABC\",\"keyVersion\":1}";

        private readonly ServiceProvider _provider;
        private int _creates;
        private int _uploads;
        private int _commits;

        private Fixture(ServiceProvider provider, ClaimInterceptor claims)
        {
            _provider = provider;
            Claims = claims;
        }

        public Guid RepositoryId { get; } = Guid.NewGuid();

        public Guid PrimaryId { get; } = Guid.NewGuid();

        public Guid EncAId { get; } = Guid.NewGuid();

        public Guid EncBId { get; } = Guid.NewGuid();

        public string PhysicalPath => $"/srv/git/{RepositoryId}.git";

        public IQueryProcessor QueryProcessor { get; } = Substitute.For<IQueryProcessor>();

        public IStorageProvisionerClient Provisioner { get; } = Substitute.For<IStorageProvisionerClient>();

        public IRepositoryKeyService RepositoryKeys { get; } = Substitute.For<IRepositoryKeyService>();

        public Func<Task>? OnCreate { get; set; }

        public ClaimInterceptor Claims { get; }

        public StorageProvisionerResult UploadResult { get; set; } = StorageProvisionerResult.Ok(201);

        public int Creates => _creates;

        public int Uploads => _uploads;

        public int Commits => _commits;

        public static async Task<Fixture> CreateAsync(
            long primaryWatermark,
            long primaryAppliedWatermark,
            DateTimeOffset? primaryLastSyncedAt
        )
        {
            var connection = SqliteTestConnection.OpenInMemory();
            var services = new ServiceCollection();
            var holder = new Fixture[1];
            services.AddSingleton(connection);
            services.AddSingleton(_ => holder[0].QueryProcessor);
            services.AddSingleton(_ => holder[0].Provisioner);
            services.AddSingleton(_ => holder[0].RepositoryKeys);
            services.AddSingleton<IFeatureAssemblyProvider>(
                new FeatureAssemblyProvider(
                    [
                        typeof(RepositoryMapsterConfig).Assembly,
                        typeof(global::OpenGitBase.Features.StorageNode.StorageNodeMapsterConfig).Assembly,
                    ]
                )
            );
            var claims = new ClaimInterceptor();
            services.AddDbContextFactory<OpenGitBaseDbContext>(options =>
                options.UseSqlite(connection).EnableServiceProviderCaching(false).AddInterceptors(claims)
            );
            var mapsterConfig = new TypeAdapterConfig();
            new RepositoryMapsterConfig().Register(mapsterConfig);
            services.AddSingleton(mapsterConfig);
            services.AddSingleton<IMapper>(sp => new Mapper(sp.GetRequiredService<TypeAdapterConfig>()));
            services.AddLogging();
            services.AddSingleton(Options.Create(new HaStorageBackgroundOptions()));
            services.AddSingleton<ReplicationArtifactPublisher>();
            services.AddSingleton<ReplicaCatchUpService>();
            services.AddSingleton<Rf1BackfillService>();
            services.AddSingleton<ILfsRepairQueue>(new OpenGitBase.Api.Tests.Services.Lfs.RecordingLfsRepairQueue());
            services.AddSingleton(Substitute.For<ILfsOrphanCleaner>());
            services.AddTransient<AntiEntropyReconcilerService>();
            services.AddSingleton<GetRepositoryReplicationContextQueryHandler>();
            services.AddSingleton<QuorumReplicateRepositoryQueryHandler>();
            services.AddSingleton<CommitReplicationWatermarkQueryHandler>();

            var fixture = new Fixture(services.BuildServiceProvider(), claims);
            holder[0] = fixture;
            fixture.RepositoryKeys
                .TryGetRepositoryKeyAsync(fixture.RepositoryId, Arg.Any<CancellationToken>())
                .Returns(new EphemeralRepositoryKey(new byte[32], 1));
            await fixture.SeedAsync(primaryWatermark, primaryAppliedWatermark, primaryLastSyncedAt);
            fixture.Route();
            return fixture;
        }

        public AntiEntropyReconcilerService NewReconciler() =>
            _provider.GetRequiredService<AntiEntropyReconcilerService>();

        public async Task<ReplicationRow> ReadStateAsync()
        {
            var factory = _provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            var repository = await context
                .Set<RepositoryEntity>()
                .AsNoTracking()
                .Include(entity => entity.Replicas)
                .SingleAsync(entity => entity.Id == RepositoryId);
            return new ReplicationRow(
                repository.PrimaryWatermark,
                repository.Replicas.Single(replica => replica.StorageNodeId == PrimaryId).AppliedWatermark,
                repository.Replicas
                    .Where(replica => replica.Role == RepositoryReplicaRole.EncryptedReplica)
                    .ToDictionary(replica => replica.StorageNodeId, replica => replica.ArtifactWatermark)
            );
        }

        /// <summary>Another repository on the same three nodes with the primary ahead.</summary>
        public async Task AddPendingRepositoryAsync(Guid repositoryId, DateTimeOffset primaryLastSyncedAt)
        {
            var factory = _provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            context.Set<RepositoryEntity>().Add(NewRepository(repositoryId, 5, 6, primaryLastSyncedAt));
            await context.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync() => await _provider.DisposeAsync();

        private static StorageNodeDto Node(Guid nodeId) =>
            new()
            {
                Id = StorageNodeId.From(nodeId),
                NodeId = nodeId.ToString(),
                InternalHost = nodeId.ToString(),
                InternalHttpPort = 8081,
                IsHealthy = true,
                FreeBytesAvailable = 1_000_000,
            };

        private void Route()
        {
            var nodeIds = new[] { PrimaryId, EncAId, EncBId };
            QueryProcessor
                .RunQueryAsync(Arg.Any<ListHealthyStorageNodesQuery>(), Arg.Any<CancellationToken>())
                .Returns(Option.From<IReadOnlyList<StorageNodeDto>>(nodeIds.Select(Node).ToList()));
            foreach (var nodeId in nodeIds)
            {
                QueryProcessor
                    .RunQueryAsync(
                        Arg.Is<GetStorageNodeQuery>(query => query.ModelId.Value == nodeId),
                        Arg.Any<CancellationToken>()
                    )
                    .Returns(Option.From(Node(nodeId)));
                QueryProcessor
                    .RunQueryAsync(
                        Arg.Is<GetStorageNodeApiTokenQuery>(query => query.StorageNodeId.Value == nodeId),
                        Arg.Any<CancellationToken>()
                    )
                    .Returns(Option.From("token"));
            }

            QueryProcessor
                .RunQueryAsync(Arg.Any<GetRepositoryReplicationContextQuery>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                    _provider
                        .GetRequiredService<GetRepositoryReplicationContextQueryHandler>()
                        .RunQueryAsync(callInfo.Arg<GetRepositoryReplicationContextQuery>(), CancellationToken.None)
                );
            QueryProcessor
                .RunQueryAsync(Arg.Any<QuorumReplicateRepositoryQuery>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                    _provider
                        .GetRequiredService<QuorumReplicateRepositoryQueryHandler>()
                        .RunQueryAsync(callInfo.Arg<QuorumReplicateRepositoryQuery>(), CancellationToken.None)
                );
            QueryProcessor
                .RunQueryAsync(Arg.Any<CommitReplicationWatermarkQuery>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    Interlocked.Increment(ref _commits);
                    return _provider
                        .GetRequiredService<CommitReplicationWatermarkQueryHandler>()
                        .RunQueryAsync(callInfo.Arg<CommitReplicationWatermarkQuery>(), CancellationToken.None);
                });

            Provisioner
                .CreateReplicationArtifactAsync(
                    Arg.Any<StorageNodeDto>(),
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<Guid>(),
                    Arg.Any<long>(),
                    Arg.Any<long>(),
                    Arg.Any<string>(),
                    Arg.Any<int>(),
                    Arg.Any<CancellationToken>()
                )
                .Returns(async _ =>
                {
                    Interlocked.Increment(ref _creates);
                    if (OnCreate is not null)
                    {
                        await OnCreate();
                    }

                    return ReplicationArtifactFetchResult.Ok(Manifest, [1, 2, 3]);
                });
            Provisioner
                .UploadReplicationArtifactAsync(
                    Arg.Any<StorageNodeDto>(),
                    Arg.Any<string>(),
                    Arg.Any<Guid>(),
                    Arg.Any<long>(),
                    Arg.Any<string>(),
                    Arg.Any<byte[]>(),
                    Arg.Any<CancellationToken>()
                )
                .Returns(_ =>
                {
                    Interlocked.Increment(ref _uploads);
                    return UploadResult;
                });
        }

        private async Task SeedAsync(
            long primaryWatermark,
            long primaryAppliedWatermark,
            DateTimeOffset? primaryLastSyncedAt
        )
        {
            var factory = _provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            await context.Database.EnsureCreatedAsync();
            foreach (var nodeId in new[] { PrimaryId, EncAId, EncBId })
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
                NewRepository(RepositoryId, primaryWatermark, primaryAppliedWatermark, primaryLastSyncedAt)
            );
            await context.SaveChangesAsync();
        }

        private RepositoryEntity NewRepository(
            Guid repositoryId,
            long primaryWatermark,
            long primaryAppliedWatermark,
            DateTimeOffset? primaryLastSyncedAt
        ) =>
            new RepositoryEntity
            {
                Id = repositoryId,
                Name = $"repo-{repositoryId:N}",
                Slug = $"repo-{repositoryId:N}",
                OwnerUserId = Guid.NewGuid(),
                PhysicalPath = $"/srv/git/{repositoryId}.git",
                StorageNodeId = PrimaryId,
                PrimaryStorageNodeId = PrimaryId,
                ReadReplicaStorageNodeId = PrimaryId,
                PrimaryWatermark = primaryWatermark,
                ReplicationEpoch = 1,
                ReplicationState = ReplicationState.Rf4Healthy,
                Replicas =
                [
                    new RepositoryReplicaEntity
                    {
                        RepositoryId = repositoryId,
                        StorageNodeId = PrimaryId,
                        Role = RepositoryReplicaRole.Primary,
                        AppliedWatermark = primaryAppliedWatermark,
                        LastSyncedAt = primaryLastSyncedAt,
                    },
                    new RepositoryReplicaEntity
                    {
                        RepositoryId = repositoryId,
                        StorageNodeId = EncAId,
                        Role = RepositoryReplicaRole.EncryptedReplica,
                        AppliedWatermark = primaryWatermark,
                        ArtifactWatermark = primaryWatermark,
                        LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-1),
                    },
                    new RepositoryReplicaEntity
                    {
                        RepositoryId = repositoryId,
                        StorageNodeId = EncBId,
                        Role = RepositoryReplicaRole.EncryptedReplica,
                        AppliedWatermark = primaryWatermark,
                        ArtifactWatermark = primaryWatermark,
                        LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-1),
                    },
                ],
            };
    }

    /// <summary>Runs <see cref="BeforeNextClaim"/> once, right before a reconciler's claim reaches the database.</summary>
    private sealed class ClaimInterceptor : DbCommandInterceptor
    {
        public Func<Task>? BeforeNextClaim { get; set; }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            var isClaim =
                command.CommandText.Contains("UPDATE \"RepositoryReplica\"", StringComparison.Ordinal)
                && command.CommandText.Contains("\"LastSyncedAt\" = @", StringComparison.Ordinal)
                && !command.CommandText.Contains("Watermark", StringComparison.Ordinal);
            var before = isClaim ? BeforeNextClaim : null;
            if (before is not null)
            {
                BeforeNextClaim = null;
                await before();
            }

            return result;
        }
    }
}
