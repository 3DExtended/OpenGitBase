using System.Net;
using System.Security.Claims;
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
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Tests.Controllers;

public class RepositoryMemberControllerAuthorizationTests
{
    [Fact]
    public async Task List_PrivateRepositoryOutsider_ReturnsForbid()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var outsiderId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = ownerId,
                        Slug = "private-repo",
                        Name = "Private",
                        IsPrivate = true,
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryMemberDto>.None);

        var controller = CreateController(queryProcessor, outsiderId);

        var result = await controller.List(repositoryId.Value, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task List_PublicRepositoryMember_ReturnsOk()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var readerId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = ownerId,
                        Slug = "public-repo",
                        Name = "Public",
                        IsPrivate = false,
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<ListRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From<IReadOnlyList<RepositoryMemberDto>>(
                    [
                        new RepositoryMemberDto
                        {
                            Id = RepositoryMemberId.From(Guid.NewGuid()),
                            RepositoryId = repositoryId,
                            UserId = readerId,
                            Role = RepositoryRole.Reader,
                        },
                    ]
                )
            );

        var controller = CreateController(queryProcessor, readerId);

        var result = await controller.List(repositoryId.Value, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var members = Assert.IsAssignableFrom<IReadOnlyList<RepositoryMemberDto>>(ok.Value);
        Assert.Single(members);
    }

    [Fact]
    public async Task Create_AdminCannotGrantOwner_ReturnsForbid()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var adminId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = ownerId,
                        Slug = "shared",
                        Name = "Shared",
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
                        UserId = adminId,
                        Role = RepositoryRole.Admin,
                    }
                )
            );

        var controller = CreateController(queryProcessor, adminId);
        var request = new CreateRepositoryMemberQuery
        {
            ModelToCreate = new RepositoryMemberDto
            {
                RepositoryId = repositoryId,
                UserId = UserId.From(Guid.NewGuid()),
                Role = RepositoryRole.Owner,
            },
        };

        var result = await controller.Create(request, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        await queryProcessor
            .DidNotReceive()
            .RunQueryAsync(Arg.Any<CreateRepositoryMemberQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_AdminCannotPromoteToOwner_ReturnsForbid()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var adminId = UserId.From(Guid.NewGuid());
        var targetUserId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var memberId = RepositoryMemberId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var query = callInfo.Arg<GetRepositoryMemberQuery>();
                if (query.UserId == adminId)
                {
                    return Option.From(
                        new RepositoryMemberDto
                        {
                            Id = RepositoryMemberId.From(Guid.NewGuid()),
                            RepositoryId = repositoryId,
                            UserId = adminId,
                            Role = RepositoryRole.Admin,
                        }
                    );
                }

                return Option.From(
                    new RepositoryMemberDto
                    {
                        Id = memberId,
                        RepositoryId = repositoryId,
                        UserId = targetUserId,
                        Role = RepositoryRole.Writer,
                    }
                );
            });
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = ownerId,
                        Slug = "shared",
                        Name = "Shared",
                    }
                )
            );

        var controller = CreateController(queryProcessor, adminId);
        var request = new UpdateRepositoryMemberQuery
        {
            UpdatedModel = new RepositoryMemberDto
            {
                Id = memberId,
                RepositoryId = repositoryId,
                UserId = targetUserId,
                Role = RepositoryRole.Owner,
            },
        };

        var result = await controller.Update(memberId.Value, request, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        await queryProcessor
            .DidNotReceive()
            .RunQueryAsync(Arg.Any<UpdateRepositoryMemberQuery>(), Arg.Any<CancellationToken>());
    }

    private static RepositoryMemberController CreateController(
        IQueryProcessor queryProcessor,
        UserId userId
    )
    {
        var userContext = Substitute.For<IUserContext>();
        userContext.User.Returns(
            new UserIdentity { IdentityProviderId = userId.Value.ToString(), Username = "testuser" }
        );

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(
            new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(
                        [new Claim("identityproviderid", userId.Value.ToString())],
                        authenticationType: "Test"
                    )
                ),
            }
        );

        return new RepositoryMemberController(
            queryProcessor,
            userContext,
            new RepositoryContentAuthorizationService(queryProcessor, accessor)
        );
    }
}
