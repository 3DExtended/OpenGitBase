using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Tests.Controllers;

public class AdminStorageNodesControllerTests
{
    [Fact]
    public async Task List_ReturnsNodes()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<ListStorageNodeQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From<IReadOnlyList<StorageNodeDto>>(Array.Empty<StorageNodeDto>()));

        var controller = new AdminStorageNodesController(queryProcessor);

        var result = await controller.List(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }
}
