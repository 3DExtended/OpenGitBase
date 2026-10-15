using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Tests.Controllers;

public class LfsInternalControllerTests
{
    private const string Oid = "1111111111111111111111111111111111111111111111111111111111111111";
    private static readonly JwtOptions Jwt = new() { Key = new string('k', 64) };

    [Fact]
    public async Task Batch_WhenLfsIsDisabled_Answers501WithTheRequestId()
    {
        var controller = CreateController(enabled: false, traceIdentifier: "trace-1");

        var result = await controller.Batch(
            new LfsBatchRequest { AccessToken = "pat", RepositoryPath = "alice/repo", Operation = "download" },
            CancellationToken.None
        );

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status501NotImplemented, objectResult.StatusCode);
        var error = Assert.IsType<LfsErrorBody>(objectResult.Value);
        Assert.Equal("trace-1", error.RequestId);
    }

    [Fact]
    public async Task CheckTransfer_WithAnInvalidToken_Answers401()
    {
        var controller = CreateController(enabled: true);

        var result = await controller.CheckTransfer(
            new LfsTransferCheckRequest
            {
                Token = "forged",
                Oid = Oid,
                Operation = "download",
                RepositoryPath = "alice/repo",
            },
            CancellationToken.None
        );

        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task Verify_WithAnInvalidToken_Answers401()
    {
        var controller = CreateController(enabled: true);

        var result = await controller.Verify(
            new LfsVerifyRequest
            {
                Token = "forged",
                Oid = Oid,
                Size = 1,
                RepositoryPath = "alice/repo",
            },
            CancellationToken.None
        );

        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task Verify_WhenStorageIsUnavailable_Answers503WithRetryAfter()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        var repositoryId = Guid.NewGuid();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryByOwnerSlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(new RepositoryDto { Id = RepositoryId.From(repositoryId), Slug = "repo" }));
        queryProcessor
            .RunQueryAsync(Arg.Any<FindLfsObjectsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From<IReadOnlyList<LfsObjectDto>>([]));
        queryProcessor
            .RunQueryAsync(Arg.Any<RepositoryReplicationRoutingQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryReplicationRoutingDto>.None);
        var controller = CreateController(enabled: true, queryProcessor: queryProcessor);
        var token = new LfsActionTokenService(Jwt, new LfsOptions(), new SystemClock())
            .Issue(repositoryId, Oid, 1, LfsTransferOperation.Upload, null)
            .Token;

        var result = await controller.Verify(
            new LfsVerifyRequest
            {
                Token = token,
                Oid = Oid,
                Size = 1,
                RepositoryPath = "alice/repo",
            },
            CancellationToken.None
        );

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Equal("10", controller.Response.Headers.RetryAfter.ToString());
    }

    private static LfsInternalController CreateController(
        bool enabled,
        string traceIdentifier = "trace",
        IQueryProcessor? queryProcessor = null
    )
    {
        queryProcessor ??= Substitute.For<IQueryProcessor>();
        var lfsOptions = new LfsOptions { Enabled = enabled };
        var service = new LfsService(
            lfsOptions,
            new GitOptions(),
            new RepositoryAccessCheckEvaluator(
                queryProcessor,
                Substitute.For<ISshKeyService>(),
                new GitPushEnforcementService(
                    queryProcessor,
                    Substitute.For<IStorageContentClient>(),
                    new WebReadReplicaSelector()
                ),
                NullLogger.Instance,
                new RepositoryStorageQuotaOptions(),
                new PlatformMergeIdentityOptions()
            ),
            new RepositoryStorageQuotaOptions(),
            queryProcessor,
            new LfsActionTokenService(Jwt, lfsOptions, new SystemClock()),
            Substitute.For<IStorageLfsClient>(),
            new LfsReplicationService(
                queryProcessor,
                Substitute.For<IStorageLfsClient>(),
                Substitute.For<IRepositoryKeyService>(),
                lfsOptions,
                NullLogger<LfsReplicationService>.Instance
            ),
            Substitute.For<ILfsCopyCompletionScheduler>(),
            Substitute.For<ILfsRepairQueue>(),
            NullLogger<LfsService>.Instance
        );
        return new LfsInternalController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = traceIdentifier },
            },
        };
    }
}
