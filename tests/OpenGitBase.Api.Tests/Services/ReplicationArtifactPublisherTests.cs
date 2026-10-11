using Microsoft.Extensions.Options;
using NSubstitute;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Tests.Services;

public class ReplicationArtifactPublisherTests
{
    private static readonly StorageNodeDto Node = new()
    {
        Id = StorageNodeId.From(Guid.NewGuid()),
        NodeId = "storage-3",
        InternalHost = "storage-3",
        InternalHttpPort = 8081,
        IsHealthy = true,
    };

    [Fact]
    public async Task UploadAsync_AReplicaThatDoesNotAnswerInTime_CountsAsRefused()
    {
        // Accepts the connection, never answers: without a time box the push (under HAProxy's
        // 5 minutes) or the reconciler pass (sharing a loop with failover) waits for HttpClient's
        // 5-minute timeout.
        var provisioner = Substitute.For<IStorageProvisionerClient>();
        provisioner
            .UploadReplicationArtifactAsync(
                Arg.Any<StorageNodeDto>(),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(async callInfo =>
            {
                await Task.Delay(Timeout.Infinite, callInfo.Arg<CancellationToken>());
                return StorageProvisionerResult.Ok(201);
            });
        var publisher = CreatePublisher(provisioner, uploadTimeoutSeconds: 1);

        var result = await publisher
            .UploadAsync(Node, Guid.NewGuid(), 6, ReplicationArtifactFetchResult.Ok("{}", [1]), CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(15));

        Assert.False(result.Success);
        Assert.Equal(504, result.StatusCode);
        Assert.Contains("storage-3", result.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadAsync_WhenTheCallerCancels_DoesNotReportARefusal()
    {
        var provisioner = Substitute.For<IStorageProvisionerClient>();
        provisioner
            .UploadReplicationArtifactAsync(
                Arg.Any<StorageNodeDto>(),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(async callInfo =>
            {
                await Task.Delay(Timeout.Infinite, callInfo.Arg<CancellationToken>());
                return StorageProvisionerResult.Ok(201);
            });
        var publisher = CreatePublisher(provisioner, uploadTimeoutSeconds: 60);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            publisher.UploadAsync(Node, Guid.NewGuid(), 6, ReplicationArtifactFetchResult.Ok("{}", [1]), cancellation.Token)
        );
    }

    private static ReplicationArtifactPublisher CreatePublisher(
        IStorageProvisionerClient provisioner,
        int uploadTimeoutSeconds
    )
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetStorageNodeApiTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From("token"));
        return new ReplicationArtifactPublisher(
            queryProcessor,
            provisioner,
            Substitute.For<IRepositoryKeyService>(),
            Options.Create(new HaStorageBackgroundOptions { ArtifactUploadTimeoutSeconds = uploadTimeoutSeconds })
        );
    }
}
