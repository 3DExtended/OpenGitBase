using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Tests.Controllers;

public class PublicDiscoveryControllerTests
{
    [Fact]
    public async Task ListRepositories_WhenFound_ReturnsOk()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<ListPublicRepositoriesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From((IReadOnlyList<RepositoryDto>)Array.Empty<RepositoryDto>()));

        var controller = new PublicDiscoveryController(queryProcessor);
        var result = await controller.ListRepositories(null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task ListRecentRepositories_WhenFound_ReturnsOk()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(
                Arg.Any<ListRecentPublicRepositoriesQuery>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Option.From((IReadOnlyList<RepositoryDto>)Array.Empty<RepositoryDto>()));

        var controller = new PublicDiscoveryController(queryProcessor);
        var result = await controller.ListRecentRepositories(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetOwnerProfile_WhenFound_ReturnsOk()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetOwnerProfileQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new OwnerProfileDto
                    {
                        Slug = "demo-user",
                        Name = "demo-user",
                        Kind = "user",
                        Repositories = [],
                    }
                )
            );

        var controller = new PublicDiscoveryController(queryProcessor);
        var result = await controller.GetOwnerProfile("demo-user", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetOwnerProfile_WhenMissing_ReturnsNotFound()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetOwnerProfileQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<OwnerProfileDto>.None);

        var controller = new PublicDiscoveryController(queryProcessor);
        var result = await controller.GetOwnerProfile("missing", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
