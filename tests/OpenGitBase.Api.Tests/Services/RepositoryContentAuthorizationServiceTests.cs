using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Tests.Services;

public class RepositoryContentAuthorizationServiceTests
{
    [Fact]
    public async Task AuthorizeReadAsync_PublicRepository_ReturnsAllowed()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(ownerId, isPrivate: false);
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryByOwnerSlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(repository));

        var service = CreateService(queryProcessor, authenticatedUserId: null);

        var result = await service.AuthorizeReadAsync("owner", "repo", CancellationToken.None);

        Assert.Equal(RepositoryContentAccessResultKind.Allowed, result.Kind);
        Assert.Equal(repository.Id, result.Repository?.Id);
    }

    [Fact]
    public async Task AuthorizeReadAsync_PrivateRepositoryAnonymous_ReturnsNotFound()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(ownerId, isPrivate: true);
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryByOwnerSlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(repository));

        var service = CreateService(queryProcessor, authenticatedUserId: null);

        var result = await service.AuthorizeReadAsync("owner", "repo", CancellationToken.None);

        Assert.Equal(RepositoryContentAccessResultKind.NotFound, result.Kind);
        Assert.Null(result.Repository);
    }

    [Fact]
    public async Task AuthorizeReadAsync_PrivateRepositoryOutsider_ReturnsForbidden()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var outsiderId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(ownerId, isPrivate: true);
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryByOwnerSlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(repository));
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryMemberDto>.None);

        var service = CreateService(queryProcessor, outsiderId);

        var result = await service.AuthorizeReadAsync("owner", "repo", CancellationToken.None);

        Assert.Equal(RepositoryContentAccessResultKind.Forbidden, result.Kind);
        Assert.Null(result.Repository);
    }

    [Fact]
    public async Task AuthorizeReadAsync_PrivateRepositoryOwner_ReturnsAllowed()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(ownerId, isPrivate: true);
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryByOwnerSlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(repository));

        var service = CreateService(queryProcessor, ownerId);

        var result = await service.AuthorizeReadAsync("owner", "repo", CancellationToken.None);

        Assert.Equal(RepositoryContentAccessResultKind.Allowed, result.Kind);
        Assert.Equal(repository.Id, result.Repository?.Id);
    }

    [Fact]
    public async Task AuthorizeReadAsync_MissingRepository_ReturnsNotFound()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryByOwnerSlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryDto>.None);

        var service = CreateService(queryProcessor, authenticatedUserId: null);

        var result = await service.AuthorizeReadAsync("owner", "missing", CancellationToken.None);

        Assert.Equal(RepositoryContentAccessResultKind.NotFound, result.Kind);
    }

    [Fact]
    public async Task AuthorizeReadByIdAsync_PrivateRepositoryOwner_ReturnsAllowed()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(ownerId, isPrivate: true);
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(repository));

        var service = CreateService(queryProcessor, ownerId);

        var result = await service.AuthorizeReadByIdAsync(repository.Id, CancellationToken.None);

        Assert.Equal(RepositoryContentAccessResultKind.Allowed, result.Kind);
        Assert.Equal(repository.Id, result.Repository?.Id);
    }

    private static RepositoryContentAuthorizationService CreateService(
        IQueryProcessor queryProcessor,
        UserId? authenticatedUserId
    )
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(CreateHttpContext(authenticatedUserId));
        return new RepositoryContentAuthorizationService(queryProcessor, accessor);
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

    private static RepositoryDto CreateRepository(UserId ownerId, bool isPrivate) =>
        new()
        {
            Id = RepositoryId.From(Guid.NewGuid()),
            OwnerUserId = ownerId,
            Slug = "repo",
            Name = "Repo",
            IsPrivate = isPrivate,
            PhysicalPath = "/data/repo.git",
        };
}
