using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.GitAccessToken.Contracts;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.PublicGitSshKey.Contracts;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;
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
    public async Task CheckRepositoryAccess_WhenBothCredentialsProvided_ReturnsValidationProblem()
    {
        var controller = CreateController();

        var result = await controller.CheckRepositoryAccess(
            new RepositoryAccessCheckRequest
            {
                PublicKey = SamplePublicKey,
                AccessToken = "ogb_test",
                RepositoryPath = "alice/repo",
                Operation = RepositoryOperation.ReadGit,
            },
            CancellationToken.None
        );

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        var details = Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        Assert.True(details.Errors.ContainsKey(nameof(RepositoryAccessCheckRequest.PublicKey)));
        Assert.True(details.Errors.ContainsKey(nameof(RepositoryAccessCheckRequest.AccessToken)));
    }

    [Fact]
    public async Task CheckRepositoryAccess_WhenAccessTokenInvalid_ReturnsDenied()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(
                Arg.Any<ValidateGitAccessTokenQuery>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Option<ValidateGitAccessTokenResult>.None);

        var controller = CreateController(queryProcessor: queryProcessor);

        var result = await controller.CheckRepositoryAccess(
            ValidTokenRequest(),
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RepositoryAccessCheckResponse>(ok.Value);
        Assert.False(response.Allowed);
        Assert.Equal("Access token is invalid or expired.", response.Reason);
    }

    [Fact]
    public async Task CheckRepositoryAccess_WhenReadTokenDeniesWrite_ReturnsDenied()
    {
        var authenticatingUserId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureTokenValidation(
            queryProcessor,
            authenticatingUserId,
            GitAccessTokenScopes.Read
        );

        var controller = CreateController(queryProcessor: queryProcessor);

        var result = await controller.CheckRepositoryAccess(
            ValidTokenRequest(operation: RepositoryOperation.WriteGit),
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RepositoryAccessCheckResponse>(ok.Value);
        Assert.False(response.Allowed);
        Assert.Equal(authenticatingUserId.Value, response.ResolvedUserId);
        Assert.Equal("Token does not allow write access.", response.Reason);
    }

    [Theory]
    [InlineData(RepositoryOperation.ReadGit, GitAccessTokenScopes.Read)]
    [InlineData(RepositoryOperation.WriteGit, GitAccessTokenScopes.Write)]
    public async Task CheckRepositoryAccess_WhenOwnerWithValidToken_AllowsOperation(
        RepositoryOperation operation,
        string scope
    )
    {
        var ownerUserId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var storageNodeId = StorageNodeId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureTokenValidation(queryProcessor, ownerUserId, scope);
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
                        PhysicalPath = $"/srv/git/{repositoryId.Value}.git",
                        StorageNodeId = storageNodeId,
                    }
                )
            );
        ConfigureReplicationRouting(queryProcessor, storageNodeId);

        var controller = CreateController(queryProcessor: queryProcessor);

        var result = await controller.CheckRepositoryAccess(
            ValidTokenRequest(operation: operation),
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RepositoryAccessCheckResponse>(ok.Value);
        Assert.True(response.Allowed);
        Assert.Equal("storage-1", response.StorageNodeInternalHost);
        Assert.Equal(22, response.StorageNodeInternalSshPort);
        Assert.Equal(8082, response.StorageNodeInternalGitHttpPort);
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
        var storageNodeId = StorageNodeId.From(Guid.NewGuid());
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
                        PhysicalPath = $"/srv/git/{repositoryId.Value}.git",
                        StorageNodeId = storageNodeId,
                    }
                )
            );
        ConfigureReplicationRouting(queryProcessor, storageNodeId);

        var controller = CreateController(queryProcessor: queryProcessor);

        var result = await controller.CheckRepositoryAccess(
            ValidRequest(operation: operation),
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RepositoryAccessCheckResponse>(ok.Value);
        Assert.True(response.Allowed);
        Assert.Equal($"/srv/git/{repositoryId.Value}.git", response.PhysicalPath);
        Assert.Equal("storage-1", response.StorageNodeInternalHost);
        Assert.Equal(22, response.StorageNodeInternalSshPort);
        Assert.Equal(8082, response.StorageNodeInternalGitHttpPort);

        await queryProcessor
            .DidNotReceive()
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckRepositoryAccess_WhenStorageNodeUnhealthy_ReturnsDenied()
    {
        var ownerUserId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var storageNodeId = StorageNodeId.From(Guid.NewGuid());
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
                        PhysicalPath = $"/srv/git/{repositoryId.Value}.git",
                        StorageNodeId = storageNodeId,
                    }
                )
            );
        ConfigureReplicationRouting(queryProcessor, storageNodeId, healthy: false);

        var controller = CreateController(queryProcessor: queryProcessor);

        var result = await controller.CheckRepositoryAccess(ValidRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RepositoryAccessCheckResponse>(ok.Value);
        Assert.False(response.Allowed);
        Assert.Equal("Primary storage node is unavailable.", response.Reason);
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
    public async Task CheckRepositoryAccess_WhenOrganizationOwnerUsesWritePat_AllowsWrite()
    {
        var authenticatingUserId = UserId.From(Guid.NewGuid());
        var organizationId = OrganizationId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var storageNodeId = StorageNodeId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();

        ConfigureTokenValidation(
            queryProcessor,
            authenticatingUserId,
            GitAccessTokenScopes.Write
        );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryByOwnerSlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = UserId.From(organizationId.Value),
                        OwnerKind = "organization",
                        OwnerSlug = "opengitbase",
                        Slug = "open-git-base",
                        Name = "Open Git Base",
                        PhysicalPath = $"/srv/git/{repositoryId.Value}.git",
                        StorageNodeId = storageNodeId,
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetOrganizationQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new OrganizationDto
                    {
                        Id = organizationId,
                        Name = "OpenGitBase",
                        Slug = "opengitbase",
                        OwnerUserId = authenticatingUserId.Value,
                    }
                )
            );
        ConfigureReplicationRouting(queryProcessor, storageNodeId);

        var controller = CreateController(queryProcessor: queryProcessor);

        var result = await controller.CheckRepositoryAccess(
            ValidTokenRequest(
                repositoryPath: "opengitbase/open-git-base",
                operation: RepositoryOperation.WriteGit
            ),
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RepositoryAccessCheckResponse>(ok.Value);
        Assert.True(response.Allowed);
        Assert.Equal("Owner", response.EffectiveRole);

        await queryProcessor
            .DidNotReceive()
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>());
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

    [Fact]
    public async Task CheckRepositoryAccess_WhenWriterPushesProtectedMainViaSsh_Denies()
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
        queryProcessor
            .RunQueryAsync(Arg.Any<ListProtectedBranchRulesQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From<IReadOnlyList<ProtectedBranchRuleDto>>(
                    [
                        new ProtectedBranchRuleDto
                        {
                            RepositoryId = repositoryId,
                            Pattern = "main",
                            BlockDirectPush = true,
                            RequireMergeRequest = true,
                        },
                    ]
                )
            );

        var controller = CreateController(queryProcessor: queryProcessor);

        var result = await controller.CheckRepositoryAccess(
            new RepositoryAccessCheckRequest
            {
                PublicKey = SamplePublicKey,
                RepositoryPath = "alice/repo",
                Operation = RepositoryOperation.WriteGit,
                RefUpdates =
                [
                    new GitRefUpdateRequest
                    {
                        RefName = "refs/heads/main",
                        OldSha = GitShaHelper.NullSha,
                        NewSha = "abababababababababababababababababababab",
                    },
                ],
            },
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RepositoryAccessCheckResponse>(ok.Value);
        Assert.False(response.Allowed);
        Assert.Contains("protected branch", response.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckRepositoryAccess_WhenWriterPushesFeatureBranchViaHttps_Allows()
    {
        var authenticatingUserId = UserId.From(Guid.NewGuid());
        var ownerUserId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureTokenValidation(
            queryProcessor,
            authenticatingUserId,
            GitAccessTokenScopes.Write
        );
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
        queryProcessor
            .RunQueryAsync(Arg.Any<ListProtectedBranchRulesQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From<IReadOnlyList<ProtectedBranchRuleDto>>(
                    [
                        new ProtectedBranchRuleDto
                        {
                            RepositoryId = repositoryId,
                            Pattern = "main",
                            BlockDirectPush = true,
                        },
                    ]
                )
            );

        var controller = CreateController(queryProcessor: queryProcessor);

        var result = await controller.CheckRepositoryAccess(
            new RepositoryAccessCheckRequest
            {
                AccessToken = "ogb_test_token",
                RepositoryPath = "alice/repo",
                Operation = RepositoryOperation.WriteGit,
                RefUpdates =
                [
                    new GitRefUpdateRequest
                    {
                        RefName = "refs/heads/feature/login",
                        OldSha = GitShaHelper.NullSha,
                        NewSha = "bcbcbcbcbcbcbcbcbcbcbcbcbcbcbcbcbcbcbcbc",
                    },
                ],
            },
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RepositoryAccessCheckResponse>(ok.Value);
        Assert.True(response.Allowed);
    }

    [Fact]
    public async Task CheckRepositoryAccess_WhenPlatformIdentityPushesProtectedMain_Allows()
    {
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var storageNodeId = StorageNodeId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryByOwnerSlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = UserId.From(Guid.NewGuid()),
                        Slug = "repo",
                        Name = "Repo",
                        DefaultBranchName = "main",
                        PhysicalPath = $"/srv/git/{repositoryId.Value}.git",
                        StorageNodeId = storageNodeId,
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<ListProtectedBranchRulesQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From<IReadOnlyList<ProtectedBranchRuleDto>>(
                    [
                        new ProtectedBranchRuleDto
                        {
                            RepositoryId = repositoryId,
                            Pattern = "main",
                            BlockDirectPush = true,
                        },
                    ]
                )
            );
        ConfigureReplicationRouting(queryProcessor, storageNodeId);

        var controller = CreateController(queryProcessor: queryProcessor);

        var result = await controller.CheckRepositoryAccess(
            new RepositoryAccessCheckRequest
            {
                AccessToken = "platform-token",
                RepositoryPath = "alice/repo",
                Operation = RepositoryOperation.WriteGit,
                RefUpdates =
                [
                    new GitRefUpdateRequest
                    {
                        RefName = "refs/heads/main",
                        OldSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                        NewSha = "cccccccccccccccccccccccccccccccccccccccc",
                        IsForcePush = true,
                    },
                ],
            },
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RepositoryAccessCheckResponse>(ok.Value);
        Assert.True(response.Allowed);
        Assert.Equal("PlatformMerge", response.EffectiveRole);
    }

    private static RepositoryAccessChecksController CreateController(
        IQueryProcessor? queryProcessor = null,
        ISshKeyService? sshKeyService = null,
        PlatformMergeIdentityOptions? platformMergeOptions = null
    )
    {
        queryProcessor ??= Substitute.For<IQueryProcessor>();
        sshKeyService ??= CreateSshKeyService();
        platformMergeOptions ??= new PlatformMergeIdentityOptions
        {
            AccessToken = "platform-token",
        };

        return new RepositoryAccessChecksController(
            queryProcessor,
            sshKeyService,
            new GitPushEnforcementService(queryProcessor),
            NullLogger<RepositoryAccessChecksController>.Instance,
            new RepositoryStorageQuotaOptions(),
            platformMergeOptions
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

    private static void ConfigureTokenValidation(
        IQueryProcessor queryProcessor,
        UserId authenticatingUserId,
        string scope
    )
    {
        queryProcessor
            .RunQueryAsync(
                Arg.Any<ValidateGitAccessTokenQuery>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Option.From(
                    new ValidateGitAccessTokenResult
                    {
                        UserId = authenticatingUserId,
                        Scope = scope,
                    }
                )
            );
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
        var storageNodeId = StorageNodeId.From(Guid.NewGuid());
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
                        PhysicalPath = $"/srv/git/{repositoryId.Value}.git",
                        StorageNodeId = storageNodeId,
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<RepositoryReplicationRoutingQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryReplicationRoutingDto
                    {
                        ReplicationEpoch = 1,
                        WriteQuorumAvailable = true,
                        Targets =
                        [
                            new RepositoryRoutingTargetDto
                            {
                                StorageNodeId = storageNodeId.Value,
                                InternalHost = "storage-1",
                                InternalSshPort = 22,
                                InternalGitHttpPort = 8082,
                                Role = "Primary",
                                IsHealthy = true,
                                IsInSync = true,
                                IsPrimary = true,
                            },
                        ],
                    }
                )
            );
    }

    private static void ConfigureReplicationRouting(
        IQueryProcessor queryProcessor,
        StorageNodeId storageNodeId,
        bool healthy = true,
        bool writeQuorumAvailable = true,
        bool inSync = true
    )
    {
        queryProcessor
            .RunQueryAsync(Arg.Any<RepositoryReplicationRoutingQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryReplicationRoutingDto
                    {
                        ReplicationEpoch = 1,
                        WriteQuorumAvailable = writeQuorumAvailable && healthy,
                        Targets =
                        [
                            new RepositoryRoutingTargetDto
                            {
                                StorageNodeId = storageNodeId.Value,
                                InternalHost = "storage-1",
                                InternalSshPort = 22,
                                InternalGitHttpPort = 8082,
                                Role = "Primary",
                                IsHealthy = healthy,
                                IsInSync = inSync && healthy,
                                IsPrimary = true,
                            },
                        ],
                    }
                )
            );
    }

    private static RepositoryAccessCheckRequest ValidTokenRequest(
        string accessToken = "ogb_test_token",
        string repositoryPath = "alice/repo",
        RepositoryOperation operation = RepositoryOperation.ReadGit
    ) =>
        new()
        {
            AccessToken = accessToken,
            RepositoryPath = repositoryPath,
            Operation = operation,
        };

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
