using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Tests.Controllers;

public class RepositoryInternalPushValidationControllerTests
{
    [Fact]
    public async Task ValidatePush_WhenPushRulesOnlyAndDcoMissing_ReturnsDenied()
    {
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        PhysicalPath = $"/srv/git/{repositoryId.Value}.git",
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<ListProtectedBranchRulesQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From<IReadOnlyList<ProtectedBranchRuleDto>>(
                    [
                        new ProtectedBranchRuleDto
                        {
                            RepositoryId = repositoryId,
                            Pattern = "main",
                            PushRules =
                            [
                                new PushRuleDto
                                {
                                    RuleType = PushRuleType.RequireDco,
                                    ConfigJson = """{"required":true}""",
                                },
                            ],
                        },
                    ]
                )
            );

        var controller = new RepositoryInternalPushValidationController(
            queryProcessor,
            new GitPushEnforcementService(queryProcessor)
        );

        var result = await controller.ValidatePush(
            new RepositoryPushValidationRequest
            {
                PhysicalPath = $"/srv/git/{repositoryId.Value}.git",
                ValidatePushRulesOnly = true,
                Commits =
                [
                    new GitPushCommitRequest
                    {
                        Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                        Message = "missing signoff",
                    },
                ],
            },
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RepositoryPushValidationResponse>(ok.Value);
        Assert.False(response.Allowed);
        Assert.Contains("RequireDco", response.Reason);
    }
}
