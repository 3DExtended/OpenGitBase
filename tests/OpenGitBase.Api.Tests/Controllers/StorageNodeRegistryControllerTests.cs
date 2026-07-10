using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Models;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Tests.Controllers;

public class StorageNodeRegistryControllerTests
{
    [Fact]
    public async Task Register_WhenSuccessful_ReturnsToken()
    {
        var storageNodeId = StorageNodeId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<RegisterStorageNodeQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RegisterStorageNodeResult
                    {
                        StorageNodeId = storageNodeId,
                        ApiToken = "token",
                        HeartbeatIntervalSeconds = 30,
                    }
                )
            );

        using var certificate = CreateSelfSignedCertificate();
        var httpContext = new DefaultHttpContext
        {
            Connection =
            {
                ClientCertificate = certificate,
            },
        };
        var controller = new StorageNodeRegistryController(queryProcessor)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext,
            },
        };

        var result = await controller.Register(
            new RegisterStorageNodeRequest
            {
                NodeId = "storage-1",
                InternalHost = "storage-1",
                InternalHttpPort = 8081,
            },
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RegisterStorageNodeResponse>(ok.Value);
        Assert.Equal(storageNodeId.Value, response.StorageNodeId);
        Assert.Equal("token", response.ApiToken);
    }

    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=storage-test",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }
}
