using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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

namespace OpenGitBase.Api.Tests.Services.Lfs;

/// <summary>
/// The reconciler's LFS step (lfs-05): it trusts each node's on-disk inventory, not the LfsObjectCopy rows, and
/// repairs every missing copy from a good one.
/// </summary>
public sealed class LfsCopyReconcilerTests : IAsyncDisposable
{
    private const string Primary = "storage-1";
    private const string EncryptedA = "storage-2";
    private const string EncryptedB = "storage-3";
    private const string ReadReplica = "storage-4";
    private const string NewReplica = "storage-5";
    private const int KeyVersion = 1;

    private readonly ServiceProvider _provider;
    private readonly IQueryProcessor _queryProcessor = Substitute.For<IQueryProcessor>();
    private readonly IRepositoryKeyService _keys = Substitute.For<IRepositoryKeyService>();
    private readonly FakeLfsStorageNodes _nodes = new();
    private readonly RecordingLogger<LfsCopyReconciler> _logger = new();
    private readonly LfsOptions _options = new() { SegmentBytes = 1024, ReplicaUploadTimeoutSeconds = 60 };
    private readonly Guid _repositoryId = Guid.NewGuid();
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);
    private readonly HashSet<string> _healthy = new(StringComparer.Ordinal) { Primary, EncryptedA, EncryptedB, ReadReplica, NewReplica };
    private readonly Dictionary<string, RepositoryReplicaRole> _replicas = new(StringComparer.Ordinal)
    {
        [Primary] = RepositoryReplicaRole.Primary,
        [ReadReplica] = RepositoryReplicaRole.ReadReplica,
        [EncryptedA] = RepositoryReplicaRole.EncryptedReplica,
        [EncryptedB] = RepositoryReplicaRole.EncryptedReplica,
    };

    public LfsCopyReconcilerTests()
    {
        _nodes.Kinds[EncryptedA] = LfsObjectCopyKind.Encrypted;
        _nodes.Kinds[EncryptedB] = LfsObjectCopyKind.Encrypted;
        _nodes.Kinds[NewReplica] = LfsObjectCopyKind.Encrypted;
        _keys
            .TryGetRepositoryKeyAsync(_repositoryId, Arg.Any<CancellationToken>())
            .Returns(_ => new EphemeralRepositoryKey((byte[])_key.Clone(), KeyVersion));
        _queryProcessor
            .RunQueryAsync(Arg.Any<ListHealthyStorageNodesQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => Option.From<IReadOnlyList<StorageNodeDto>>(_healthy.Select(Node).ToList()));
        _queryProcessor
            .RunQueryAsync(Arg.Any<GetStorageNodeApiTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From("node-token"));

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
    public async Task ReconcileRepositoryAsync_WhenAnEncryptedCopyIsGoneFromDiskButStillRecorded_ReEncryptsItFromPlaintext()
    {
        var content = Content("lost-encrypted");
        var oid = await RecordEverywhereAsync(content);
        _nodes.Remove(EncryptedA, _repositoryId, oid, LfsObjectCopyKind.Encrypted);

        var result = await ReconcileAsync();

        var restored = _nodes.Find(EncryptedA, _repositoryId, oid, LfsObjectCopyKind.Encrypted);
        Assert.NotNull(restored);
        Assert.Equal(content, await DecryptAsync(restored, oid));
        Assert.Equal((1, 1, 0, 0), (result.Missing, result.Repaired, result.Failed, result.Remaining));
        var copy = await FindCopyAsync(oid, EncryptedA);
        Assert.NotNull(copy);
        Assert.Equal(LfsObjectCopyKind.Encrypted, copy.Kind);
        Assert.NotNull(copy.LastVerifiedAt);
    }

    [Fact]
    public async Task ReconcileRepositoryAsync_WhenAPlaintextCopyIsGone_CopiesItFromTheOtherPlaintextNode()
    {
        var content = Content("lost-on-primary");
        var oid = await RecordEverywhereAsync(content);
        _nodes.Remove(Primary, _repositoryId, oid, LfsObjectCopyKind.Plaintext);

        var result = await ReconcileAsync();

        Assert.Equal(content, _nodes.Find(Primary, _repositoryId, oid, LfsObjectCopyKind.Plaintext));
        Assert.Equal(1, result.Repaired);
        Assert.DoesNotContain(_nodes.Requests, request => request.Method == HttpMethod.Get && request.Uri.AbsolutePath.Contains("/encrypted/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReconcileRepositoryAsync_WhenNoPlaintextCopyIsLeft_DecryptsOneFromAnEncryptedReplica()
    {
        var content = Content("plaintext-lost-everywhere");
        var oid = await RecordEverywhereAsync(content);
        _nodes.Remove(Primary, _repositoryId, oid, LfsObjectCopyKind.Plaintext);
        _nodes.Remove(ReadReplica, _repositoryId, oid, LfsObjectCopyKind.Plaintext);

        var result = await ReconcileAsync();

        Assert.Equal(content, _nodes.Find(Primary, _repositoryId, oid, LfsObjectCopyKind.Plaintext));
        Assert.Equal(content, _nodes.Find(ReadReplica, _repositoryId, oid, LfsObjectCopyKind.Plaintext));
        Assert.Equal((2, 2, 0), (result.Missing, result.Repaired, result.Failed));
    }

    [Fact]
    public async Task ReconcileRepositoryAsync_NeverWritesPlaintextFromAnEncryptedCopyThatFailsVerification()
    {
        var content = Content("tampered");
        var oid = await RecordEverywhereAsync(content);
        _nodes.Remove(Primary, _repositoryId, oid, LfsObjectCopyKind.Plaintext);
        _nodes.Remove(ReadReplica, _repositoryId, oid, LfsObjectCopyKind.Plaintext);
        foreach (var host in new[] { EncryptedA, EncryptedB })
        {
            var tampered = _nodes.Find(host, _repositoryId, oid, LfsObjectCopyKind.Encrypted)!;
            tampered[^1] ^= 0x01;
            _nodes.Store(host, _repositoryId, oid, LfsObjectCopyKind.Encrypted, tampered);
        }

        var result = await ReconcileAsync();

        Assert.Null(_nodes.Find(Primary, _repositoryId, oid, LfsObjectCopyKind.Plaintext));
        Assert.Null(_nodes.Find(ReadReplica, _repositoryId, oid, LfsObjectCopyKind.Plaintext));
        Assert.Equal((0, 2), (result.Repaired, result.Failed));
        Assert.Contains(_logger.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains($"Could not repair LFS object {oid} of {_repositoryId} on {Primary}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReconcileRepositoryAsync_DeletesTheRowsOfMissingCopiesAndUpdatesLastVerifiedAtOfFoundOnes()
    {
        var content = Content("gone-everywhere-but-primary");
        var oid = await RecordEverywhereAsync(content);
        foreach (var host in new[] { ReadReplica, EncryptedA, EncryptedB })
        {
            _nodes.Remove(host, _repositoryId, oid, host == ReadReplica ? LfsObjectCopyKind.Plaintext : LfsObjectCopyKind.Encrypted);
        }

        _nodes.FailingHosts.Add(ReadReplica);
        _keys.TryGetRepositoryKeyAsync(_repositoryId, Arg.Any<CancellationToken>()).Returns((EphemeralRepositoryKey?)null);

        await ReconcileAsync();

        Assert.NotNull((await FindCopyAsync(oid, Primary))?.LastVerifiedAt);
        Assert.Null(await FindCopyAsync(oid, EncryptedA));
        Assert.Null(await FindCopyAsync(oid, EncryptedB));
        Assert.Contains(_logger.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains($"LFS object {oid} of {_repositoryId} missing on {EncryptedA}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReconcileRepositoryAsync_SkipsAnUnreachableNodeAndLeavesItsRowsButRepairsTheOthers()
    {
        var content = Content("one-node-down");
        var oid = await RecordEverywhereAsync(content);
        _nodes.Remove(EncryptedA, _repositoryId, oid, LfsObjectCopyKind.Encrypted);
        _nodes.Remove(EncryptedB, _repositoryId, oid, LfsObjectCopyKind.Encrypted);
        _nodes.FailingHosts.Add(EncryptedB);

        var result = await ReconcileAsync();

        Assert.NotNull(_nodes.Find(EncryptedA, _repositoryId, oid, LfsObjectCopyKind.Encrypted));
        var untouched = await FindCopyAsync(oid, EncryptedB);
        Assert.NotNull(untouched);
        Assert.Null(untouched.LastVerifiedAt);
        Assert.Equal((1, 1), (result.Missing, result.Repaired));
        Assert.Contains(_logger.Entries, entry => entry.Message.Contains($"Could not read the LFS inventory of {EncryptedB}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReconcileRepositoryAsync_LeavesUnhealthyNodesAlone()
    {
        var oid = await RecordEverywhereAsync(Content("unhealthy"));
        _nodes.Remove(EncryptedA, _repositoryId, oid, LfsObjectCopyKind.Encrypted);
        _healthy.Remove(EncryptedA);

        var result = await ReconcileAsync();

        Assert.DoesNotContain(_nodes.Requests, request => request.Uri.Host == EncryptedA);
        Assert.NotNull(await FindCopyAsync(oid, EncryptedA));
        Assert.Equal(0, result.Missing);
    }

    [Fact]
    public async Task ReconcileRepositoryAsync_FillsANewlyAssignedReplicaWithEveryObject()
    {
        var oids = new List<string>();
        foreach (var name in new[] { "one", "two", "three" })
        {
            oids.Add(await RecordEverywhereAsync(Content(name)));
        }

        await AddReplicaAsync(NewReplica, RepositoryReplicaRole.EncryptedReplica);

        var result = await ReconcileAsync();

        Assert.Equal(3, result.Repaired);
        foreach (var oid in oids)
        {
            Assert.NotNull(_nodes.Find(NewReplica, _repositoryId, oid, LfsObjectCopyKind.Encrypted));
            Assert.Equal(LfsObjectCopyKind.Encrypted, (await FindCopyAsync(oid, NewReplica))?.Kind);
        }
    }

    [Fact]
    public async Task ReconcileRepositoryAsync_RecordsCopiesFoundOnDiskThatHadNoRow()
    {
        var content = Content("unrecorded-copy");
        var oid = await RecordEverywhereAsync(content);
        await using (var context = await CreateContextAsync())
        {
            await context.Set<LfsObjectCopyEntity>().Where(copy => copy.StorageNodeId == FakeLfsStorageNodes.NodeId(ReadReplica)).ExecuteDeleteAsync();
        }

        var result = await ReconcileAsync();

        Assert.Equal(0, result.Missing);
        Assert.Equal(LfsObjectCopyKind.Plaintext, (await FindCopyAsync(oid, ReadReplica))?.Kind);
    }

    [Fact]
    public async Task ReconcileRepositoryAsync_TreatsACopyOfTheWrongSizeAsMissing()
    {
        var content = Content("truncated");
        var oid = await RecordEverywhereAsync(content);
        var truncated = _nodes.Find(EncryptedA, _repositoryId, oid, LfsObjectCopyKind.Encrypted)![..100];
        _nodes.Store(EncryptedA, _repositoryId, oid, LfsObjectCopyKind.Encrypted, truncated);

        var result = await ReconcileAsync();

        Assert.Equal(1, result.Repaired);
        Assert.Equal(content, await DecryptAsync(_nodes.Find(EncryptedA, _repositoryId, oid, LfsObjectCopyKind.Encrypted)!, oid));
    }

    [Fact]
    public async Task ReconcileRepositoryAsync_RepairsTheLongestMissingFirstAndCapsARun()
    {
        _options.MaxRepairsPerRun = 1;
        var recent = await RecordEverywhereAsync(Content("seen-recently"), lastVerifiedAt: DateTimeOffset.UtcNow.AddMinutes(-5));
        var old = await RecordEverywhereAsync(Content("seen-long-ago"), lastVerifiedAt: DateTimeOffset.UtcNow.AddDays(-2));
        _nodes.Remove(EncryptedA, _repositoryId, recent, LfsObjectCopyKind.Encrypted);
        _nodes.Remove(EncryptedA, _repositoryId, old, LfsObjectCopyKind.Encrypted);

        var result = await ReconcileAsync();

        Assert.NotNull(_nodes.Find(EncryptedA, _repositoryId, old, LfsObjectCopyKind.Encrypted));
        Assert.Null(_nodes.Find(EncryptedA, _repositoryId, recent, LfsObjectCopyKind.Encrypted));
        Assert.Equal((2, 1, 1), (result.Missing, result.Repaired, result.Remaining));
    }

    [Fact]
    public async Task ReconcileRepositoryAsync_LogsEveryRepairWithRepositoryOidAndNode()
    {
        var oid = await RecordEverywhereAsync(Content("logged"));
        _nodes.Remove(EncryptedB, _repositoryId, oid, LfsObjectCopyKind.Encrypted);

        await ReconcileAsync();

        Assert.Contains(_logger.Entries, entry => entry.Message.Contains($"Repaired LFS object {oid} of {_repositoryId} on {EncryptedB}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReconcileRepositoryAsync_SkipsANodeWhoseInventoryReportsTheWrongKind()
    {
        var oid = await RecordEverywhereAsync(Content("wrong-kind"));
        _nodes.Kinds[EncryptedA] = LfsObjectCopyKind.Plaintext;

        var result = await ReconcileAsync();

        Assert.Equal(0, result.Missing);
        Assert.NotNull(await FindCopyAsync(oid, EncryptedA));
        Assert.DoesNotContain(_nodes.Requests, request => request.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task ReconcileRepositoryAsync_OfARepositoryWithoutLfsObjects_AsksNoNode()
    {
        await SeedRepositoryAsync();

        var result = await ReconcileAsync();

        Assert.Equal(0, result.Missing);
        Assert.Empty(_nodes.Requests);
    }

    private static StorageNodeDto Node(string host) =>
        new()
        {
            Id = StorageNodeId.From(FakeLfsStorageNodes.NodeId(host)),
            NodeId = host,
            InternalHost = host,
            InternalHttpPort = 8081,
            IsHealthy = true,
        };

    private static byte[] Content(string name) =>
        [0, .. System.Text.Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat($"{name}-", 400)))];

    private async Task<LfsReconcileResult> ReconcileAsync()
    {
        var client = _nodes.CreateClient();
        var transfers = new LfsReplicationService(_queryProcessor, client, _keys, _options, NullLogger<LfsReplicationService>.Instance);
        var reconciler = new LfsCopyReconciler(
            _provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>(),
            _queryProcessor,
            client,
            transfers,
            _keys,
            _options,
            _logger
        );
        return await reconciler.ReconcileRepositoryAsync(_repositoryId, CancellationToken.None);
    }

    /// <summary>Stores the object on every node of the repository and records it with a copy row per node.</summary>
    private async Task<string> RecordEverywhereAsync(byte[] content, DateTimeOffset? lastVerifiedAt = null)
    {
        await SeedRepositoryAsync();
        var oid = Convert.ToHexStringLower(SHA256.HashData(content));
        await using var context = await CreateContextAsync();
        context.Set<LfsObjectEntity>().Add(
            new LfsObjectEntity
            {
                RepositoryId = _repositoryId,
                Oid = oid,
                Size = content.Length,
                KeyVersion = KeyVersion,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-7),
            }
        );
        foreach (var (host, role) in _replicas)
        {
            var kind = role == RepositoryReplicaRole.EncryptedReplica ? LfsObjectCopyKind.Encrypted : LfsObjectCopyKind.Plaintext;
            _nodes.Store(host, _repositoryId, oid, kind, kind == LfsObjectCopyKind.Encrypted ? await EncryptAsync(content, oid) : content);
            context.Set<LfsObjectCopyEntity>().Add(
                new LfsObjectCopyEntity
                {
                    RepositoryId = _repositoryId,
                    Oid = oid,
                    StorageNodeId = FakeLfsStorageNodes.NodeId(host),
                    Kind = kind,
                    StoredAt = DateTimeOffset.UtcNow.AddDays(-7),
                    LastVerifiedAt = lastVerifiedAt,
                }
            );
        }

        await context.SaveChangesAsync();
        return oid;
    }

    private async Task SeedRepositoryAsync()
    {
        await using var context = await CreateContextAsync();
        await context.Database.EnsureCreatedAsync();
        if (await context.Set<RepositoryEntity>().AnyAsync(repository => repository.Id == _repositoryId))
        {
            return;
        }

        foreach (var host in new[] { Primary, EncryptedA, EncryptedB, ReadReplica, NewReplica })
        {
            context.Set<StorageNodeEntity>().Add(
                new StorageNodeEntity
                {
                    Id = FakeLfsStorageNodes.NodeId(host),
                    NodeId = host,
                    InternalHost = host,
                    InternalHttpPort = 8081,
                    IsHealthy = true,
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
                StorageNodeId = FakeLfsStorageNodes.NodeId(Primary),
                PrimaryStorageNodeId = FakeLfsStorageNodes.NodeId(Primary),
                ReplicationState = ReplicationState.Rf4Healthy,
                Replicas = _replicas
                    .Select(replica => new RepositoryReplicaEntity
                    {
                        RepositoryId = _repositoryId,
                        StorageNodeId = FakeLfsStorageNodes.NodeId(replica.Key),
                        Role = replica.Value,
                    })
                    .ToList(),
            }
        );
        await context.SaveChangesAsync();
    }

    private async Task AddReplicaAsync(string host, RepositoryReplicaRole role)
    {
        await using var context = await CreateContextAsync();
        context.Set<RepositoryReplicaEntity>().Add(
            new RepositoryReplicaEntity
            {
                RepositoryId = _repositoryId,
                StorageNodeId = FakeLfsStorageNodes.NodeId(host),
                Role = role,
            }
        );
        await context.SaveChangesAsync();
    }

    private async Task<LfsObjectCopyEntity?> FindCopyAsync(string oid, string host)
    {
        await using var context = await CreateContextAsync();
        var nodeId = FakeLfsStorageNodes.NodeId(host);
        return await context
            .Set<LfsObjectCopyEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(copy => copy.RepositoryId == _repositoryId && copy.Oid == oid && copy.StorageNodeId == nodeId);
    }

    private Task<OpenGitBaseDbContext> CreateContextAsync() =>
        _provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>().CreateDbContextAsync();

    private async Task<byte[]> EncryptAsync(byte[] content, string oid)
    {
        using var output = new MemoryStream();
        await LfsObjectCipher.EncryptAsync(
            new MemoryStream(content),
            content.Length,
            output,
            new LfsCipherContext(_key, KeyVersion, _repositoryId, oid),
            _options.SegmentBytes,
            CancellationToken.None
        );
        return output.ToArray();
    }

    private async Task<byte[]> DecryptAsync(byte[] ciphertext, string oid)
    {
        using var output = new MemoryStream();
        await LfsObjectCipher.DecryptAsync(
            new MemoryStream(ciphertext),
            output,
            new LfsCipherContext(_key, KeyVersion, _repositoryId, oid),
            CancellationToken.None
        );
        return output.ToArray();
    }
}
