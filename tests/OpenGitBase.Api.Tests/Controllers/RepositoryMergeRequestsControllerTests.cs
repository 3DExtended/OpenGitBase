using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Tests.Controllers;

public class RepositoryMergeRequestsControllerTests
{
    [Fact]
    public async Task List_PublicRepositoryAnonymous_ReturnsOk()
    {
        var repository = CreateRepository(isPrivate: false);
        var controller = CreateController(repository, authenticatedUserId: null);

        var result = await controller.List("owner", "repo", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task List_PrivateRepositoryAnonymous_ReturnsNotFound()
    {
        var repository = CreateRepository(isPrivate: true);
        var controller = CreateController(repository, authenticatedUserId: null);

        var result = await controller.List("owner", "repo", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task List_PrivateRepositoryOutsider_ReturnsForbid()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(isPrivate: true);
        var controller = CreateController(repository, userId, memberRole: null);

        var result = await controller.List("owner", "repo", CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task List_PrivateRepositoryMember_ReturnsOk()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(isPrivate: true);
        var controller = CreateController(
            repository,
            userId,
            memberRole: RepositoryRole.Reader
        );

        var result = await controller.List("owner", "repo", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Create_AnonymousOnPublic_ReturnsUnauthorized()
    {
        var repository = CreateRepository(isPrivate: false);
        var controller = CreateController(repository, authenticatedUserId: null);

        var result = await controller.Create(
            "owner",
            "repo",
            new CreateMergeRequestRequest { Title = "Test" },
            CancellationToken.None
        );

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.NotNull(unauthorized.Value);
    }

    private static RepositoryMergeRequestsController CreateController(
        RepositoryDto repository,
        UserId? authenticatedUserId,
        RepositoryRole? memberRole = RepositoryRole.Reader
    )
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryByOwnerSlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(repository));

        if (memberRole is not null && authenticatedUserId is not null)
        {
            queryProcessor
                .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
                .Returns(
                    Option.From(
                        new RepositoryMemberDto
                        {
                            RepositoryId = repository.Id,
                            UserId = authenticatedUserId,
                            Role = memberRole.Value,
                        }
                    )
                );
        }
        else
        {
            queryProcessor
                .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
                .Returns(Option<RepositoryMemberDto>.None);
        }

        queryProcessor
            .RunQueryAsync(Arg.Any<IsRepositoryUserBlockedQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(false));

        var contentAuth = new RepositoryContentAuthorizationService(
            queryProcessor,
            CreateHttpContextAccessor(authenticatedUserId)
        );
        var discussionAuth = new DiscussionAuthorizationService(
            contentAuth,
            queryProcessor,
            CreateHttpContextAccessor(authenticatedUserId)
        );
        var authorization = new MergeRequestAuthorizationService(discussionAuth);

        return new RepositoryMergeRequestsController(authorization)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = CreateHttpContextAccessor(authenticatedUserId).HttpContext!,
            },
        };
    }

    private static IHttpContextAccessor CreateHttpContextAccessor(UserId? authenticatedUserId)
    {
        var httpContextAccessor = new HttpContextAccessor();
        var context = new DefaultHttpContext();
        if (authenticatedUserId is not null)
        {
            context.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim("identityproviderid", authenticatedUserId.Value.ToString())],
                    authenticationType: "Test"
                )
            );
        }

        httpContextAccessor.HttpContext = context;
        return httpContextAccessor;
    }

    private static RepositoryDto CreateRepository(bool isPrivate)
    {
        var ownerId = UserId.From(Guid.NewGuid());
        return new RepositoryDto
        {
            Id = RepositoryId.From(Guid.NewGuid()),
            OwnerUserId = ownerId,
            OwnerKind = "user",
            Slug = "repo",
            Name = "Repo",
            IsPrivate = isPrivate,
        };
    }
}
