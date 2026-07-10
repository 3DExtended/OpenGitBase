using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Tests.Controllers;

public class PublicDiscoveryControllerRedactionTests
{
    [Fact]
    public async Task ListRepositories_OmitsInfrastructureFields()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var repositories = new List<RepositoryDto>
        {
            new()
            {
                Id = RepositoryId.From(Guid.NewGuid()),
                OwnerUserId = ownerId,
                Slug = "hello-world",
                Name = "Hello World",
                PhysicalPath = "/srv/git/hello.git",
                StorageNodeId = StorageNodeId.From(Guid.NewGuid()),
            },
        };
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<ListPublicRepositoriesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From((IReadOnlyList<RepositoryDto>)repositories));

        var controller = CreateController(queryProcessor);
        var result = await controller.ListRepositories(null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<object>>(ok.Value);
        Assert.All(returned, item => Assert.IsType<RepositorySummaryResponse>(item));
    }

    [Fact]
    public async Task GetOwnerProfile_OmitsInfrastructureFields()
    {
        var ownerId = UserId.From(Guid.NewGuid());
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
                        Repositories =
                        [
                            new RepositoryDto
                            {
                                Id = RepositoryId.From(Guid.NewGuid()),
                                OwnerUserId = ownerId,
                                Slug = "hello-world",
                                Name = "Hello World",
                                PhysicalPath = "/srv/git/hello.git",
                                StorageNodeId = StorageNodeId.From(Guid.NewGuid()),
                            },
                        ],
                    }
                )
            );

        var controller = CreateController(queryProcessor);
        var result = await controller.GetOwnerProfile("demo-user", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = ok.Value!;
        var repositoriesProperty = payload.GetType().GetProperty("Repositories");
        Assert.NotNull(repositoriesProperty);
        var repositories = Assert.IsAssignableFrom<IReadOnlyList<object>>(
            repositoriesProperty.GetValue(payload)
        );
        Assert.All(repositories, item => Assert.IsType<RepositorySummaryResponse>(item));
    }

    private static PublicDiscoveryController CreateController(IQueryProcessor queryProcessor)
    {
        var context = new DefaultHttpContext
        {
            Connection = { RemoteIpAddress = IPAddress.Parse("203.0.113.10") },
        };
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        return new PublicDiscoveryController(
            queryProcessor,
            new RepositoryResponseMapper(accessor)
        );
    }
}
