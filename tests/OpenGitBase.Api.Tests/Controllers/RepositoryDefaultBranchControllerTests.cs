using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Auth;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Tests.Controllers;

public class RepositoryDefaultBranchControllerTests
{
    [Fact]
    public async Task Get_WhenRepositoryMissing_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryDto>.None);

        var controller = CreateController(queryProcessor, userId);
        var result = await controller.Get(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Get_WhenAdminMember_ReturnsDefaultBranch()
    {
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var ownerId = UserId.From(Guid.NewGuid());
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = ownerId,
                        Name = "Repo",
                        Slug = "repo",
                        DefaultBranchName = "main",
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryMemberDto
                    {
                        Id = RepositoryMemberId.From(Guid.NewGuid()),
                        RepositoryId = repositoryId,
                        UserId = userId,
                        Role = RepositoryRole.Admin,
                    }
                )
            );

        var controller = CreateController(queryProcessor, userId);
        var result = await controller.Get(repositoryId.Value, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<RepositoryDefaultBranchResponse>(ok.Value);
        Assert.Equal("main", payload.DefaultBranchName);
    }

    private static RepositoryDefaultBranchController CreateController(
        IQueryProcessor queryProcessor,
        UserId userId
    )
    {
        var userContext = Substitute.For<IUserContext>();
        userContext.User.Returns(
            new UserIdentity { IdentityProviderId = userId.Value.ToString(), Username = "test-user" }
        );

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(new DefaultHttpContext());

        var storageContentClient = Substitute.For<IStorageContentClient>();
        var cache = Substitute.For<IRepositoryContentCache>();
        var authorization = new RepositoryContentAuthorizationService(queryProcessor, accessor);
        var contentService = new RepositoryContentService(
            authorization,
            queryProcessor,
            new WebReadReplicaSelector(),
            storageContentClient,
            cache
        );

        return new RepositoryDefaultBranchController(
            queryProcessor,
            userContext,
            contentService
        );
    }
}
