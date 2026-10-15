using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenGitBase.Api.Services;
using OpenGitBase.Api.Tests.Services.Lfs;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Common.Storage;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Api.Tests.Services;

/// <summary>
/// Cold recovery rebuilds the bare repository from an encrypted artifact on a healthy plaintext node. Once git is
/// back the repository is writable, and the LFS objects follow in the background (lfs-06).
/// </summary>
public sealed class ColdRecoveryServiceTests : IAsyncDisposable
{
    private const string LostPrimary = "storage-1";
    private const string EncryptedA = "storage-2";
    private const string EncryptedB = "storage-3";
    private const string Target = "storage-4";
    private const long Watermark = 5;

    private readonly ServiceProvider _provider;
    private readonly IQueryProcessor _queryProcessor = Substitute.For<IQueryProcessor>();
    private readonly IRepositoryKeyService _keys = Substitute.For<IRepositoryKeyService>();
    private readonly IStorageProvisionerClient _provisioner = Substitute.For<IStorageProvisionerClient>();
    private readonly RecordingLfsRepairQueue _repairs = new();
    private readonly Guid _repositoryId = Guid.NewGuid();
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);
    private readonly string _oid = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

    public ColdRecoveryServiceTests()
    {
        var bundle = RandomNumberGenerator.GetBytes(256);
        var artifacts = new EncryptedArtifactService();
        var manifest = new EncryptedArtifactManifest(1, Watermark, artifacts.ComputeBundleSha256(bundle), 1);
        var envelope = artifacts.EncryptBundle(bundle, _key, manifest, _repositoryId);

        _keys
            .TryGetRepositoryKeyAsync(_repositoryId, Arg.Any<CancellationToken>())
            .Returns(_ => new EphemeralRepositoryKey((byte[])_key.Clone(), 1));
        _queryProcessor
            .RunQueryAsync(Arg.Any<GetStorageNodeQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => Option.From(Node(Host(((GetStorageNodeQuery)call[0]).ModelId.Value))));
        _queryProcessor
            .RunQueryAsync(Arg.Any<GetStorageNodeApiTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From("node-token"));
        _queryProcessor
            .RunQueryAsync(Arg.Any<ListHealthyStorageNodesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From<IReadOnlyList<StorageNodeDto>>([Node(EncryptedA), Node(EncryptedB), Node(Target)]));
        _provisioner
            .TryGetReplicationArtifactAsync(
                Arg.Any<StorageNodeDto>(),
                Arg.Any<string>(),
                _repositoryId,
                Watermark,
                Arg.Any<CancellationToken>()
            )
            .Returns(ReplicationArtifactFetchResult.Ok(EncryptedArtifactService.SerializeManifest(manifest), envelope.Ciphertext));
        _provisioner
            .ImportRepositoryBundleAsync(
                Arg.Any<StorageNodeDto>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(StorageProvisionerResult.Ok());

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
        _provider = services.BuildServiceProvider();
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();

    [Fact]
    public async Task TryRecoverAsync_OnceGitIsRestored_LeavesTheRepositoryWritableAndQueuesTheLfsRestore()
    {
        await SeedAsync();

        var recovered = await CreateService().TryRecoverAsync(_repositoryId);

        Assert.True(recovered);
        await using var context = await CreateContextAsync();
        var repository = await context.Set<RepositoryEntity>().Include(entity => entity.Replicas).SingleAsync();
        Assert.Equal(ReplicationState.Rf4Healthy, repository.ReplicationState);
        Assert.Equal(NodeId(Target), repository.PrimaryStorageNodeId);
        Assert.Equal([_repositoryId], _repairs.Scheduled);
    }

    [Fact]
    public async Task TryRecoverAsync_PrefersANodeWithoutACopyOverOneOfTheEncryptedReplicas()
    {
        // The encrypted copies sit on storage-2 and storage-4 (placement follows free space, not node names), so
        // only storage-3 holds nothing of the repository. Recovering onto storage-4 would leave one encrypted copy.
        await SeedAsync(encryptedB: Target);
        _queryProcessor
            .RunQueryAsync(Arg.Any<ListHealthyStorageNodesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From<IReadOnlyList<StorageNodeDto>>([Node(Target), Node(EncryptedA), Node(EncryptedB)]));

        Assert.True(await CreateService().TryRecoverAsync(_repositoryId));

        await using var context = await CreateContextAsync();
        var repository = await context.Set<RepositoryEntity>().Include(entity => entity.Replicas).SingleAsync();
        Assert.Equal(NodeId(EncryptedB), repository.PrimaryStorageNodeId);
        Assert.Equal(
            2,
            repository.Replicas.Count(replica => replica.Role == RepositoryReplicaRole.EncryptedReplica)
        );
    }

    [Fact]
    public async Task TryRecoverAsync_OnANodeWithoutAReplicaRow_RecordsItAsThePrimaryReplica()
    {
        await SeedAsync();

        await CreateService().TryRecoverAsync(_repositoryId);

        await using var context = await CreateContextAsync();
        var target = await context
            .Set<RepositoryReplicaEntity>()
            .SingleAsync(replica => replica.RepositoryId == _repositoryId && replica.StorageNodeId == NodeId(Target));
        Assert.Equal(RepositoryReplicaRole.Primary, target.Role);
        Assert.Equal(Watermark, target.AppliedWatermark);
        Assert.Equal(
            RepositoryReplicaRole.Replica,
            (await context.Set<RepositoryReplicaEntity>().SingleAsync(replica => replica.StorageNodeId == NodeId(LostPrimary))).Role
        );
    }

    [Fact]
    public async Task TryRecoverAsync_ForgetsThePlaintextCopiesOfTheLostNodesAndKeepsTheEncryptedOnes()
    {
        await SeedAsync();

        await CreateService().TryRecoverAsync(_repositoryId);

        await using var context = await CreateContextAsync();
        var copies = await context.Set<LfsObjectCopyEntity>().AsNoTracking().ToListAsync();
        Assert.Equal(new[] { NodeId(EncryptedA), NodeId(EncryptedB) }.Order(), copies.Select(copy => copy.StorageNodeId).Order());
        Assert.All(copies, copy => Assert.Equal(LfsObjectCopyKind.Encrypted, copy.Kind));
        Assert.Single(await context.Set<LfsObjectEntity>().AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task TryRecoverAsync_RunAgain_KeepsOnePrimaryRowAndTheCopiesRestoredMeanwhile()
    {
        await SeedAsync();
        var service = CreateService();
        await service.TryRecoverAsync(_repositoryId);
        await using (var context = await CreateContextAsync())
        {
            context.Set<LfsObjectCopyEntity>().Add(Copy(Target, LfsObjectCopyKind.Plaintext));
            await context.SaveChangesAsync();
        }

        var again = await service.TryRecoverAsync(_repositoryId);

        Assert.True(again);
        await using var check = await CreateContextAsync();
        Assert.Single(await check.Set<RepositoryReplicaEntity>().Where(replica => replica.StorageNodeId == NodeId(Target)).ToListAsync());
        Assert.NotNull(await check.Set<LfsObjectCopyEntity>().SingleOrDefaultAsync(copy => copy.StorageNodeId == NodeId(Target)));
        Assert.Equal([_repositoryId, _repositoryId], _repairs.Scheduled);
    }

    [Fact]
    public async Task TryRecoverAsync_WhenTheImportFails_QueuesNoLfsRestoreAndKeepsTheCopyRows()
    {
        await SeedAsync();
        _provisioner
            .ImportRepositoryBundleAsync(
                Arg.Any<StorageNodeDto>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(StorageProvisionerResult.Fail(500, "disk full"));

        var recovered = await CreateService().TryRecoverAsync(_repositoryId);

        Assert.False(recovered);
        Assert.Empty(_repairs.Scheduled);
        await using var context = await CreateContextAsync();
        Assert.Equal(3, await context.Set<LfsObjectCopyEntity>().CountAsync());
        Assert.DoesNotContain(
            await context.Set<RepositoryReplicaEntity>().ToListAsync(),
            replica => replica.StorageNodeId == NodeId(Target)
        );
    }

    private static Guid NodeId(string host) => FakeLfsStorageNodes.NodeId(host);

    private static string Host(Guid nodeId) =>
        new[] { LostPrimary, EncryptedA, EncryptedB, Target }.Single(host => NodeId(host) == nodeId);

    private static StorageNodeDto Node(string host) =>
        new()
        {
            Id = StorageNodeId.From(NodeId(host)),
            NodeId = host,
            InternalHost = host,
            InternalHttpPort = 8081,
            IsHealthy = host != LostPrimary,
        };

    private ColdRecoveryService CreateService() =>
        new(
            _provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>(),
            _queryProcessor,
            _keys,
            new EncryptedArtifactService(),
            _provisioner,
            _repairs,
            NullLogger<ColdRecoveryService>.Instance
        );

    private LfsObjectCopyEntity Copy(string host, LfsObjectCopyKind kind) =>
        new()
        {
            RepositoryId = _repositoryId,
            Oid = _oid,
            StorageNodeId = NodeId(host),
            Kind = kind,
            StoredAt = DateTimeOffset.UtcNow.AddDays(-1),
            LastVerifiedAt = DateTimeOffset.UtcNow.AddDays(-1),
        };

    /// <summary>
    /// RF4 with its plaintext node lost: storage-1 held primary and read replica, storage-2 and storage-3 hold
    /// encrypted copies (unless given others), and storage-4 is healthy but holds nothing of the repository.
    /// </summary>
    private async Task SeedAsync(string encryptedA = EncryptedA, string encryptedB = EncryptedB)
    {
        await using var context = await CreateContextAsync();
        await context.Database.EnsureCreatedAsync();
        foreach (var host in new[] { LostPrimary, EncryptedA, EncryptedB, Target })
        {
            context.Set<StorageNodeEntity>().Add(
                new StorageNodeEntity
                {
                    Id = NodeId(host),
                    NodeId = host,
                    InternalHost = host,
                    InternalHttpPort = 8081,
                    IsHealthy = host != LostPrimary,
                    RegisteredAt = DateTimeOffset.UtcNow,
                }
            );
        }

        context.Set<RepositoryEntity>().Add(
            new RepositoryEntity
            {
                Id = _repositoryId,
                Name = "repo",
                Slug = "repo",
                OwnerUserId = Guid.NewGuid(),
                PhysicalPath = $"/srv/git/{_repositoryId}.git",
                StorageNodeId = NodeId(LostPrimary),
                PrimaryStorageNodeId = NodeId(LostPrimary),
                PrimaryWatermark = Watermark,
                ReplicationEpoch = 1,
                ReplicationState = ReplicationState.Rf4Healthy,
                Replicas =
                [
                    new RepositoryReplicaEntity
                    {
                        RepositoryId = _repositoryId,
                        StorageNodeId = NodeId(LostPrimary),
                        Role = RepositoryReplicaRole.Primary,
                        AppliedWatermark = Watermark,
                    },
                    new RepositoryReplicaEntity
                    {
                        RepositoryId = _repositoryId,
                        StorageNodeId = NodeId(encryptedA),
                        Role = RepositoryReplicaRole.EncryptedReplica,
                        ArtifactWatermark = Watermark,
                    },
                    new RepositoryReplicaEntity
                    {
                        RepositoryId = _repositoryId,
                        StorageNodeId = NodeId(encryptedB),
                        Role = RepositoryReplicaRole.EncryptedReplica,
                        ArtifactWatermark = Watermark,
                    },
                ],
            }
        );
        context.Set<LfsObjectEntity>().Add(
            new LfsObjectEntity
            {
                RepositoryId = _repositoryId,
                Oid = _oid,
                Size = 4096,
                KeyVersion = 1,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            }
        );
        context.Set<LfsObjectCopyEntity>().AddRange(
            Copy(LostPrimary, LfsObjectCopyKind.Plaintext),
            Copy(encryptedA, LfsObjectCopyKind.Encrypted),
            Copy(encryptedB, LfsObjectCopyKind.Encrypted)
        );
        await context.SaveChangesAsync();
    }

    private Task<OpenGitBaseDbContext> CreateContextAsync() =>
        _provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>().CreateDbContextAsync();
}
