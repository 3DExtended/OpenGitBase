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

public class DiscussionAuthorizationServiceTests
{
    [Fact]
    public async Task AuthorizeReadAsync_PublicRepository_ReturnsAllowed()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(ownerId, isPrivate: false);
        var contentAuth = CreateContentAuth(repository);
        var service = CreateService(contentAuth, authenticatedUserId: null);

        var result = await service.AuthorizeReadAsync("owner", "repo", CancellationToken.None);

        Assert.Equal(RepositoryContentAccessResultKind.Allowed, result.Kind);
    }

    [Fact]
    public async Task AuthorizeParticipateAsync_AnonymousOnPublic_ReturnsSignInRequired()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(ownerId, isPrivate: false);
        var contentAuth = CreateContentAuth(repository);
        var service = CreateService(contentAuth, authenticatedUserId: null);

        var result = await service.AuthorizeParticipateAsync("owner", "repo", CancellationToken.None);

        Assert.Equal(DiscussionParticipationResultKind.SignInRequired, result.Kind);
    }

    [Fact]
    public async Task AuthorizeParticipateAsync_BlockedUser_ReturnsBlocked()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var userId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(ownerId, isPrivate: false);
        var contentAuth = CreateContentAuth(repository);
        var queryProcessor = Substitute.For<IQueryProcessor>();
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

        var service = CreateService(contentAuth, userId, queryProcessor);

        var result = await service.AuthorizeParticipateAsync("owner", "repo", CancellationToken.None);

        Assert.Equal(DiscussionParticipationResultKind.Blocked, result.Kind);
    }

    private static RepositoryContentAuthorizationService CreateContentAuth(RepositoryDto repository)
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryByOwnerSlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(repository));
        return new RepositoryContentAuthorizationService(
            queryProcessor,
            new HttpContextAccessor()
        );
    }

    private static DiscussionAuthorizationService CreateService(
        RepositoryContentAuthorizationService contentAuth,
        UserId? authenticatedUserId,
        IQueryProcessor? queryProcessor = null
    )
    {
        queryProcessor ??= Substitute.For<IQueryProcessor>();
        var httpContextAccessor = new HttpContextAccessor();
        if (authenticatedUserId is not null)
        {
            var context = new DefaultHttpContext();
            context.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim("identityproviderid", authenticatedUserId.Value.ToString())],
                    "test"
                )
            );
            httpContextAccessor.HttpContext = context;
        }

        return new DiscussionAuthorizationService(contentAuth, queryProcessor, httpContextAccessor);
    }

    private static RepositoryDto CreateRepository(UserId ownerId, bool isPrivate) =>
        new()
        {
            Id = RepositoryId.From(Guid.NewGuid()),
            Name = "repo",
            Slug = "repo",
            OwnerUserId = ownerId,
            OwnerKind = "user",
            IsPrivate = isPrivate,
        };
}
