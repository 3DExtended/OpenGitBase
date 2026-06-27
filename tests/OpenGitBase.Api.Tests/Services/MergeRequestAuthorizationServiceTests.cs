using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Tests.Services;

public class MergeRequestAuthorizationServiceTests
{
    [Fact]
    public async Task AuthorizeReadAsync_PublicRepository_ReturnsAllowed()
    {
        var repository = CreateRepository(isPrivate: false);
        var service = CreateService(repository, authenticatedUserId: null);

        var result = await service.AuthorizeReadAsync("owner", "repo", CancellationToken.None);

        Assert.Equal(RepositoryContentAccessResultKind.Allowed, result.Kind);
    }

    [Fact]
    public async Task AuthorizeCreateAsync_AnonymousOnPublic_ReturnsSignInRequired()
    {
        var repository = CreateRepository(isPrivate: false);
        var service = CreateService(repository, authenticatedUserId: null);

        var result = await service.AuthorizeCreateAsync("owner", "repo", CancellationToken.None);

        Assert.Equal(MergeRequestAuthorizationResultKind.SignInRequired, result.Kind);
    }

    [Fact]
    public async Task AuthorizeCreateAsync_ReaderOnPublic_ReturnsInsufficientRole()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(isPrivate: false);
        var service = CreateService(
            repository,
            userId,
            memberRole: RepositoryRole.Reader
        );

        var result = await service.AuthorizeCreateAsync("owner", "repo", CancellationToken.None);

        Assert.Equal(MergeRequestAuthorizationResultKind.InsufficientRole, result.Kind);
    }

    [Fact]
    public async Task AuthorizeCreateAsync_WriterOnPublic_ReturnsAllowed()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(isPrivate: false);
        var service = CreateService(
            repository,
            userId,
            memberRole: RepositoryRole.Writer
        );

        var result = await service.AuthorizeCreateAsync("owner", "repo", CancellationToken.None);

        Assert.Equal(MergeRequestAuthorizationResultKind.Allowed, result.Kind);
        Assert.Equal(userId, result.UserId);
    }

    [Fact]
    public async Task AuthorizeApproveAsync_Author_ReturnsSelfApprovalNotAllowed()
    {
        var authorId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(isPrivate: false);
        var service = CreateService(
            repository,
            authorId,
            memberRole: RepositoryRole.Writer
        );

        var result = await service.AuthorizeApproveAsync(
            "owner",
            "repo",
            authorId,
            CancellationToken.None
        );

        Assert.Equal(MergeRequestAuthorizationResultKind.SelfApprovalNotAllowed, result.Kind);
    }

    [Fact]
    public async Task AuthorizeApproveAsync_WriterNotAuthor_ReturnsAllowed()
    {
        var authorId = UserId.From(Guid.NewGuid());
        var reviewerId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(isPrivate: false);
        var service = CreateService(
            repository,
            reviewerId,
            memberRole: RepositoryRole.Writer
        );

        var result = await service.AuthorizeApproveAsync(
            "owner",
            "repo",
            authorId,
            CancellationToken.None
        );

        Assert.Equal(MergeRequestAuthorizationResultKind.Allowed, result.Kind);
    }

    [Fact]
    public async Task AuthorizeApproveAsync_Reader_ReturnsInsufficientRole()
    {
        var authorId = UserId.From(Guid.NewGuid());
        var userId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(isPrivate: false);
        var service = CreateService(
            repository,
            userId,
            memberRole: RepositoryRole.Reader
        );

        var result = await service.AuthorizeApproveAsync(
            "owner",
            "repo",
            authorId,
            CancellationToken.None
        );

        Assert.Equal(MergeRequestAuthorizationResultKind.InsufficientRole, result.Kind);
    }

    [Fact]
    public async Task AuthorizeMergeAsync_Reader_ReturnsInsufficientRole()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(isPrivate: false);
        var service = CreateService(
            repository,
            userId,
            memberRole: RepositoryRole.Reader
        );

        var result = await service.AuthorizeMergeAsync("owner", "repo", CancellationToken.None);

        Assert.Equal(MergeRequestAuthorizationResultKind.InsufficientRole, result.Kind);
    }

    [Fact]
    public async Task AuthorizeMergeAsync_Writer_ReturnsAllowed()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(isPrivate: false);
        var service = CreateService(
            repository,
            userId,
            memberRole: RepositoryRole.Writer
        );

        var result = await service.AuthorizeMergeAsync("owner", "repo", CancellationToken.None);

        Assert.Equal(MergeRequestAuthorizationResultKind.Allowed, result.Kind);
    }

    [Fact]
    public async Task AuthorizeParticipateAsync_BlockedUser_ReturnsBlocked()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(isPrivate: false);
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureRepositoryLookup(queryProcessor, repository);
        queryProcessor
            .RunQueryAsync(Arg.Any<IsRepositoryUserBlockedQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(true));
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryMemberDto
                    {
                        RepositoryId = repository.Id,
                        UserId = userId,
                        Role = RepositoryRole.Reader,
                    }
                )
            );

        var service = CreateService(repository, userId, queryProcessor);

        var result = await service.AuthorizeParticipateAsync(
            "owner",
            "repo",
            CancellationToken.None
        );

        Assert.Equal(MergeRequestAuthorizationResultKind.Blocked, result.Kind);
    }

    private static MergeRequestAuthorizationService CreateService(
        RepositoryDto repository,
        UserId? authenticatedUserId,
        IQueryProcessor? queryProcessor = null,
        RepositoryRole? memberRole = null
    )
    {
        var ownsQueryProcessor = queryProcessor is null;
        queryProcessor ??= Substitute.For<IQueryProcessor>();
        ConfigureRepositoryLookup(queryProcessor, repository);

        if (ownsQueryProcessor)
        {
            if (memberRole is not null && authenticatedUserId is not null)
            {
                queryProcessor
                    .RunQueryAsync(
                        Arg.Any<GetRepositoryMemberQuery>(),
                        Arg.Any<CancellationToken>()
                    )
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
                    .RunQueryAsync(
                        Arg.Any<GetRepositoryMemberQuery>(),
                        Arg.Any<CancellationToken>()
                    )
                    .Returns(Option<RepositoryMemberDto>.None);
            }

            queryProcessor
                .RunQueryAsync(
                    Arg.Any<IsRepositoryUserBlockedQuery>(),
                    Arg.Any<CancellationToken>()
                )
                .Returns(Option.From(false));
        }

        var contentAuth = new RepositoryContentAuthorizationService(
            queryProcessor,
            new HttpContextAccessor()
        );
        var discussionAuth = new DiscussionAuthorizationService(
            contentAuth,
            queryProcessor,
            CreateHttpContextAccessor(authenticatedUserId)
        );
        return new MergeRequestAuthorizationService(discussionAuth);
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

    private static IHttpContextAccessor CreateHttpContextAccessor(UserId? authenticatedUserId)
    {
        var httpContextAccessor = new HttpContextAccessor();
        if (authenticatedUserId is null)
        {
            return httpContextAccessor;
        }

        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim("identityproviderid", authenticatedUserId.Value.ToString())],
                authenticationType: "Test"
            )
        );
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
