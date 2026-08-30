using NSubstitute;
using OpenGitBase.Api.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Tests.Services;

public class RelayReplicationArtifactTests
{
    [Fact]
    public async Task Relays_ArtifactToEncryptedReplica_UsingTargetNodeToken()
    {
        var repositoryId = Guid.NewGuid();
        var targetNodeId = Guid.NewGuid();
        var (handler, client) = Build(
            Context(repositoryId, targetNodeId, isPrimary: true),
            targetNodeId,
            targetToken: "target-node-token",
            StorageProvisionerResult.Ok(201)
        );

        var result = await handler.RunQueryAsync(
            Query(repositoryId, targetNodeId),
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.True(result.Get().Success);
        await client
            .Received(1)
            .UploadReplicationArtifactAsync(
                Arg.Is<StorageNodeDto>(node => node.Id == StorageNodeId.From(targetNodeId)),
                "target-node-token",
                repositoryId,
                7L,
                Arg.Any<string>(),
                Arg.Is<byte[]>(bytes => bytes.Length == 4),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Rejects_WhenCallerIsNotPrimary()
    {
        var repositoryId = Guid.NewGuid();
        var targetNodeId = Guid.NewGuid();
        var (handler, client) = Build(
            Context(repositoryId, targetNodeId, isPrimary: false),
            targetNodeId,
            targetToken: "target-node-token",
            StorageProvisionerResult.Ok(201)
        );

        var result = await handler.RunQueryAsync(
            Query(repositoryId, targetNodeId),
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.False(result.Get().Success);
        Assert.Equal(403, result.Get().StatusCode);
        await client
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
    public async Task Rejects_WhenTargetIsNotHealthyEncryptedReplica()
    {
        var repositoryId = Guid.NewGuid();
        var targetNodeId = Guid.NewGuid();
        var (handler, _) = Build(
            Context(repositoryId, targetNodeId, isPrimary: true, targetHealthy: false),
            targetNodeId,
            targetToken: "target-node-token",
            StorageProvisionerResult.Ok(201)
        );

        var result = await handler.RunQueryAsync(
            Query(repositoryId, targetNodeId),
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.False(result.Get().Success);
        Assert.Equal(409, result.Get().StatusCode);
    }

    [Fact]
    public async Task Surfaces_UploadFailureStatus()
    {
        var repositoryId = Guid.NewGuid();
        var targetNodeId = Guid.NewGuid();
        var (handler, _) = Build(
            Context(repositoryId, targetNodeId, isPrimary: true),
            targetNodeId,
            targetToken: "target-node-token",
            StorageProvisionerResult.Fail(507, "insufficient storage")
        );

        var result = await handler.RunQueryAsync(
            Query(repositoryId, targetNodeId),
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.False(result.Get().Success);
        Assert.Equal(507, result.Get().StatusCode);
    }

    private static RepositoryReplicationContextDto Context(
        Guid repositoryId,
        Guid targetNodeId,
        bool isPrimary,
        bool targetHealthy = true,
        string targetRole = nameof(RepositoryReplicaRole.EncryptedReplica)
    ) =>
        new()
        {
            RepositoryId = repositoryId,
            IsPrimary = isPrimary,
            ReplicationEpoch = 3,
            PrimaryWatermark = 6,
            ReplicationState = nameof(ReplicationState.Rf4Healthy),
            Peers =
            [
                new RepositoryReplicationPeerDto
                {
                    StorageNodeId = targetNodeId,
                    InternalHost = "storage-3",
                    InternalHttpPort = 8081,
                    Role = targetRole,
                    IsHealthy = targetHealthy,
                },
            ],
        };

    private static (RelayReplicationArtifactQueryHandler Handler, IStorageProvisionerClient Client) Build(
        RepositoryReplicationContextDto context,
        Guid targetNodeId,
        string? targetToken,
        StorageProvisionerResult uploadResult
    )
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(
                Arg.Any<GetRepositoryReplicationContextQuery>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Option.From(context));
        queryProcessor
            .RunQueryAsync(Arg.Any<GetStorageNodeQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new StorageNodeDto
                    {
                        Id = StorageNodeId.From(targetNodeId),
                        NodeId = "storage-3",
                        InternalHost = "storage-3",
                        InternalHttpPort = 8081,
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetStorageNodeApiTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(targetToken is null ? Option<string>.None : Option.From(targetToken));

        var client = Substitute.For<IStorageProvisionerClient>();
        client
            .UploadReplicationArtifactAsync(
                Arg.Any<StorageNodeDto>(),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(uploadResult);

        return (new RelayReplicationArtifactQueryHandler(queryProcessor, client), client);
    }

    private static RelayReplicationArtifactQuery Query(Guid repositoryId, Guid targetNodeId) =>
        new()
        {
            RepositoryId = RepositoryId.From(repositoryId),
            StorageNodeId = StorageNodeId.From(Guid.NewGuid()),
            TargetStorageNodeId = targetNodeId,
            Watermark = 7,
            ManifestJson = "{\"watermark\":7}",
            BundlePayload = [1, 2, 3, 4],
        };
}
