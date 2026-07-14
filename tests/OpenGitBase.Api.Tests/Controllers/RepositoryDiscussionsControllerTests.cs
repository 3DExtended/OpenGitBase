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

public class RepositoryDiscussionsControllerTests
{
    [Fact]
    public async Task Get_WithIncludeComments_PassesFlagToQuery()
    {
        var repository = CreateRepository(isPrivate: false);
        var comments = new List<DiscussionCommentDto>
        {
            new()
            {
                Id = DiscussionCommentId.From(Guid.NewGuid()),
                BodyMarkdown = "Hello",
            },
        };
        var dto = new DiscussionDto
        {
            Id = DiscussionId.From(Guid.NewGuid()),
            RepositoryId = repository.Id.Value,
            Number = 1,
            Title = "Test",
            Comments = comments,
        };

        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureRepositoryLookup(queryProcessor, repository);
        queryProcessor
            .RunQueryAsync(
                Arg.Is<GetDiscussionByNumberQuery>(q =>
                    q.IncludeComments && q.Number == 1 && q.RepositoryId == repository.Id.Value
                ),
                Arg.Any<CancellationToken>()
            )
            .Returns(Option.From(dto));

        var controller = CreateController(queryProcessor, authenticatedUserId: null);

        var result = await controller.Get(
            "owner",
            "repo",
            1,
            "comments",
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<DiscussionDto>(ok.Value);
        Assert.NotNull(response.Comments);
        Assert.Single(response.Comments);
    }

    [Fact]
    public async Task Get_WhenAuthenticated_SetsViewerEffectiveRole()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(isPrivate: false);
        repository.OwnerUserId = userId;
        var dto = new DiscussionDto
        {
            Id = DiscussionId.From(Guid.NewGuid()),
            RepositoryId = repository.Id.Value,
            Number = 1,
            Title = "Test",
        };

        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureRepositoryLookup(queryProcessor, repository);
        queryProcessor
            .RunQueryAsync(Arg.Any<GetDiscussionByNumberQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(dto));

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.Get(
            "owner",
            "repo",
            1,
            null,
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<DiscussionDto>(ok.Value);
        Assert.Equal(nameof(RepositoryRole.Owner), response.ViewerEffectiveRole);
    }

    [Fact]
    public async Task Create_WithBody_CreatesInitialComment()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(isPrivate: false);
        var discussion = new DiscussionDto
        {
            Id = DiscussionId.From(Guid.NewGuid()),
            RepositoryId = repository.Id.Value,
            Number = 1,
            Title = "New thread",
        };

        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureRepositoryLookup(queryProcessor, repository);
        queryProcessor
            .RunQueryAsync(Arg.Any<CreateDiscussionQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(discussion));
        queryProcessor
            .RunQueryAsync(Arg.Any<CreateDiscussionCommentQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new DiscussionCommentDto
                    {
                        Id = DiscussionCommentId.From(Guid.NewGuid()),
                        BodyMarkdown = "Opening post",
                    }
                )
            );

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.Create(
            "owner",
            "repo",
            new CreateDiscussionRequest { Title = "New thread", Body = "Opening post" },
            CancellationToken.None
        );

        Assert.IsType<CreatedAtActionResult>(result);
        await queryProcessor
            .Received(1)
            .RunQueryAsync(
                Arg.Is<CreateDiscussionCommentQuery>(q =>
                    q.BodyMarkdown == "Opening post" && q.DiscussionNumber == 1
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ListLinks_PublicRepository_ReturnsOk()
    {
        var repository = CreateRepository(isPrivate: false);
        var links = new List<DiscussionLinkDto>
        {
            new()
            {
                TargetDiscussionNumber = 42,
                RelationshipType = DiscussionRelationshipType.Parent,
                TargetDiscussionTitle = "[PRD] Spec",
            },
        };

        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureRepositoryLookup(queryProcessor, repository);
        queryProcessor
            .RunQueryAsync(Arg.Any<ListDiscussionLinksQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From<IReadOnlyList<DiscussionLinkDto>>(links));

        var controller = CreateController(queryProcessor, authenticatedUserId: null);

        var result = await controller.ListLinks("owner", "repo", 43, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsAssignableFrom<IReadOnlyList<DiscussionLinkDto>>(ok.Value);
        Assert.Single(response);
    }

    [Fact]
    public async Task CreateLink_WhenAuthenticated_PassesTargetNumber()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(isPrivate: false);
        repository.OwnerUserId = userId;
        var link = new DiscussionLinkDto
        {
            TargetDiscussionNumber = 42,
            RelationshipType = DiscussionRelationshipType.Parent,
        };

        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureRepositoryLookup(queryProcessor, repository);
        ConfigureMemberLookup(queryProcessor, repository, userId, RepositoryRole.Writer);
        ConfigureBlockedLookup(queryProcessor);
        queryProcessor
            .RunQueryAsync(Arg.Any<CreateDiscussionLinkQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(link));

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.CreateLink(
            "owner",
            "repo",
            43,
            new CreateDiscussionLinkRequest
            {
                TargetDiscussionNumber = 42,
                RelationshipType = "parent",
            },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        await queryProcessor
            .Received(1)
            .RunQueryAsync(
                Arg.Is<CreateDiscussionLinkQuery>(q =>
                    q.Number == 43
                    && q.TargetDiscussionNumber == 42
                    && q.RelationshipType == DiscussionRelationshipType.Parent
                ),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateLink_Anonymous_ReturnsUnauthorized()
    {
        var repository = CreateRepository(isPrivate: false);
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureRepositoryLookup(queryProcessor, repository);

        var controller = CreateController(queryProcessor, authenticatedUserId: null);

        var result = await controller.CreateLink(
            "owner",
            "repo",
            43,
            new CreateDiscussionLinkRequest
            {
                TargetDiscussionNumber = 42,
                RelationshipType = "parent",
            },
            CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task CreateLink_InvalidRelationship_ReturnsBadRequest()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(isPrivate: false);
        repository.OwnerUserId = userId;
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureRepositoryLookup(queryProcessor, repository);
        ConfigureMemberLookup(queryProcessor, repository, userId, RepositoryRole.Writer);
        ConfigureBlockedLookup(queryProcessor);

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.CreateLink(
            "owner",
            "repo",
            43,
            new CreateDiscussionLinkRequest
            {
                TargetDiscussionNumber = 42,
                RelationshipType = "invalid",
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DeleteLink_ExistingLink_ReturnsNoContent()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(isPrivate: false);
        repository.OwnerUserId = userId;
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureRepositoryLookup(queryProcessor, repository);
        ConfigureMemberLookup(queryProcessor, repository, userId, RepositoryRole.Writer);
        ConfigureBlockedLookup(queryProcessor);
        queryProcessor
            .RunQueryAsync(Arg.Any<DeleteDiscussionLinkQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(Unit.Value));

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.DeleteLink(
            "owner",
            "repo",
            43,
            42,
            "parent",
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteLink_MissingLink_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(isPrivate: false);
        repository.OwnerUserId = userId;
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureRepositoryLookup(queryProcessor, repository);
        ConfigureMemberLookup(queryProcessor, repository, userId, RepositoryRole.Writer);
        ConfigureBlockedLookup(queryProcessor);
        queryProcessor
            .RunQueryAsync(Arg.Any<DeleteDiscussionLinkQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<Unit>.None);

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.DeleteLink(
            "owner",
            "repo",
            43,
            42,
            "parent",
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    private static RepositoryDiscussionsController CreateController(
        IQueryProcessor queryProcessor,
        UserId? authenticatedUserId
    )
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(CreateHttpContext(authenticatedUserId));

        var authorization = new DiscussionAuthorizationService(
            new RepositoryContentAuthorizationService(queryProcessor, accessor),
            queryProcessor,
            accessor
        );

        return new RepositoryDiscussionsController(authorization, queryProcessor)
        {
            ControllerContext = new ControllerContext { HttpContext = accessor.HttpContext! },
        };
    }

    private static void ConfigureRepositoryLookup(
        IQueryProcessor queryProcessor,
        RepositoryDto repository
    )
    {
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryByOwnerSlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(repository));
    }

    private static void ConfigureMemberLookup(
        IQueryProcessor queryProcessor,
        RepositoryDto repository,
        UserId userId,
        RepositoryRole role
    )
    {
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryMemberDto
                    {
                        RepositoryId = repository.Id,
                        UserId = userId,
                        Role = role,
                    }
                )
            );
    }

    private static void ConfigureBlockedLookup(IQueryProcessor queryProcessor)
    {
        queryProcessor
            .RunQueryAsync(Arg.Any<IsRepositoryUserBlockedQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(false));
    }

    private static HttpContext CreateHttpContext(UserId? authenticatedUserId)
    {
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

        return context;
    }

    private static RepositoryDto CreateRepository(bool isPrivate) =>
        new()
        {
            Id = RepositoryId.From(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            OwnerUserId = UserId.From(Guid.NewGuid()),
            Slug = "repo",
            Name = "Repo",
            IsPrivate = isPrivate,
            PhysicalPath = "/data/repo.git",
        };
}
