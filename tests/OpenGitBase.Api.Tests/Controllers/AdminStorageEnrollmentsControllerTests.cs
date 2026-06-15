using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Models;
using OpenGitBase.Common.Auth;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Tests.Controllers;

public class AdminStorageEnrollmentsControllerTests
{
    [Fact]
    public async Task List_ReturnsEnrollments()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<ListStorageNodeEnrollmentsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From<IReadOnlyList<StorageNodeEnrollmentDto>>(Array.Empty<StorageNodeEnrollmentDto>()));

        var userContext = Substitute.For<IUserContext>();
        userContext.User.Returns(
            new UserIdentity { IdentityProviderId = Guid.NewGuid().ToString(), Username = "admin" }
        );

        var controller = new AdminStorageEnrollmentsController(queryProcessor, userContext);

        var result = await controller.List(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }
}
