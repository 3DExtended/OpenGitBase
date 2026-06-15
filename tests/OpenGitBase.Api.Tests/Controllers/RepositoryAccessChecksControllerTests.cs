using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Models;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.PublicGitSshKey.Contracts;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;

namespace OpenGitBase.Api.Tests.Controllers;

public class RepositoryAccessChecksControllerTests
{
    private const string SamplePublicKey = "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABgQC7";
    private const string SampleFingerprint = "sample-fingerprint";

    [Theory]
    [InlineData(
        "",
        "alice/repo",
        RepositoryOperation.ReadGit,
        nameof(RepositoryAccessCheckRequest.PublicKey)
    )]
    [InlineData(
        SamplePublicKey,
        "",
        RepositoryOperation.ReadGit,
        nameof(RepositoryAccessCheckRequest.RepositoryPath)
    )]
    [InlineData(
        SamplePublicKey,
        "alice/repo",
        RepositoryOperation.Unknown,
        nameof(RepositoryAccessCheckRequest.Operation)
    )]
    public async Task CheckRepositoryAccess_WhenRequiredFieldMissing_ReturnsValidationProblem(
        string publicKey,
        string repositoryPath,
        RepositoryOperation operation,
        string expectedField
    )
    {
        var controller = CreateController();

        var result = await controller.CheckRepositoryAccess(
            new RepositoryAccessCheckRequest
            {
                PublicKey = publicKey,
                RepositoryPath = repositoryPath,
                Operation = operation,
            },
            CancellationToken.None
        );

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        var details = Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        Assert.True(details.Errors.ContainsKey(expectedField));
    }

    [Theory]
    [InlineData("alice")]
    [InlineData("alice/bob/extra")]
    [InlineData("alice/")]
    [InlineData("/repo")]
    public async Task CheckRepositoryAccess_WhenRepositoryPathInvalid_ReturnsValidationProblem(
        string repositoryPath
    )
    {
        var controller = CreateController();

        var result = await controller.CheckRepositoryAccess(
            ValidRequest(repositoryPath: repositoryPath),
            CancellationToken.None
        );

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        var details = Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        Assert.True(
            details.Errors.ContainsKey(nameof(RepositoryAccessCheckRequest.RepositoryPath))
        );
    }

    [Fact]
    public async Task CheckRepositoryAccess_WhenPublicKeyInvalid_ReturnsValidationProblem()
    {
        var sshKeyService = Substitute.For<ISshKeyService>();
        sshKeyService
            .ValidateAndGetFingerprint(Arg.Any<string>())
            .Throws(new ArgumentException("Invalid SSH key format."));

        var controller = CreateController(sshKeyService: sshKeyService);

        var result = await controller.CheckRepositoryAccess(
            ValidRequest(publicKey: "invalid-key"),
            CancellationToken.None
        );

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        var details = Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        Assert.True(details.Errors.ContainsKey(nameof(RepositoryAccessCheckRequest.PublicKey)));
    }

    [Fact]
    public async Task CheckRepositoryAccess_WhenKeyNotRegistered_ReturnsDenied()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(
                Arg.Any<GetUserIdBySshKeyFingerprintQuery>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Option<UserId>.None);

        var controller = CreateController(queryProcessor: queryProcessor);

        var result = await controller.CheckRepositoryAccess(ValidRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RepositoryAccessCheckResponse>(ok.Value);
        Assert.False(response.Allowed);
        Assert.Equal("SSH public key is not registered.", response.Reason);
    }

    [Fact]
    public async Task CheckRepositoryAccess_WhenOwnerUserMissing_ReturnsDenied()
    {
        var authenticatingUserId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureFingerprintLookup(queryProcessor, authenticatingUserId);
        queryProcessor
            .RunQueryAsync(Arg.Any<UserExistsByUsernameQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<UserId>.None);

        var controller = CreateController(queryProcessor: queryProcessor);

        var result = await controller.CheckRepositoryAccess(ValidRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RepositoryAccessCheckResponse>(ok.Value);
        Assert.False(response.Allowed);
        Assert.Equal(authenticatingUserId.Value, response.ResolvedUserId);
        Assert.Equal("User of repo path not found.", response.Reason);
    }

    [Fact]
    public async Task CheckRepositoryAccess_WhenRepositoryMissing_ReturnsDenied()
    {
        var authenticatingUserId = UserId.From(Guid.NewGuid());
        var ownerUserId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureFingerprintLookup(queryProcessor, authenticatingUserId);
        queryProcessor
            .RunQueryAsync(Arg.Any<UserExistsByUsernameQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(ownerUserId));
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryByOwnerSlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryDto>.None);

        var controller = CreateController(queryProcessor: queryProcessor);

        var result = await controller.CheckRepositoryAccess(ValidRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RepositoryAccessCheckResponse>(ok.Value);
        Assert.False(response.Allowed);
        Assert.Equal("Repository of repo path not found.", response.Reason);
    }

    [Theory]
    [InlineData(RepositoryOperation.ReadGit)]
    [InlineData(RepositoryOperation.WriteGit)]
    public async Task CheckRepositoryAccess_WhenOwner_AllowsOperation(RepositoryOperation operation)
    {
        var ownerUserId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureFingerprintLookup(queryProcessor, ownerUserId);
        queryProcessor
            .RunQueryAsync(Arg.Any<UserExistsByUsernameQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(ownerUserId));
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryByOwnerSlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = ownerUserId,
                        Slug = "repo",
                        Name = "Repo",
                    }
                )
            );

        var controller = CreateController(queryProcessor: queryProcessor);

        var result = await controller.CheckRepositoryAccess(
            ValidRequest(operation: operation),
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RepositoryAccessCheckResponse>(ok.Value);
        Assert.True(response.Allowed);
        Assert.Equal(ownerUserId.Value, response.ResolvedUserId);
        Assert.Equal(repositoryId.Value, response.RepositoryId);
        Assert.Equal("Owner", response.EffectiveRole);

        await queryProcessor
            .DidNotReceive()
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckRepositoryAccess_WhenCollaboratorReader_AllowsRead()
    {
        var authenticatingUserId = UserId.From(Guid.NewGuid());
        var ownerUserId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureOwnerAndRepository(
            queryProcessor,
            authenticatingUserId,
            ownerUserId,
            repositoryId
        );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryMemberDto
                    {
                        RepositoryId = repositoryId,
                        UserId = authenticatingUserId,
                        Role = RepositoryRole.Reader,
                    }
                )
            );

        var controller = CreateController(queryProcessor: queryProcessor);

        var result = await controller.CheckRepositoryAccess(
            ValidRequest(operation: RepositoryOperation.ReadGit),
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RepositoryAccessCheckResponse>(ok.Value);
        Assert.True(response.Allowed);
        Assert.Equal(nameof(RepositoryRole.Reader), response.EffectiveRole);
    }

    [Fact]
    public async Task CheckRepositoryAccess_WhenCollaboratorReader_DeniesWrite()
    {
        var authenticatingUserId = UserId.From(Guid.NewGuid());
        var ownerUserId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureOwnerAndRepository(
            queryProcessor,
            authenticatingUserId,
            ownerUserId,
            repositoryId
        );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryMemberDto
                    {
                        RepositoryId = repositoryId,
                        UserId = authenticatingUserId,
                        Role = RepositoryRole.Reader,
                    }
                )
            );

        var controller = CreateController(queryProcessor: queryProcessor);

        var result = await controller.CheckRepositoryAccess(
            ValidRequest(operation: RepositoryOperation.WriteGit),
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RepositoryAccessCheckResponse>(ok.Value);
        Assert.False(response.Allowed);
        Assert.Equal(nameof(RepositoryRole.Reader), response.EffectiveRole);
    }

    [Fact]
    public async Task CheckRepositoryAccess_WhenCollaboratorWriter_AllowsWrite()
    {
        var authenticatingUserId = UserId.From(Guid.NewGuid());
        var ownerUserId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureOwnerAndRepository(
            queryProcessor,
            authenticatingUserId,
            ownerUserId,
            repositoryId
        );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryMemberDto
                    {
                        RepositoryId = repositoryId,
                        UserId = authenticatingUserId,
                        Role = RepositoryRole.Writer,
                    }
                )
            );

        var controller = CreateController(queryProcessor: queryProcessor);

        var result = await controller.CheckRepositoryAccess(
            ValidRequest(operation: RepositoryOperation.WriteGit),
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RepositoryAccessCheckResponse>(ok.Value);
        Assert.True(response.Allowed);
        Assert.Equal(nameof(RepositoryRole.Writer), response.EffectiveRole);
    }

    [Fact]
    public async Task CheckRepositoryAccess_WhenNotMember_ReturnsDenied()
    {
        var authenticatingUserId = UserId.From(Guid.NewGuid());
        var ownerUserId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureOwnerAndRepository(
            queryProcessor,
            authenticatingUserId,
            ownerUserId,
            repositoryId
        );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryMemberDto>.None);

        var controller = CreateController(queryProcessor: queryProcessor);

        var result = await controller.CheckRepositoryAccess(ValidRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RepositoryAccessCheckResponse>(ok.Value);
        Assert.False(response.Allowed);
        Assert.Equal("No access to repository for user.", response.Reason);
    }

    private static RepositoryAccessChecksController CreateController(
        IQueryProcessor? queryProcessor = null,
        ISshKeyService? sshKeyService = null
    )
    {
        queryProcessor ??= Substitute.For<IQueryProcessor>();
        sshKeyService ??= CreateSshKeyService();

        return new RepositoryAccessChecksController(
            queryProcessor,
            sshKeyService,
            NullLogger<RepositoryAccessChecksController>.Instance,
            new RepositoryStorageQuotaOptions()
        )
        {
            ControllerContext = new ControllerContext(),
        };
    }

    private static ISshKeyService CreateSshKeyService()
    {
        var sshKeyService = Substitute.For<ISshKeyService>();
        sshKeyService.ValidateAndGetFingerprint(Arg.Any<string>()).Returns(SampleFingerprint);
        return sshKeyService;
    }

    private static void ConfigureFingerprintLookup(
        IQueryProcessor queryProcessor,
        UserId authenticatingUserId
    )
    {
        queryProcessor
            .RunQueryAsync(
                Arg.Any<GetUserIdBySshKeyFingerprintQuery>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Option.From(authenticatingUserId));
    }

    private static void ConfigureOwnerAndRepository(
        IQueryProcessor queryProcessor,
        UserId authenticatingUserId,
        UserId ownerUserId,
        RepositoryId repositoryId
    )
    {
        ConfigureFingerprintLookup(queryProcessor, authenticatingUserId);
        queryProcessor
            .RunQueryAsync(Arg.Any<UserExistsByUsernameQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(ownerUserId));
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryByOwnerSlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = ownerUserId,
                        Slug = "repo",
                        Name = "Repo",
                    }
                )
            );
    }

    private static RepositoryAccessCheckRequest ValidRequest(
        string publicKey = SamplePublicKey,
        string repositoryPath = "alice/repo",
        RepositoryOperation operation = RepositoryOperation.ReadGit
    ) =>
        new()
        {
            PublicKey = publicKey,
            RepositoryPath = repositoryPath,
            Operation = operation,
        };
}
