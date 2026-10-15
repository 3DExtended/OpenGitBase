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

namespace OpenGitBase.Api.Tests.Controllers;

public class LfsInternalControllerTests
{
    private const string Oid = "1111111111111111111111111111111111111111111111111111111111111111";

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

    private static LfsInternalController CreateController(bool enabled, string traceIdentifier = "trace")
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
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
            new LfsActionTokenService(new JwtOptions(), lfsOptions, new SystemClock()),
            Substitute.For<IStorageLfsClient>(),
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
