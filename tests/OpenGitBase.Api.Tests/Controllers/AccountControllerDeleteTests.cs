using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Models;
using OpenGitBase.Common.Auth;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;

namespace OpenGitBase.Api.Tests.Controllers;

public class AccountControllerDeleteTests
{
    [Fact]
    public async Task DeleteAccount_WithWrongPassword_ReturnsBadRequest()
    {
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<UserDeleteAccountQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<UserDeleteAccountResult>.None);

        var userContext = Substitute.For<IUserContext>();
        userContext.User.Returns(
            new UserIdentity
            {
                IdentityProviderId = userId.Value.ToString(),
                Username = "deleteuser",
            }
        );

        var controller = new AccountController(
            queryProcessor,
            userContext,
            new DebugFeaturesOptions()
        );
        var result = await controller.DeleteAccountAsync(
            new DeleteAccountDto { Password = "WrongPassword!" },
            CancellationToken.None
        );

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
