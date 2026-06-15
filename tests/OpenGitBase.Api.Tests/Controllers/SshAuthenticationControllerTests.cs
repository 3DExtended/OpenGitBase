using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Models;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.PublicGitSshKey.Contracts;

namespace OpenGitBase.Api.Tests.Controllers;

public class SshAuthenticationControllerTests
{
    private const string SamplePublicKey = "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABgQC7";
    private const string SampleFingerprint = "SHA256:sample-fingerprint";

    [Fact]
    public async Task GetByFingerprint_WhenFingerprintMissing_ReturnsNotFound()
    {
        var controller = CreateController();

        var result = await controller.GetByFingerprint(string.Empty, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetByFingerprint_WhenFingerprintUnknown_ReturnsNotFound()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(
                Arg.Any<GetPublicGitSshKeyByFingerprintQuery>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Option<PublicGitSshKeyDto>.None);

        var controller = CreateController(queryProcessor);

        var result = await controller.GetByFingerprint(
            SampleFingerprint,
            CancellationToken.None
        );

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetByFingerprint_WhenFingerprintKnown_ReturnsAuthorizedKeysLine()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(
                Arg.Any<GetPublicGitSshKeyByFingerprintQuery>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Option.From(
                    new PublicGitSshKeyDto
                    {
                        PublicSSHKey = SamplePublicKey,
                        Fingerprint = SampleFingerprint,
                        Name = "Laptop",
                    }
                )
            );

        var controller = CreateController(queryProcessor);

        var result = await controller.GetByFingerprint(
            SampleFingerprint,
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SshAuthenticationResponse>(ok.Value);
        Assert.Equal(SampleFingerprint, response.Fingerprint);
        Assert.Equal(SamplePublicKey, response.PublicSshKey);
        Assert.Equal(
            SshAuthorizedKeysLineBuilder.Build(SampleFingerprint, SamplePublicKey),
            response.AuthorizedKeysLine
        );
        Assert.DoesNotContain("environment=\"SSH_PUBLIC_KEY=", response.AuthorizedKeysLine);
    }

    private static SshAuthenticationController CreateController(
        IQueryProcessor? queryProcessor = null
    )
    {
        queryProcessor ??= Substitute.For<IQueryProcessor>();
        return new SshAuthenticationController(
            queryProcessor,
            NullLogger<SshAuthenticationController>.Instance
        );
    }
}
