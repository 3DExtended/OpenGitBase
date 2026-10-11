using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Tests.Controllers;

public class StorageReplicationControllerTests
{
    [Fact]
    public async Task QuorumReplicate_WhenUnauthorized_ReturnsUnauthorized()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        var repositoryKeyService = Substitute.For<IRepositoryKeyService>();
        var controller = new StorageReplicationController(queryProcessor, repositoryKeyService)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = await controller.QuorumReplicate(
            Guid.NewGuid(),
            new QuorumReplicateRepositoryRequest { AppliedWatermark = 1 },
            CancellationToken.None
        );

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task GetReplicationContext_WhenUnauthorized_ReturnsUnauthorized()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        var repositoryKeyService = Substitute.For<IRepositoryKeyService>();
        var controller = new StorageReplicationController(queryProcessor, repositoryKeyService)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = await controller.GetReplicationContext(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task QuorumReplicate_PassesCreateArtifactToTheQuery()
    {
        var nodeId = Guid.NewGuid();
        var queryProcessor = AuthenticatedProcessor(nodeId);
        QuorumReplicateRepositoryQuery? dispatched = null;
        queryProcessor
            .RunQueryAsync(Arg.Any<QuorumReplicateRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                dispatched = callInfo.Arg<QuorumReplicateRepositoryQuery>();
                return Option.From(QuorumReplicateRepositoryResult.Replicated(6));
            });
        var controller = AuthenticatedController(queryProcessor);

        var result = await controller.QuorumReplicate(
            Guid.NewGuid(),
            new QuorumReplicateRepositoryRequest { AppliedWatermark = 6, CreateArtifact = true },
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(6, Assert.IsType<QuorumReplicateRepositoryResponse>(ok.Value).PrimaryWatermark);
        Assert.NotNull(dispatched);
        Assert.True(dispatched!.CreateArtifact);
        Assert.Equal(6, dispatched.AppliedWatermark);
        Assert.Equal(nodeId, dispatched.StorageNodeId.Value);
        Assert.Null(dispatched.ExpectedPrimaryLastSyncedAt);
    }

    [Fact]
    public async Task QuorumReplicate_WithoutCreateArtifact_KeepsTheRelayRequest()
    {
        var queryProcessor = AuthenticatedProcessor(Guid.NewGuid());
        QuorumReplicateRepositoryQuery? dispatched = null;
        queryProcessor
            .RunQueryAsync(Arg.Any<QuorumReplicateRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                dispatched = callInfo.Arg<QuorumReplicateRepositoryQuery>();
                return Option.From(QuorumReplicateRepositoryResult.Replicated(6));
            });
        var confirmed = Guid.NewGuid();
        var controller = AuthenticatedController(queryProcessor);

        await controller.QuorumReplicate(
            Guid.NewGuid(),
            new QuorumReplicateRepositoryRequest { AppliedWatermark = 6, ConfirmedEncryptedNodeIds = [confirmed] },
            CancellationToken.None
        );

        Assert.NotNull(dispatched);
        Assert.False(dispatched!.CreateArtifact);
        Assert.Equal([confirmed], dispatched.ConfirmedEncryptedNodeIds);
    }

    [Fact]
    public async Task QuorumReplicate_WhenStale_ReturnsConflictMarkedStale()
    {
        var queryProcessor = AuthenticatedProcessor(Guid.NewGuid());
        queryProcessor
            .RunQueryAsync(Arg.Any<QuorumReplicateRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(QuorumReplicateRepositoryResult.StaleWatermark("Watermark 6 is already committed.")));
        var controller = AuthenticatedController(queryProcessor);

        var result = await controller.QuorumReplicate(
            Guid.NewGuid(),
            new QuorumReplicateRepositoryRequest { AppliedWatermark = 6, CreateArtifact = true },
            CancellationToken.None
        );

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var body = Assert.IsType<QuorumReplicateRepositoryResponse>(conflict.Value);
        Assert.False(body.Success);
        Assert.True(body.Stale);
        Assert.Equal("Watermark 6 is already committed.", body.Error);
    }

    private static IQueryProcessor AuthenticatedProcessor(Guid nodeId)
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<VerifyStorageNodeTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(StorageNodeId.From(nodeId)));
        return queryProcessor;
    }

    private static StorageReplicationController AuthenticatedController(IQueryProcessor queryProcessor)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.5");
        httpContext.Request.Headers.Authorization = "Bearer node-token";
        httpContext.Request.Headers["X-Storage-Node-Id"] = "storage-1";
        httpContext.Request.Headers[StorageNodeCertificateHeaderReader.ThumbprintHeaderName] = new string('A', 64);
        return new StorageReplicationController(queryProcessor, Substitute.For<IRepositoryKeyService>())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
    }
}
