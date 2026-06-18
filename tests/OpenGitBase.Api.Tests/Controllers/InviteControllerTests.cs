using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Common.Auth;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Tests.Controllers;

public class InviteControllerTests
{
    [Fact]
    public async Task GetByToken_WhenFound_ReturnsOk()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetOrganizationInviteByTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new OrganizationInvitePublicDto
                    {
                        InviteId = OrganizationInviteId.From(Guid.NewGuid()),
                        OrganizationId = OrganizationId.From(Guid.NewGuid()),
                        OrganizationName = "Acme",
                        OrganizationSlug = "acme",
                        Email = "ac***@example.com",
                        Role = OrganizationMemberRole.Member,
                        ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                        Status = OrganizationInviteStatus.Pending,
                    }
                )
            );

        var controller = CreateController(queryProcessor, UserId.From(Guid.NewGuid()));
        var result = await controller.GetByToken("token", CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Accept_WhenEmailMismatch_ReturnsBadRequest()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<AcceptOrganizationInviteQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(AcceptOrganizationInviteResult.EmailMismatch));

        var controller = CreateController(queryProcessor, UserId.From(Guid.NewGuid()));
        var result = await controller.Accept("token", CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Decline_WhenMissing_ReturnsNotFound()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<DeclineOrganizationInviteQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<Unit>.None);

        var controller = CreateController(queryProcessor, UserId.From(Guid.NewGuid()));
        var result = await controller.Decline("token", CancellationToken.None);
        Assert.IsType<NotFoundResult>(result);
    }

    private static InviteController CreateController(IQueryProcessor queryProcessor, UserId userId)
    {
        var userContext = Substitute.For<IUserContext>();
        userContext.User.Returns(
            new UserIdentity
            {
                IdentityProviderId = userId.Value.ToString(),
                Username = "invite-user",
            }
        );

        return new InviteController(queryProcessor, userContext);
    }
}
