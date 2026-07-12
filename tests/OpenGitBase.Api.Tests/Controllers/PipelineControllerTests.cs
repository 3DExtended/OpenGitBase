using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Auth;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Tests.Controllers;

public class PipelineControllerTests
{
    [Fact]
    public async Task IngestGitPush_WithoutStorageHeader_ReturnsUnauthorized()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        var authorization = new RepositoryContentAuthorizationService(
            queryProcessor,
            new HttpContextAccessor()
        );
        var userContext = Substitute.For<IUserContext>();
        var controller = new PipelineController(queryProcessor, authorization, userContext)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };

        var result = await controller.IngestGitPush(
            new IngestGitPushQuery
            {
                RepositoryId = Guid.NewGuid(),
                Ref = "main",
                AfterSha = "abc123",
            },
            CancellationToken.None
        );

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task ListRepositoryRuns_WhenAuthorized_ReturnsRuns()
    {
        var repositoryId = Guid.NewGuid();
        var runId = PipelineRunId.From(Guid.NewGuid());
        var repository = new RepositoryDto
        {
            Id = RepositoryId.From(repositoryId),
            Name = "repo",
            Slug = "repo",
            OwnerUserId = UserId.From(Guid.NewGuid()),
            IsPrivate = false,
        };

        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(repository));
        queryProcessor
            .RunQueryAsync(Arg.Any<ListPipelineRunsQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From<IReadOnlyList<PipelineRunDto>>(
                    new List<PipelineRunDto>
                    {
                        new()
                        {
                            Id = runId,
                            RepositoryId = repositoryId,
                            Ref = "main",
                            AfterSha = "abc123",
                            Status = PipelineRunStatus.Passed,
                            CreatedAt = DateTimeOffset.UtcNow,
                        },
                    }
                )
            );

        var authorization = new RepositoryContentAuthorizationService(
            queryProcessor,
            new HttpContextAccessor()
        );
        var userContext = Substitute.For<IUserContext>();
        var controller = new PipelineController(queryProcessor, authorization, userContext);
        var result = await controller.ListRepositoryRuns(repositoryId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var runs = Assert.IsAssignableFrom<IReadOnlyList<PipelineRunDto>>(ok.Value);
        Assert.Single(runs);
        Assert.Equal(runId, runs[0].Id);
    }
}
