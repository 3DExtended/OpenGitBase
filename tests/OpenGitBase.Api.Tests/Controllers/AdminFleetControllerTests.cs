using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Tests.Controllers;

public class AdminFleetControllerTests
{
    [Fact]
    public async Task GetDispatcherSshPublicKey_WhenConfigured_ReturnsKey()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetFleetDispatcherSshPublicKeyQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From("ssh-rsa AAA"));

        var controller = new AdminFleetController(queryProcessor);

        var result = await controller.GetDispatcherSshPublicKey(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }
}
