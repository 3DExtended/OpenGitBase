using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Models.StorageContent;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Tests.Controllers;

public class RepositoryMergeRequestsControllerTests
{
    [Fact]
    public async Task List_PublicRepositoryAnonymous_ReturnsOk()
    {
        var repository = CreateRepository(isPrivate: false);
        var controller = CreateController(repository, authenticatedUserId: null);

        var result = await controller.List("owner", "repo", null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsAssignableFrom<IReadOnlyList<MergeRequestDto>>(ok.Value);
    }

    [Fact]
    public async Task List_PrivateRepositoryAnonymous_ReturnsNotFound()
    {
        var repository = CreateRepository(isPrivate: true);
        var controller = CreateController(repository, authenticatedUserId: null);

        var result = await controller.List("owner", "repo", null, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task List_PrivateRepositoryOutsider_ReturnsForbid()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(isPrivate: true);
        var controller = CreateController(repository, userId, memberRole: null);

        var result = await controller.List("owner", "repo", null, CancellationToken.None);

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

        var result = await controller.List("owner", "repo", null, CancellationToken.None);

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
            new CreateMergeRequestRequest
            {
                Title = "Test",
                SourceRef = "feature/a",
                TargetRef = "main",
            },
            CancellationToken.None
        );

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.NotNull(unauthorized.Value);
    }

    [Fact]
    public async Task Create_RejectsSourceNotAhead()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(isPrivate: false);
        var controller = CreateController(
            repository,
            userId,
            memberRole: RepositoryRole.Writer,
            aheadCount: 0
        );

        var result = await controller.Create(
            "owner",
            "repo",
            new CreateMergeRequestRequest
            {
                Title = "Test",
                SourceRef = "feature/a",
                TargetRef = "main",
            },
            CancellationToken.None
        );

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task GetChanges_PublicRepositoryAnonymous_ReturnsOk()
    {
        var repository = CreateRepository(isPrivate: false);
        var controller = CreateController(repository, authenticatedUserId: null);

        var result = await controller.GetChanges("owner", "repo", 1, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var changes = Assert.IsType<MergeRequestChangesResponse>(ok.Value);
        Assert.Single(changes.Files);
        Assert.Equal("README.md", changes.Files[0].FilePath);
        Assert.Equal("remove", changes.Files[0].Hunks[0].Lines[0].Type);
    }

    [Fact]
    public async Task ListCommits_PublicRepositoryAnonymous_ReturnsOk()
    {
        var repository = CreateRepository(isPrivate: false);
        var controller = CreateController(repository, authenticatedUserId: null);

        var result = await controller.ListCommits("owner", "repo", 1, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var commits = Assert.IsAssignableFrom<IReadOnlyList<MergeRequestCommitResponse>>(ok.Value);
        Assert.Single(commits);
        Assert.Equal("feature commit", commits[0].Message);
    }

    [Fact]
    public async Task ListCommits_MessageWithEmail_RedactsEmail()
    {
        var repository = CreateRepository(isPrivate: false);
        var controller = CreateController(
            repository,
            authenticatedUserId: null,
            commitMessage: "Signed-off-by: Dev <dev@example.com>"
        );

        var result = await controller.ListCommits("owner", "repo", 1, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var commits = Assert.IsAssignableFrom<IReadOnlyList<MergeRequestCommitResponse>>(ok.Value);
        Assert.Equal("Signed-off-by: Dev <***@***>", commits[0].Message);
    }

    [Fact]
    public async Task ListDiscussionLinks_PublicRepository_ReturnsOk()
    {
        var repository = CreateRepository(isPrivate: false);
        var controller = CreateController(repository, authenticatedUserId: null);
        var queryProcessor = GetQueryProcessor(controller);
        queryProcessor
            .RunQueryAsync(Arg.Any<ListMergeRequestDiscussionLinksQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From<IReadOnlyList<MergeRequestDiscussionLinkDto>>(
                    [
                        new MergeRequestDiscussionLinkDto
                        {
                            DiscussionNumber = 3,
                            RelationshipType = MergeRequestRelationshipType.Closes,
                            DiscussionTitle = "Fix login",
                            DiscussionStatus = "Open",
                        },
                    ]
                )
            );

        var result = await controller.ListDiscussionLinks("owner", "repo", 1, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var links = Assert.IsAssignableFrom<IReadOnlyList<MergeRequestDiscussionLinkDto>>(ok.Value);
        Assert.Single(links);
        Assert.Equal(3, links[0].DiscussionNumber);
    }

    [Fact]
    public async Task CreateDiscussionLink_Writer_ReturnsOk()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(isPrivate: false);
        var controller = CreateController(repository, userId, memberRole: RepositoryRole.Writer);
        var queryProcessor = GetQueryProcessor(controller);
        queryProcessor
            .RunQueryAsync(Arg.Any<CreateMergeRequestDiscussionLinkQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new MergeRequestDiscussionLinkDto
                    {
                        DiscussionNumber = 5,
                        RelationshipType = MergeRequestRelationshipType.Implements,
                        DiscussionTitle = "API cleanup",
                        DiscussionStatus = "Open",
                    }
                )
            );

        var result = await controller.CreateDiscussionLink(
            "owner",
            "repo",
            1,
            new CreateMergeRequestDiscussionLinkRequest
            {
                DiscussionNumber = 5,
                RelationshipType = "implements",
            },
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result);
        var link = Assert.IsType<MergeRequestDiscussionLinkDto>(ok.Value);
        Assert.Equal(5, link.DiscussionNumber);
    }

    [Fact]
    public async Task CreateDiscussionLink_InvalidRelationship_ReturnsBadRequest()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(isPrivate: false);
        var controller = CreateController(repository, userId, memberRole: RepositoryRole.Writer);

        var result = await controller.CreateDiscussionLink(
            "owner",
            "repo",
            1,
            new CreateMergeRequestDiscussionLinkRequest
            {
                DiscussionNumber = 5,
                RelationshipType = "invalid-type",
            },
            CancellationToken.None
        );

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DeleteDiscussionLink_MissingLink_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(isPrivate: false);
        var controller = CreateController(repository, userId, memberRole: RepositoryRole.Writer);
        var queryProcessor = GetQueryProcessor(controller);
        queryProcessor
            .RunQueryAsync(Arg.Any<DeleteMergeRequestDiscussionLinkQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<Unit>.None);

        var result = await controller.DeleteDiscussionLink(
            "owner",
            "repo",
            1,
            9,
            "related",
            CancellationToken.None
        );

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CreateDiscussionLink_NotFound_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(isPrivate: false);
        var controller = CreateController(repository, userId, memberRole: RepositoryRole.Writer);
        var queryProcessor = GetQueryProcessor(controller);
        queryProcessor
            .RunQueryAsync(Arg.Any<CreateMergeRequestDiscussionLinkQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<MergeRequestDiscussionLinkDto>.None);

        var result = await controller.CreateDiscussionLink(
            "owner",
            "repo",
            1,
            new CreateMergeRequestDiscussionLinkRequest
            {
                DiscussionNumber = 404,
                RelationshipType = "related",
            },
            CancellationToken.None
        );

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CreateDiscussionLink_Anonymous_ReturnsUnauthorized()
    {
        var repository = CreateRepository(isPrivate: false);
        var controller = CreateController(repository, authenticatedUserId: null);

        var result = await controller.CreateDiscussionLink(
            "owner",
            "repo",
            1,
            new CreateMergeRequestDiscussionLinkRequest
            {
                DiscussionNumber = 5,
                RelationshipType = "related",
            },
            CancellationToken.None
        );

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task DeleteDiscussionLink_ExistingLink_ReturnsNoContent()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(isPrivate: false);
        var controller = CreateController(repository, userId, memberRole: RepositoryRole.Writer);
        var queryProcessor = GetQueryProcessor(controller);
        queryProcessor
            .RunQueryAsync(Arg.Any<DeleteMergeRequestDiscussionLinkQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(Unit.Value));

        var result = await controller.DeleteDiscussionLink(
            "owner",
            "repo",
            1,
            9,
            "closes",
            CancellationToken.None
        );

        Assert.IsType<NoContentResult>(result);
    }

    private static IQueryProcessor GetQueryProcessor(RepositoryMergeRequestsController controller)
    {
        var field = typeof(RepositoryMergeRequestsController).GetField(
            "_queryProcessor",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );
        return (IQueryProcessor)field!.GetValue(controller)!;
    }

    private static RepositoryMergeRequestsController CreateController(
        RepositoryDto repository,
        UserId? authenticatedUserId,
        RepositoryRole? memberRole = RepositoryRole.Reader,
        int aheadCount = 3,
        string commitMessage = "feature commit"
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

        queryProcessor
            .RunQueryAsync(
                Arg.Any<ListMergeRequestsByRepositoryQuery>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Option.From<IReadOnlyList<MergeRequestDto>>([]));

        queryProcessor
            .RunQueryAsync(Arg.Any<GetMergeRequestByNumberQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new MergeRequestDto
                    {
                        RepositoryId = repository.Id.Value,
                        Number = 1,
                        Title = "Test MR",
                        Status = MergeRequestStatus.Open,
                        CreatorUserId = authenticatedUserId ?? UserId.From(Guid.NewGuid()),
                        SourceRef = "feature/a",
                        TargetRef = "main",
                        SourceHeadSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                        TargetBaseSha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                    }
                )
            );

        var storageContentClient = Substitute.For<IStorageContentClient>();
        storageContentClient
            .ResolveRefAsync(
                Arg.Any<RepositoryRoutingTargetDto>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                call =>
                {
                    var refName = call.ArgAt<string>(3);
                    return Task.FromResult<StorageContentResolveRefPayload?>(
                        new StorageContentResolveRefPayload
                        {
                            CommitSha = refName.Contains("main", StringComparison.OrdinalIgnoreCase)
                                ? "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                                : "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                        }
                    );
                }
            );
        storageContentClient
            .GetAheadCountAsync(
                Arg.Any<RepositoryRoutingTargetDto>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new StorageContentAheadCountPayload { AheadCount = aheadCount });
        storageContentClient
            .GetDiffAsync(
                Arg.Any<RepositoryRoutingTargetDto>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new StorageContentDiffPayload
                {
                    Files =
                    [
                        new StorageContentDiffFilePayload
                        {
                            NewPath = "README.md",
                            Status = "modified",
                            Hunks =
                            [
                                new StorageContentDiffHunkPayload
                                {
                                    OldStart = 1,
                                    OldLines = 1,
                                    NewStart = 1,
                                    NewLines = 2,
                                    Lines =
                                    [
                                        new StorageContentDiffLinePayload
                                        {
                                            Type = "delete",
                                            Content = "initial",
                                            OldLineNumber = 1,
                                        },
                                        new StorageContentDiffLinePayload
                                        {
                                            Type = "add",
                                            Content = "change",
                                            NewLineNumber = 1,
                                        },
                                    ],
                                },
                            ],
                        },
                    ],
                }
            );
        storageContentClient
            .ListCommitsSinceMergeBaseAsync(
                Arg.Any<RepositoryRoutingTargetDto>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new StorageContentCommitsPayload
                {
                    Commits =
                    [
                        new StorageContentCommitPayload
                        {
                            Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                            ShortSha = "aaaaaaaa",
                            Message = commitMessage,
                            AuthorName = "owner",
                            AuthoredAt = "2026-07-01T00:00:00+00:00",
                        },
                    ],
                }
            );

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
        var refService = new MergeRequestRefService(
            queryProcessor,
            storageContentClient,
            new WebReadReplicaSelector()
        );
        var mergeService = new MergeRequestMergeService(
            queryProcessor,
            storageContentClient,
            refService,
            new PlatformMergeIdentityOptions { AccessToken = "platform-token" }
        );
        var compareService = new MergeRequestCompareService(refService, storageContentClient);

        queryProcessor
            .RunQueryAsync(Arg.Any<RepositoryReplicationRoutingQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryReplicationRoutingDto
                    {
                        Targets =
                        [
                            new RepositoryRoutingTargetDto
                            {
                                StorageNodeId = Guid.NewGuid(),
                                InternalHost = "127.0.0.1",
                                InternalHttpPort = 8081,
                                IsHealthy = true,
                                IsPrimary = true,
                                IsInSync = true,
                            },
                        ],
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetStorageNodeApiTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From("token"));

        return new RepositoryMergeRequestsController(
            authorization,
            refService,
            mergeService,
            compareService,
            queryProcessor
        )
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
            PhysicalPath = "/srv/git/test.git",
        };
    }
}
