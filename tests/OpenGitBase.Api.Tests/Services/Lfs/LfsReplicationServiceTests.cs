using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Tests.Services.Lfs;

public class LfsReplicationServiceTests
{
    private const string Primary = "storage-1";
    private const string EncryptedA = "storage-2";
    private const string EncryptedB = "storage-3";
    private const string ReadReplica = "storage-4";
    private const int KeyVersion = 1;

    private readonly IQueryProcessor _queryProcessor = Substitute.For<IQueryProcessor>();
    private readonly IRepositoryKeyService _keys = Substitute.For<IRepositoryKeyService>();
    private readonly FakeLfsStorageNodes _nodes = new();
    private readonly LfsOptions _options = new() { SegmentBytes = 1024, ReplicaUploadTimeoutSeconds = 600 };
    private readonly Guid _repositoryId = Guid.NewGuid();
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);
    private readonly byte[] _content;
    private readonly string _oid;
    private readonly Dictionary<string, (string Role, bool Healthy)> _replicas = new(StringComparer.Ordinal)
    {
        [Primary] = ("Primary", true),
        [ReadReplica] = ("ReadReplica", true),
        [EncryptedA] = ("EncryptedReplica", true),
        [EncryptedB] = ("EncryptedReplica", true),
    };

    public LfsReplicationServiceTests()
    {
        _content = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("PLAINTEXT-MARKER-", 300)));
        _oid = Convert.ToHexStringLower(SHA256.HashData(_content));
        _nodes.Store(Primary, _repositoryId, _oid, LfsObjectCopyKind.Plaintext, _content);
        _keys
            .TryGetRepositoryKeyAsync(_repositoryId, Arg.Any<CancellationToken>())
            .Returns(_ => new EphemeralRepositoryKey((byte[])_key.Clone(), KeyVersion));
        _queryProcessor
            .RunQueryAsync(Arg.Any<GetStorageNodeApiTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From("node-token"));
        _queryProcessor
            .RunQueryAsync(Arg.Any<RecordLfsObjectCopyQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(new RecordLfsObjectResult { Created = true }));
        _queryProcessor
            .RunQueryAsync(Arg.Any<RepositoryReplicationRoutingQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => Option.From(Routing()));
        _queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryReplicationContextQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => Option.From(Context()));
    }

    [Fact]
    public async Task ResolveReplicaSetAsync_ReturnsThePrimaryThePlaintextReplicaAndTheHealthyEncryptedReplicas()
    {
        var replicas = await CreateService().ResolveReplicaSetAsync(_repositoryId, CancellationToken.None);

        Assert.NotNull(replicas);
        Assert.Equal(Primary, replicas.Primary.InternalHost);
        Assert.Equal([ReadReplica], replicas.PlaintextReplicas.Select(node => node.InternalHost));
        Assert.Equal(
            [EncryptedA, EncryptedB],
            replicas.EncryptedReplicas.Select(node => node.InternalHost).Order(StringComparer.Ordinal)
        );
        Assert.All(replicas.EncryptedReplicas, node => Assert.Equal("node-token", node.ApiToken));
    }

    [Fact]
    public async Task ResolveReplicaSetAsync_LeavesOutUnhealthyEncryptedReplicas()
    {
        _replicas[EncryptedA] = ("EncryptedReplica", false);

        var replicas = await CreateService().ResolveReplicaSetAsync(_repositoryId, CancellationToken.None);

        Assert.Equal([EncryptedB], replicas!.EncryptedReplicas.Select(node => node.InternalHost));
    }

    [Fact]
    public async Task ResolveReplicaSetAsync_WhenThePrimaryIsUnhealthy_ReturnsNull()
    {
        _replicas[Primary] = ("Primary", false);

        Assert.Null(await CreateService().ResolveReplicaSetAsync(_repositoryId, CancellationToken.None));
    }

    [Fact]
    public async Task ReachQuorumAsync_StoresCiphertextThatDecryptsToTheObjectAndHoldsNoPlaintext()
    {
        var service = CreateService();
        var replicas = await service.ResolveReplicaSetAsync(_repositoryId, CancellationToken.None);

        var result = await service.ReachQuorumAsync(replicas!, _oid, _content.Length, CancellationToken.None);
        await service.CompleteCopiesAsync(result.Pending!, CancellationToken.None);

        Assert.Equal(LfsQuorumStatus.Reached, result.Status);
        Assert.Equal(KeyVersion, result.KeyVersion);
        foreach (var host in new[] { EncryptedA, EncryptedB })
        {
            var ciphertext = _nodes.Find(host, _repositoryId, _oid, LfsObjectCopyKind.Encrypted);
            Assert.NotNull(ciphertext);
            Assert.Equal(-1, ciphertext.AsSpan().IndexOf("PLAINTEXT-MARKER"u8));
            Assert.Equal(_content, await DecryptAsync(ciphertext));
            Assert.Null(_nodes.Find(host, _repositoryId, _oid, LfsObjectCopyKind.Plaintext));
        }
    }

    [Fact]
    public async Task ReachQuorumAsync_RecordsThePrimaryAndTheFirstConfirmedEncryptedReplicaAsQuorumCopies()
    {
        _nodes.HangingHosts.Add(EncryptedA);
        var service = CreateService();
        var replicas = await service.ResolveReplicaSetAsync(_repositoryId, CancellationToken.None);

        var result = await service.ReachQuorumAsync(replicas!, _oid, _content.Length, CancellationToken.None);

        Assert.Equal(
            [(FakeLfsStorageNodes.NodeId(Primary), LfsObjectCopyKind.Plaintext), (FakeLfsStorageNodes.NodeId(EncryptedB), LfsObjectCopyKind.Encrypted)],
            result.Copies.Select(copy => (copy.StorageNodeId, copy.Kind))
        );
    }

    [Fact]
    public async Task ReachQuorumAsync_WhenOneEncryptedReplicaHangs_DoesNotWaitForIt()
    {
        _nodes.HangingHosts.Add(EncryptedA);
        var service = CreateService();
        var replicas = await service.ResolveReplicaSetAsync(_repositoryId, CancellationToken.None);
        var stopwatch = Stopwatch.StartNew();

        var result = await service.ReachQuorumAsync(replicas!, _oid, _content.Length, CancellationToken.None);

        Assert.Equal(LfsQuorumStatus.Reached, result.Status);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"Quorum took {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task ReachQuorumAsync_WhenEveryEncryptedReplicaHangs_GivesUpAfterTheUploadTimeout()
    {
        _options.ReplicaUploadTimeoutSeconds = 1;
        _nodes.HangingHosts.Add(EncryptedA);
        _nodes.HangingHosts.Add(EncryptedB);
        var service = CreateService();
        var replicas = await service.ResolveReplicaSetAsync(_repositoryId, CancellationToken.None);
        var stopwatch = Stopwatch.StartNew();

        var result = await service.ReachQuorumAsync(replicas!, _oid, _content.Length, CancellationToken.None);

        Assert.Equal(LfsQuorumStatus.NotReached, result.Status);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"Giving up took {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task ReachQuorumAsync_WhenEveryEncryptedReplicaFails_IsNotReached()
    {
        _nodes.FailingHosts.Add(EncryptedA);
        _nodes.FailingHosts.Add(EncryptedB);
        var service = CreateService();
        var replicas = await service.ResolveReplicaSetAsync(_repositoryId, CancellationToken.None);

        var result = await service.ReachQuorumAsync(replicas!, _oid, _content.Length, CancellationToken.None);

        Assert.Equal(LfsQuorumStatus.NotReached, result.Status);
        Assert.Empty(result.Copies);
    }

    [Fact]
    public async Task ReachQuorumAsync_WhenThePrimaryCopyDoesNotMatchTheOid_IsNotReached()
    {
        var corrupted = (byte[])_content.Clone();
        corrupted[^1] ^= 0xFF;
        _nodes.Store(Primary, _repositoryId, _oid, LfsObjectCopyKind.Plaintext, corrupted);
        var service = CreateService();
        var replicas = await service.ResolveReplicaSetAsync(_repositoryId, CancellationToken.None);

        var result = await service.ReachQuorumAsync(replicas!, _oid, _content.Length, CancellationToken.None);

        Assert.Equal(LfsQuorumStatus.NotReached, result.Status);
    }

    [Fact]
    public async Task ReachQuorumAsync_WithoutARepositoryKey_DoesNotUpload()
    {
        _keys
            .TryGetRepositoryKeyAsync(_repositoryId, Arg.Any<CancellationToken>())
            .Returns((EphemeralRepositoryKey?)null);
        var service = CreateService();
        var replicas = await service.ResolveReplicaSetAsync(_repositoryId, CancellationToken.None);

        var result = await service.ReachQuorumAsync(replicas!, _oid, _content.Length, CancellationToken.None);

        Assert.Equal(LfsQuorumStatus.KeyUnavailable, result.Status);
        Assert.DoesNotContain(_nodes.Requests, request => request.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task ReplicatingAnObject_NeverSendsTheRepositoryKeyToStorage()
    {
        _nodes.FailingHosts.Add(EncryptedB);
        var service = CreateService();
        var replicas = await service.ResolveReplicaSetAsync(_repositoryId, CancellationToken.None);

        var result = await service.ReachQuorumAsync(replicas!, _oid, _content.Length, CancellationToken.None);
        _nodes.FailingHosts.Clear();
        await service.CompleteCopiesAsync(result.Pending!, CancellationToken.None);

        Assert.Contains(_nodes.Requests, request => request.Method == HttpMethod.Put && request.Uri.Host == EncryptedB);
        var forms = new[]
        {
            Convert.ToHexStringLower(_key),
            Convert.ToHexString(_key),
            Convert.ToBase64String(_key),
            Convert.ToBase64String(_key).TrimEnd('=').Replace('+', '-').Replace('/', '_'),
        };
        foreach (var request in _nodes.Requests)
        {
            Assert.Equal(-1, request.Body.AsSpan().IndexOf(_key));
            var text = request.Uri + request.Headers + Encoding.Latin1.GetString(request.Body);
            Assert.All(forms, form => Assert.DoesNotContain(form, text, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task CompleteCopiesAsync_AddsTheSecondEncryptedCopyAndThePlaintextReplicaCopy()
    {
        var service = CreateService();
        var replicas = await service.ResolveReplicaSetAsync(_repositoryId, CancellationToken.None);
        var result = await service.ReachQuorumAsync(replicas!, _oid, _content.Length, CancellationToken.None);
        var quorumEncrypted = result.Copies.Single(copy => copy.Kind == LfsObjectCopyKind.Encrypted).StorageNodeId;
        var otherEncrypted = new[] { EncryptedA, EncryptedB }
            .Select(FakeLfsStorageNodes.NodeId)
            .Single(id => id != quorumEncrypted);

        await service.CompleteCopiesAsync(result.Pending!, CancellationToken.None);

        Assert.Equal(_content, _nodes.Find(ReadReplica, _repositoryId, _oid, LfsObjectCopyKind.Plaintext));
        Assert.Equal(
            [(otherEncrypted, LfsObjectCopyKind.Encrypted), (FakeLfsStorageNodes.NodeId(ReadReplica), LfsObjectCopyKind.Plaintext)],
            RecordedCopies().OrderBy(copy => copy.Kind == LfsObjectCopyKind.Plaintext)
        );
    }

    [Fact]
    public async Task CompleteCopiesAsync_RetriesAnEncryptedCopyThatFailedDuringQuorum()
    {
        _nodes.FailingHosts.Add(EncryptedB);
        var service = CreateService();
        var replicas = await service.ResolveReplicaSetAsync(_repositoryId, CancellationToken.None);
        var result = await service.ReachQuorumAsync(replicas!, _oid, _content.Length, CancellationToken.None);
        _nodes.FailingHosts.Clear();

        await service.CompleteCopiesAsync(result.Pending!, CancellationToken.None);

        var ciphertext = _nodes.Find(EncryptedB, _repositoryId, _oid, LfsObjectCopyKind.Encrypted);
        Assert.Equal(_content, await DecryptAsync(ciphertext!));
        Assert.Contains((FakeLfsStorageNodes.NodeId(EncryptedB), LfsObjectCopyKind.Encrypted), RecordedCopies());
    }

    [Fact]
    public async Task CompleteCopiesAsync_WhenAnEncryptedReplicaStaysDown_RecordsOnlyWhatExists()
    {
        _nodes.FailingHosts.Add(EncryptedB);
        var service = CreateService();
        var replicas = await service.ResolveReplicaSetAsync(_repositoryId, CancellationToken.None);
        var result = await service.ReachQuorumAsync(replicas!, _oid, _content.Length, CancellationToken.None);

        await service.CompleteCopiesAsync(result.Pending!, CancellationToken.None);

        Assert.DoesNotContain(FakeLfsStorageNodes.NodeId(EncryptedB), RecordedCopies().Select(copy => copy.StorageNodeId));
        Assert.Contains((FakeLfsStorageNodes.NodeId(ReadReplica), LfsObjectCopyKind.Plaintext), RecordedCopies());
    }

    [Fact]
    public async Task CompleteCopiesAsync_WhenTheReadRoleSitsOnThePrimaryNode_MakesNoSecondPlaintextCopy()
    {
        _replicas.Remove(ReadReplica);
        var service = CreateService();
        var replicas = await service.ResolveReplicaSetAsync(_repositoryId, CancellationToken.None);
        var result = await service.ReachQuorumAsync(replicas!, _oid, _content.Length, CancellationToken.None);

        await service.CompleteCopiesAsync(result.Pending!, CancellationToken.None);

        Assert.Empty(replicas!.PlaintextReplicas);
        Assert.DoesNotContain(RecordedCopies(), copy => copy.Kind == LfsObjectCopyKind.Plaintext);
    }

    private static RepositoryRoutingTargetDto Target(string host, string role, bool healthy) =>
        new()
        {
            StorageNodeId = FakeLfsStorageNodes.NodeId(host),
            InternalHost = host,
            InternalSshPort = 22,
            InternalGitHttpPort = 8082,
            InternalHttpPort = 8081,
            Role = role,
            IsHealthy = healthy,
            IsInSync = healthy,
            IsPrimary = role == "Primary",
        };

    private RepositoryReplicationRoutingDto Routing() =>
        new()
        {
            ReplicationEpoch = 1,
            WriteQuorumAvailable = true,
            Targets = _replicas
                .Where(replica => replica.Value.Role != "EncryptedReplica")
                .Select(replica => Target(replica.Key, replica.Value.Role, replica.Value.Healthy))
                .ToList(),
        };

    private RepositoryReplicationContextDto Context() =>
        new()
        {
            RepositoryId = _repositoryId,
            IsPrimary = true,
            Peers = _replicas
                .Select(replica => new RepositoryReplicationPeerDto
                {
                    StorageNodeId = FakeLfsStorageNodes.NodeId(replica.Key),
                    InternalHost = replica.Key,
                    InternalHttpPort = 8081,
                    Role = replica.Value.Role,
                    IsHealthy = replica.Value.Healthy,
                })
                .ToList(),
        };

    private LfsReplicationService CreateService() =>
        new(_queryProcessor, _nodes.CreateClient(), _keys, _options, NullLogger<LfsReplicationService>.Instance);

    private List<(Guid StorageNodeId, LfsObjectCopyKind Kind)> RecordedCopies() =>
        _queryProcessor
            .ReceivedCalls()
            .Select(call => call.GetArguments().FirstOrDefault())
            .OfType<RecordLfsObjectCopyQuery>()
            .Select(query =>
            {
                Assert.Equal(_repositoryId, query.RepositoryId.Value);
                Assert.Equal(_oid, query.Oid);
                return (query.StorageNodeId, query.Kind);
            })
            .ToList();

    private async Task<byte[]> DecryptAsync(byte[] ciphertext)
    {
        using var output = new MemoryStream();
        await LfsObjectCipher.DecryptAsync(
            new MemoryStream(ciphertext),
            output,
            new LfsCipherContext(_key, KeyVersion, _repositoryId, _oid),
            CancellationToken.None
        );
        return output.ToArray();
    }
}
