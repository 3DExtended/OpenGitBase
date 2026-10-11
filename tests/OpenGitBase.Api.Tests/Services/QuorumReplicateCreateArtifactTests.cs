using Microsoft.EntityFrameworkCore;
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

namespace OpenGitBase.Api.Tests.Services;

/// <summary>
/// quorum-replicate with createArtifact: the API builds the encrypted artifact on the primary
/// itself and uploads it, instead of receiving it from the post-receive hook (#413: the hook's
/// relay ran into Kestrel's request body limit).
/// </summary>
public class QuorumReplicateCreateArtifactTests
{
    private const string Manifest = "{\"epoch\":1,\"watermark\":6,\"bundleSha256\":\"ABC\",\"keyVersion\":1}";

    [Fact]
    public async Task CreateArtifact_CreatesOnPrimaryUploadsToEveryEncryptedPeerAndCommitsWithoutConfirmDownload()
    {
        await using var fixture = await Fixture.CreateAsync(primaryWatermark: 5);
        fixture.CommitSucceeds();

        var result = await fixture.RunAsync(appliedWatermark: 6, createArtifact: true);

        Assert.True(result.Success, result.Error);
        Assert.Equal(6, result.PrimaryWatermark);
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
        foreach (var encryptedId in new[] { fixture.EncAId, fixture.EncBId })
        {
            await fixture.Provisioner
                .Received(1)
                .UploadReplicationArtifactAsync(
                    Arg.Is<StorageNodeDto>(node => node.Id.Value == encryptedId),
                    "token",
                    fixture.RepositoryId,
                    6,
                    Manifest,
                    Arg.Any<byte[]>(),
                    Arg.Any<CancellationToken>()
                );
        }

        await fixture.Provisioner
            .DidNotReceive()
            .TryGetReplicationArtifactAsync(
                Arg.Any<StorageNodeDto>(),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<long>(),
                Arg.Any<CancellationToken>()
            );
        var commit = Assert.Single(fixture.Commits);
        Assert.Equal(6, commit.NewWatermark);
        Assert.Equal(1, commit.ReplicationEpoch);
        Assert.Equal(
            new[] { fixture.PrimaryId, fixture.EncAId, fixture.EncBId }.OrderBy(id => id),
            commit.QuorumNodeIds.Select(id => id.Value).OrderBy(id => id)
        );
    }

    [Fact]
    public async Task CreateArtifact_CommitsWithThePeersThatAcceptedAndRetriesTheOthersInTheBackground()
    {
        await using var fixture = await Fixture.CreateAsync(primaryWatermark: 5);
        fixture.CommitSucceeds();
        fixture.Provisioner
            .UploadReplicationArtifactAsync(
                Arg.Is<StorageNodeDto>(node => node.Id.Value == fixture.EncBId),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                StorageProvisionerResult.Fail(503, "busy"),
                StorageProvisionerResult.Ok(201)
            );

        var result = await fixture.RunAsync(appliedWatermark: 6, createArtifact: true);

        Assert.True(result.Success, result.Error);
        var commit = Assert.Single(fixture.Commits);
        Assert.Equal(
            new[] { fixture.PrimaryId, fixture.EncAId }.OrderBy(id => id),
            commit.QuorumNodeIds.Select(id => id.Value).OrderBy(id => id)
        );

        // The fan-out re-sends the artifact it already holds; it does not download it again.
        var encBWatermark = await fixture.WaitForArtifactWatermarkAsync(fixture.EncBId, 6);
        Assert.Equal(6, encBWatermark);
        await fixture.Provisioner
            .DidNotReceive()
            .TryGetReplicationArtifactAsync(
                Arg.Any<StorageNodeDto>(),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<long>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task CreateArtifact_RetriesARefusedPeerAfterTheRequestScopeIsGone()
    {
        // In production the handler, its query processor and the publisher belong to the request's
        // scope, which is disposed once the response is written. The background retry of a refused
        // replica runs after that and must not resolve anything from it.
        await using var fixture = await Fixture.CreateAsync(primaryWatermark: 5, requestScoped: true);
        fixture.CommitSucceeds();
        fixture.Provisioner
            .UploadReplicationArtifactAsync(
                Arg.Is<StorageNodeDto>(node => node.Id.Value == fixture.EncBId),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                StorageProvisionerResult.Fail(503, "busy"),
                StorageProvisionerResult.Ok(201)
            );
        var requestDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.HoldLaterLookupsOf(fixture.EncBId, requestDone.Task);

        var result = await fixture.RunAsync(appliedWatermark: 6, createArtifact: true);
        requestDone.SetResult();

        Assert.True(result.Success, result.Error);
        Assert.Equal(6, await fixture.WaitForArtifactWatermarkAsync(fixture.EncBId, 6));
    }

    [Fact]
    public async Task CreateArtifact_AReplicaThatDoesNotAnswer_NeitherHoldsUpTheOtherNorFailsThePush()
    {
        // storage-3 accepts the connection but never answers while its agent keeps it "healthy".
        // The push must not wait for it past HAProxy's 5 minutes (the hook would then report a
        // write that storage-2 already holds as failed), and must not wait for it before
        // uploading to storage-2 either.
        await using var fixture = await Fixture.CreateAsync(primaryWatermark: 5, uploadTimeoutSeconds: 1);
        fixture.CommitSucceeds();
        var encAInFlight = 0;
        var encBStartedWhileEncAWasInFlight = false;
        fixture.Provisioner
            .UploadReplicationArtifactAsync(
                Arg.Is<StorageNodeDto>(node => node.Id.Value == fixture.EncAId),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(async callInfo =>
            {
                Interlocked.Increment(ref encAInFlight);
                try
                {
                    await Task.Delay(Timeout.Infinite, callInfo.Arg<CancellationToken>());
                    return StorageProvisionerResult.Ok(201);
                }
                finally
                {
                    Interlocked.Decrement(ref encAInFlight);
                }
            });
        fixture.Provisioner
            .UploadReplicationArtifactAsync(
                Arg.Is<StorageNodeDto>(node => node.Id.Value == fixture.EncBId),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(async _ =>
            {
                // Give a parallel upload to storage-2 the moment to start first.
                await Task.Delay(100);
                encBStartedWhileEncAWasInFlight |= Volatile.Read(ref encAInFlight) > 0;
                return StorageProvisionerResult.Ok(201);
            });

        var result = await fixture
            .RunAsync(appliedWatermark: 6, createArtifact: true)
            .WaitAsync(TimeSpan.FromSeconds(15));

        Assert.True(result.Success, result.Error);
        var commit = Assert.Single(fixture.Commits);
        Assert.Equal(
            new[] { fixture.PrimaryId, fixture.EncBId }.OrderBy(id => id),
            commit.QuorumNodeIds.Select(id => id.Value).OrderBy(id => id)
        );
        Assert.True(encBStartedWhileEncAWasInFlight);
    }

    [Fact]
    public async Task CreateArtifact_FanOutDoesNotMarkAPeerThatStillRejectsTheArtifact()
    {
        await using var fixture = await Fixture.CreateAsync(primaryWatermark: 5);
        fixture.CommitSucceeds();
        var encBUploads = 0;
        fixture.Provisioner
            .UploadReplicationArtifactAsync(
                Arg.Is<StorageNodeDto>(node => node.Id.Value == fixture.EncBId),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(_ =>
            {
                Interlocked.Increment(ref encBUploads);
                return StorageProvisionerResult.Fail(503, "busy");
            });

        var result = await fixture.RunAsync(appliedWatermark: 6, createArtifact: true);

        Assert.True(result.Success, result.Error);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (Volatile.Read(ref encBUploads) < 2 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.Equal(2, Volatile.Read(ref encBUploads));
        // Give a (wrong) bookkeeping write after the failed retry the chance to land.
        await Task.Delay(200);
        Assert.Equal(5, await fixture.WaitForArtifactWatermarkAsync(fixture.EncBId, expected: 5));
    }

    [Fact]
    public async Task CreateArtifact_FailsWithoutCommitWhenNoEncryptedPeerAcceptsTheUpload()
    {
        await using var fixture = await Fixture.CreateAsync(primaryWatermark: 5);
        fixture.CommitSucceeds();
        fixture.Provisioner
            .UploadReplicationArtifactAsync(
                Arg.Any<StorageNodeDto>(),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(StorageProvisionerResult.Fail(507, "disk full"));

        var result = await fixture.RunAsync(appliedWatermark: 6, createArtifact: true);

        Assert.False(result.Success);
        Assert.False(result.Stale);
        Assert.Contains("disk full", result.Error!, StringComparison.Ordinal);
        Assert.Empty(fixture.Commits);
    }

    [Fact]
    public async Task CreateArtifact_FailsWithoutUploadWhenThePrimaryCannotCreateTheArtifact()
    {
        await using var fixture = await Fixture.CreateAsync(primaryWatermark: 5);
        fixture.CommitSucceeds();
        fixture.Provisioner
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
            .Returns(ReplicationArtifactFetchResult.Fail(500, "detected dubious ownership"));

        var result = await fixture.RunAsync(appliedWatermark: 6, createArtifact: true);

        Assert.False(result.Success);
        Assert.Contains("dubious ownership", result.Error!, StringComparison.Ordinal);
        Assert.Empty(fixture.Commits);
        await fixture.Provisioner
            .DidNotReceive()
            .UploadReplicationArtifactAsync(
                Arg.Any<StorageNodeDto>(),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task CreateArtifact_FailsWithoutCommitWhenThePrimaryIsUnreachable()
    {
        await using var fixture = await Fixture.CreateAsync(primaryWatermark: 5);
        fixture.CommitSucceeds();
        fixture.Provisioner
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
            .Returns<ReplicationArtifactFetchResult>(_ => throw new HttpRequestException("connection refused"));

        var result = await fixture.RunAsync(appliedWatermark: 6, createArtifact: true);

        Assert.False(result.Success);
        Assert.Contains("connection refused", result.Error!, StringComparison.Ordinal);
        Assert.Empty(fixture.Commits);
    }

    [Fact]
    public async Task WhenTheWatermarkIsAlreadyCommitted_ReportsStaleSoTheHookRetries()
    {
        await using var fixture = await Fixture.CreateAsync(primaryWatermark: 6);
        fixture.CommitSucceeds();

        var result = await fixture.RunAsync(appliedWatermark: 6, createArtifact: true);

        Assert.False(result.Success);
        Assert.True(result.Stale);
        Assert.Empty(fixture.Commits);
    }

    [Fact]
    public async Task CreateArtifact_WhenAnotherCommitWinsTheRace_ReportsStale()
    {
        await using var fixture = await Fixture.CreateAsync(primaryWatermark: 5);
        fixture.CommitFailsBecauseSomeoneElseCommitted();

        var result = await fixture.RunAsync(appliedWatermark: 6, createArtifact: true);

        Assert.False(result.Success);
        Assert.True(result.Stale);
    }

    [Fact]
    public async Task CreateArtifact_FromThePushHook_MarksThePrimaryAsReplicatingBeforeCreating()
    {
        var longAgo = DateTimeOffset.UtcNow.AddDays(-2);
        await using var fixture = await Fixture.CreateAsync(primaryWatermark: 5, primaryLastSyncedAt: longAgo);
        fixture.CommitSucceeds();
        DateTimeOffset? lastSyncedAtDuringCreate = null;
        fixture.OnCreate = () => lastSyncedAtDuringCreate = fixture.ReadPrimaryLastSyncedAt();

        var result = await fixture.RunAsync(appliedWatermark: 6, createArtifact: true);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(lastSyncedAtDuringCreate);
        Assert.True(lastSyncedAtDuringCreate > DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task CreateArtifact_ForAReconcilerClaim_DoesNotUploadOnceAPushTookOver()
    {
        var claim = DateTimeOffset.UtcNow.AddSeconds(-5);
        await using var fixture = await Fixture.CreateAsync(primaryWatermark: 5, primaryLastSyncedAt: claim);
        fixture.CommitSucceeds();
        // A push's quorum-replicate marks the primary while the reconciler's artifact is built.
        fixture.OnCreate = () => fixture.SetPrimaryLastSyncedAt(DateTimeOffset.UtcNow);

        var result = await fixture.RunAsync(
            appliedWatermark: 6,
            createArtifact: true,
            expectedPrimaryLastSyncedAt: fixture.ReadPrimaryLastSyncedAt()
        );

        Assert.False(result.Success);
        Assert.Empty(fixture.Commits);
        await fixture.Provisioner
            .DidNotReceive()
            .UploadReplicationArtifactAsync(
                Arg.Any<StorageNodeDto>(),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task CreateArtifact_ForAReconcilerClaim_StopsUploadingWhenAPushTakesOverMidway()
    {
        var claim = DateTimeOffset.UtcNow.AddSeconds(-5);
        await using var fixture = await Fixture.CreateAsync(primaryWatermark: 5, primaryLastSyncedAt: claim);
        fixture.CommitSucceeds();
        var uploadedTo = new List<Guid>();
        fixture.Provisioner
            .UploadReplicationArtifactAsync(
                Arg.Any<StorageNodeDto>(),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(callInfo =>
            {
                uploadedTo.Add(callInfo.Arg<StorageNodeDto>().Id.Value);
                // A push marks the primary while the reconciler's first upload is in flight.
                fixture.SetPrimaryLastSyncedAt(DateTimeOffset.UtcNow);
                return StorageProvisionerResult.Ok(201);
            });

        var result = await fixture.RunAsync(
            appliedWatermark: 6,
            createArtifact: true,
            expectedPrimaryLastSyncedAt: fixture.ReadPrimaryLastSyncedAt()
        );

        Assert.False(result.Success);
        Assert.Empty(fixture.Commits);
        Assert.Single(uploadedTo);
    }

    [Fact]
    public async Task WithoutCreateArtifact_KeepsTheRelayPathAndItsConfirmDownload()
    {
        await using var fixture = await Fixture.CreateAsync(primaryWatermark: 5);
        fixture.CommitSucceeds();
        fixture.Provisioner
            .TryGetReplicationArtifactAsync(
                Arg.Any<StorageNodeDto>(),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<long>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(ReplicationArtifactFetchResult.Ok(Manifest, [1, 2, 3]));

        var result = await fixture.RunAsync(
            appliedWatermark: 6,
            createArtifact: false,
            confirmedEncryptedNodeIds: [fixture.EncAId]
        );

        Assert.True(result.Success, result.Error);
        await fixture.Provisioner
            .Received()
            .TryGetReplicationArtifactAsync(
                Arg.Is<StorageNodeDto>(node => node.Id.Value == fixture.EncAId),
                Arg.Any<string>(),
                fixture.RepositoryId,
                6,
                Arg.Any<CancellationToken>()
            );
        await fixture.Provisioner
            .DidNotReceive()
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
            );
    }

    [Fact]
    public async Task WithoutCreateArtifact_StillRequiresAnEncryptedConfirmation()
    {
        await using var fixture = await Fixture.CreateAsync(primaryWatermark: 5);
        fixture.CommitSucceeds();

        var result = await fixture.RunAsync(appliedWatermark: 6, createArtifact: false);

        Assert.False(result.Success);
        Assert.Contains("encrypted", result.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fixture.Commits);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

        private Fixture(ServiceProvider provider)
        {
            _provider = provider;
            _contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        }

        public Guid RepositoryId { get; } = Guid.NewGuid();

        public Guid PrimaryId { get; } = Guid.NewGuid();

        public Guid EncAId { get; } = Guid.NewGuid();

        public Guid EncBId { get; } = Guid.NewGuid();

        public string PhysicalPath => $"/srv/git/{RepositoryId}.git";

        public IQueryProcessor QueryProcessor { get; } = Substitute.For<IQueryProcessor>();

        public IStorageProvisionerClient Provisioner { get; } = Substitute.For<IStorageProvisionerClient>();

        public List<CommitReplicationWatermarkQuery> Commits { get; } = [];

        public Action? OnCreate { get; set; }

        private long PrimaryWatermark { get; set; }

        public static async Task<Fixture> CreateAsync(
            long primaryWatermark,
            DateTimeOffset? primaryLastSyncedAt = null,
            bool requestScoped = false,
            int uploadTimeoutSeconds = 120
        )
        {
            var connection = SqliteTestConnection.OpenInMemory();
            var services = new ServiceCollection();
            var repositoryKeyService = Substitute.For<IRepositoryKeyService>();
            var fixtureHolder = new Fixture[1];
            services.AddSingleton(connection);
            services.AddLogging();
            services.AddSingleton(
                Options.Create(new HaStorageBackgroundOptions { ArtifactUploadTimeoutSeconds = uploadTimeoutSeconds })
            );
            if (requestScoped)
            {
                // Like AddCqrs: the query processor belongs to a scope and stops working with it.
                services.AddScoped<IQueryProcessor>(_ => new ScopeBoundQueryProcessor(fixtureHolder[0].QueryProcessor));
                services.AddTransient<ReplicationArtifactPublisher>();
                services.AddTransient<ReplicaCatchUpService>();
                services.AddTransient<QuorumReplicateRepositoryQueryHandler>();
            }
            else
            {
                services.AddSingleton(_ => fixtureHolder[0].QueryProcessor);
                services.AddSingleton<ReplicationArtifactPublisher>();
                services.AddSingleton<ReplicaCatchUpService>();
                services.AddSingleton<QuorumReplicateRepositoryQueryHandler>();
            }

            services.AddSingleton(_ => fixtureHolder[0].Provisioner);
            services.AddSingleton(repositoryKeyService);
            services.AddSingleton<IFeatureAssemblyProvider>(
                new FeatureAssemblyProvider(
                    [
                        typeof(RepositoryMapsterConfig).Assembly,
                        typeof(global::OpenGitBase.Features.StorageNode.StorageNodeMapsterConfig).Assembly,
                    ]
                )
            );
            services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
            var fixture = new Fixture(services.BuildServiceProvider()) { PrimaryWatermark = primaryWatermark };
            fixtureHolder[0] = fixture;

            repositoryKeyService
                .TryGetRepositoryKeyAsync(fixture.RepositoryId, Arg.Any<CancellationToken>())
                .Returns(new EphemeralRepositoryKey(new byte[32], 1));
            await fixture.SeedAsync(primaryWatermark, primaryLastSyncedAt);
            fixture.ConfigureDefaults();
            return fixture;
        }

        public void CommitSucceeds() =>
            QueryProcessor
                .RunQueryAsync(Arg.Any<CommitReplicationWatermarkQuery>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var query = callInfo.Arg<CommitReplicationWatermarkQuery>();
                    Commits.Add(query);
                    PrimaryWatermark = query.NewWatermark;
                    return Option.From(CommitReplicationWatermarkResult.Committed(query.NewWatermark));
                });

        public void CommitFailsBecauseSomeoneElseCommitted() =>
            QueryProcessor
                .RunQueryAsync(Arg.Any<CommitReplicationWatermarkQuery>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    PrimaryWatermark = callInfo.Arg<CommitReplicationWatermarkQuery>().NewWatermark;
                    return Option.From(
                        CommitReplicationWatermarkResult.Failed(
                            "Watermark was committed concurrently; it must increment monotonically by one."
                        )
                    );
                });

        public async Task<QuorumReplicateRepositoryResult> RunAsync(
            long appliedWatermark,
            bool createArtifact,
            IReadOnlyList<Guid>? confirmedEncryptedNodeIds = null,
            DateTimeOffset? expectedPrimaryLastSyncedAt = null
        )
        {
            // A request's scope: disposed as soon as the handler has answered.
            await using var scope = _provider.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<QuorumReplicateRepositoryQueryHandler>();
            var result = await handler.RunQueryAsync(
                new QuorumReplicateRepositoryQuery
                {
                    RepositoryId = global::OpenGitBase.Features.Repository.Contracts.RepositoryId.From(RepositoryId),
                    StorageNodeId = StorageNodeId.From(PrimaryId),
                    AppliedWatermark = appliedWatermark,
                    CreateArtifact = createArtifact,
                    ConfirmedEncryptedNodeIds = confirmedEncryptedNodeIds ?? [],
                    ExpectedPrimaryLastSyncedAt = expectedPrimaryLastSyncedAt,
                },
                CancellationToken.None
            );
            Assert.True(result.IsSome);
            return result.Get();
        }

        /// <summary>
        /// Lets the first lookup of <paramref name="nodeId"/> through, and holds every later one
        /// until <paramref name="release"/> completes.
        /// </summary>
        public void HoldLaterLookupsOf(Guid nodeId, Task release)
        {
            var lookups = 0;
            QueryProcessor
                .RunQueryAsync(
                    Arg.Is<GetStorageNodeQuery>(query => query.ModelId.Value == nodeId),
                    Arg.Any<CancellationToken>()
                )
                .Returns(async _ =>
                {
                    if (Interlocked.Increment(ref lookups) > 1)
                    {
                        await release;
                    }

                    return Option.From(Node(nodeId));
                });
        }

        public DateTimeOffset? ReadPrimaryLastSyncedAt()
        {
            using var context = _contextFactory.CreateDbContext();
            return context
                .Set<RepositoryReplicaEntity>()
                .AsNoTracking()
                .Single(replica => replica.RepositoryId == RepositoryId && replica.StorageNodeId == PrimaryId)
                .LastSyncedAt;
        }

        public void SetPrimaryLastSyncedAt(DateTimeOffset value)
        {
            using var context = _contextFactory.CreateDbContext();
            var primary = context
                .Set<RepositoryReplicaEntity>()
                .Single(replica => replica.RepositoryId == RepositoryId && replica.StorageNodeId == PrimaryId);
            primary.LastSyncedAt = value;
            context.SaveChanges();
        }

        public async Task<long?> WaitForArtifactWatermarkAsync(Guid storageNodeId, long expected)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            long? watermark = null;
            while (DateTime.UtcNow < deadline)
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                watermark = await context
                    .Set<RepositoryReplicaEntity>()
                    .Where(replica => replica.RepositoryId == RepositoryId && replica.StorageNodeId == storageNodeId)
                    .Select(replica => replica.ArtifactWatermark)
                    .SingleAsync();
                if (watermark == expected)
                {
                    break;
                }

                await Task.Delay(50);
            }

            return watermark;
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
            };

        private static RepositoryReplicationPeerDto Peer(Guid nodeId, RepositoryReplicaRole role) =>
            new()
            {
                StorageNodeId = nodeId,
                InternalHost = nodeId.ToString(),
                InternalHttpPort = 8081,
                Role = role.ToString(),
                IsHealthy = true,
            };

        private async Task SeedAsync(long primaryWatermark, DateTimeOffset? primaryLastSyncedAt)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await context.Database.EnsureCreatedAsync();
            context.Set<RepositoryEntity>().Add(
                new RepositoryEntity
                {
                    Id = RepositoryId,
                    Name = "repo",
                    Slug = "repo",
                    OwnerUserId = Guid.NewGuid(),
                    PhysicalPath = PhysicalPath,
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
                            RepositoryId = RepositoryId,
                            StorageNodeId = PrimaryId,
                            Role = RepositoryReplicaRole.Primary,
                            AppliedWatermark = primaryWatermark + 1,
                            LastSyncedAt = primaryLastSyncedAt,
                        },
                        new RepositoryReplicaEntity
                        {
                            RepositoryId = RepositoryId,
                            StorageNodeId = EncAId,
                            Role = RepositoryReplicaRole.EncryptedReplica,
                            ArtifactWatermark = primaryWatermark,
                        },
                        new RepositoryReplicaEntity
                        {
                            RepositoryId = RepositoryId,
                            StorageNodeId = EncBId,
                            Role = RepositoryReplicaRole.EncryptedReplica,
                            ArtifactWatermark = primaryWatermark,
                        },
                    ],
                }
            );
            await context.SaveChangesAsync();
        }

        private void ConfigureDefaults()
        {
            QueryProcessor
                .RunQueryAsync(Arg.Any<GetRepositoryReplicationContextQuery>(), Arg.Any<CancellationToken>())
                .Returns(_ => Option.From(BuildContext()));
            foreach (var nodeId in new[] { PrimaryId, EncAId, EncBId })
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
                .Returns(_ =>
                {
                    OnCreate?.Invoke();
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
                .Returns(StorageProvisionerResult.Ok(201));
        }

        private RepositoryReplicationContextDto BuildContext() =>
            new()
            {
                RepositoryId = RepositoryId,
                ReplicationEpoch = 1,
                PrimaryWatermark = PrimaryWatermark,
                IsPrimary = true,
                PhysicalPath = PhysicalPath,
                ReplicationState = nameof(ReplicationState.Rf4Healthy),
                Peers =
                [
                    Peer(PrimaryId, RepositoryReplicaRole.Primary),
                    Peer(EncAId, RepositoryReplicaRole.EncryptedReplica),
                    Peer(EncBId, RepositoryReplicaRole.EncryptedReplica),
                ],
            };
    }

    /// <summary>A query processor that, like the request-scoped one in production, dies with its scope.</summary>
    private sealed class ScopeBoundQueryProcessor : IQueryProcessor, IDisposable
    {
        private readonly IQueryProcessor _inner;
        private bool _disposed;

        public ScopeBoundQueryProcessor(IQueryProcessor inner)
        {
            _inner = inner;
        }

        public Task<Option<TResult>> RunQueryAsync<TQuery, TResult>(
            IQuery<TResult, TQuery> query,
            CancellationToken cancellationToken
        )
            where TQuery : IQuery<TResult, TQuery>
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _inner.RunQueryAsync(query, cancellationToken);
        }

        public void Dispose() => _disposed = true;
    }
}
