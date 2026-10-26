using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenGitBase.Api.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Tests.Services.Lfs;

/// <summary>The web UI reads LFS objects from the plaintext nodes like git-lfs downloads do (lfs-07).</summary>
public sealed class LfsContentReaderTests
{
    private const string Primary = "storage-1";
    private const string ReadReplica = "storage-4";
    private const string Down = "storage-5";

    private readonly IQueryProcessor _queryProcessor = Substitute.For<IQueryProcessor>();
    private readonly FakeLfsStorageNodes _nodes = new();
    private readonly RecordingLfsRepairQueue _repairs = new();
    private readonly Guid _repositoryId = Guid.NewGuid();
    private readonly List<RepositoryRoutingTargetDto> _targets =
    [
        Target(Primary, isPrimary: true),
        Target(ReadReplica, isPrimary: false),
    ];

    public LfsContentReaderTests()
    {
        _queryProcessor
            .RunQueryAsync(Arg.Any<RepositoryReplicationRoutingQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => Option.From(new RepositoryReplicationRoutingDto { WriteQuorumAvailable = true, Targets = [.. _targets] }));
        _queryProcessor
            .RunQueryAsync(Arg.Any<GetStorageNodeApiTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From("node-token"));
    }

    [Fact]
    public async Task OpenAsync_ReadsFromTheReadReplicaBeforeThePrimary()
    {
        var (oid, content) = Store(Primary, ReadReplica);

        await using var stream = await CreateReader().OpenAsync(_repositoryId, oid, CancellationToken.None);

        Assert.Equal(content, await ReadAllAsync(stream));
        Assert.All(_nodes.Requests, request => Assert.Equal(ReadReplica, request.Uri.Host));
    }

    [Fact]
    public async Task OpenAsync_FallsBackToThePrimaryWhenTheReadReplicaLacksTheObject()
    {
        var (oid, content) = Store(Primary);

        await using var stream = await CreateReader().OpenAsync(_repositoryId, oid, CancellationToken.None);

        Assert.Equal(content, await ReadAllAsync(stream));
        Assert.Empty(_repairs.Scheduled);
    }

    [Fact]
    public async Task OpenAsync_SkipsANodeThatFailsAndOneThatIsUnhealthy()
    {
        var (oid, content) = Store(Primary);
        _targets.Insert(0, Target(Down, isPrimary: false, isHealthy: false));
        _nodes.FailingHosts.Add(ReadReplica);

        await using var stream = await CreateReader().OpenAsync(_repositoryId, oid, CancellationToken.None);

        Assert.Equal(content, await ReadAllAsync(stream));
        Assert.DoesNotContain(_nodes.Requests, request => request.Uri.Host == Down);
    }

    [Fact]
    public async Task OpenAsync_AsksANodeListedAsPrimaryAndReadReplicaOnce()
    {
        _targets.Clear();
        _targets.Add(Target(Primary, isPrimary: true));
        _targets.Add(Target(Primary, isPrimary: false));
        var oid = Convert.ToHexStringLower(SHA256.HashData("never stored"u8));

        var stream = await CreateReader().OpenAsync(_repositoryId, oid, CancellationToken.None);

        Assert.Null(stream);
        Assert.Single(_nodes.Requests);
    }

    [Fact]
    public async Task OpenAsync_WhenNoNodeHoldsTheObject_QueuesARepair()
    {
        var oid = Convert.ToHexStringLower(SHA256.HashData("lost"u8));

        var stream = await CreateReader().OpenAsync(_repositoryId, oid, CancellationToken.None);

        Assert.Null(stream);
        Assert.Equal([_repositoryId], _repairs.Scheduled);
    }

    [Fact]
    public async Task FindRecordedSizeAsync_ReturnsTheSizeOfARecordedObjectAndNullOtherwise()
    {
        const string recorded = "aa";
        _queryProcessor
            .RunQueryAsync(Arg.Is<FindLfsObjectsQuery>(query => query.Oids.Contains(recorded)), Arg.Any<CancellationToken>())
            .Returns(Option.From<IReadOnlyList<LfsObjectDto>>([new LfsObjectDto { Oid = recorded, Size = 42 }]));
        _queryProcessor
            .RunQueryAsync(Arg.Is<FindLfsObjectsQuery>(query => !query.Oids.Contains(recorded)), Arg.Any<CancellationToken>())
            .Returns(Option.From<IReadOnlyList<LfsObjectDto>>([]));
        var reader = CreateReader();

        Assert.Equal(42, await reader.FindRecordedSizeAsync(_repositoryId, recorded, CancellationToken.None));
        Assert.Null(await reader.FindRecordedSizeAsync(_repositoryId, "bb", CancellationToken.None));
    }

    private static RepositoryRoutingTargetDto Target(string host, bool isPrimary, bool isHealthy = true) =>
        new()
        {
            StorageNodeId = FakeLfsStorageNodes.NodeId(host),
            InternalHost = host,
            InternalHttpPort = 8081,
            IsHealthy = isHealthy,
            IsPrimary = isPrimary,
            IsInSync = true,
        };

    private static async Task<byte[]> ReadAllAsync(Stream? stream)
    {
        Assert.NotNull(stream);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private (string Oid, byte[] Content) Store(params string[] hosts)
    {
        var content = RandomNumberGenerator.GetBytes(2048);
        var oid = Convert.ToHexStringLower(SHA256.HashData(content));
        foreach (var host in hosts)
        {
            _nodes.Store(host, _repositoryId, oid, LfsObjectCopyKind.Plaintext, content);
        }

        return (oid, content);
    }

    private LfsContentReader CreateReader() =>
        new(_queryProcessor, _nodes.CreateClient(), _repairs, NullLogger<LfsContentReader>.Instance);
}
