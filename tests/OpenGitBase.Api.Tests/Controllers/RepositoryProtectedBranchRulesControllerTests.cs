using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Auth;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Tests.Controllers;

public class RepositoryProtectedBranchRulesControllerTests
{
    [Fact]
    public async Task List_WhenRepositoryMissing_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryDto>.None);

        var controller = CreateController(queryProcessor, userId);
        var result = await controller.List(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task List_WhenNonOwnerWithoutAdminMembership_ReturnsForbid()
    {
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var ownerId = UserId.From(Guid.NewGuid());
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(CreateRepository(repositoryId, ownerId)));
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryMemberDto>.None);

        var controller = CreateController(queryProcessor, userId);
        var result = await controller.List(repositoryId.Value, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task List_WhenAdminMember_ReturnsOk()
    {
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var ownerId = UserId.From(Guid.NewGuid());
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(CreateRepository(repositoryId, ownerId)));
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryMemberDto
                    {
                        Id = RepositoryMemberId.From(Guid.NewGuid()),
                        RepositoryId = repositoryId,
                        UserId = userId,
                        Role = RepositoryRole.Admin,
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
                            Id = ProtectedBranchRuleId.From(Guid.NewGuid()),
                            RepositoryId = repositoryId,
                            Pattern = "@default",
                        },
                    ]
                )
            );

        var controller = CreateController(queryProcessor, userId);
        var result = await controller.List(repositoryId.Value, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<ProtectedBranchRuleDto>>(ok.Value);
        Assert.Single(payload);
    }

    [Fact]
    public async Task Create_WhenOwner_MapsDefaultPatternAliasAndReturnsCreated()
    {
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var ownerId = UserId.From(Guid.NewGuid());
        var createdId = ProtectedBranchRuleId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(CreateRepository(repositoryId, ownerId)));
        queryProcessor
            .RunQueryAsync(Arg.Any<CreateProtectedBranchRuleQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(createdId));

        var controller = CreateController(queryProcessor, ownerId);
        var request = new UpsertProtectedBranchRuleRequest
        {
            Pattern = "@default",
            AllowedPushUserIds = [Guid.NewGuid()],
            PushRules = [new UpsertPushRuleRequest { RuleType = PushRuleType.RequireDco, ConfigJson = "{}" }],
        };

        var result = await controller.Create(repositoryId.Value, request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(createdId, created.Value);
        await queryProcessor
            .Received(1)
            .RunQueryAsync(
                Arg.Is<CreateProtectedBranchRuleQuery>(query =>
                    query.ModelToCreate.Pattern == DefaultRefResolver.DefaultBranchPatternAlias
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Update_WhenRuleNotFound_ReturnsNotFound()
    {
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var ownerId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(CreateRepository(repositoryId, ownerId)));
        queryProcessor
            .RunQueryAsync(Arg.Any<UpdateProtectedBranchRuleQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<Unit>.None);

        var controller = CreateController(queryProcessor, ownerId);
        var result = await controller.Update(
            repositoryId.Value,
            Guid.NewGuid(),
            new UpsertProtectedBranchRuleRequest { Pattern = "main" },
            CancellationToken.None
        );

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_WhenOwnerAndSuccess_ReturnsNoContent()
    {
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var ownerId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(CreateRepository(repositoryId, ownerId)));
        queryProcessor
            .RunQueryAsync(Arg.Any<DeleteProtectedBranchRuleQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(Unit.Value));

        var controller = CreateController(queryProcessor, ownerId);
        var ruleId = Guid.NewGuid();
        var result = await controller.Delete(repositoryId.Value, ruleId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        await queryProcessor
            .Received(1)
            .RunQueryAsync(
                Arg.Is<DeleteProtectedBranchRuleQuery>(query =>
                    query.Id == ProtectedBranchRuleId.From(ruleId)
                ),
                Arg.Any<CancellationToken>()
            );
    }

    private static RepositoryDto CreateRepository(RepositoryId repositoryId, UserId ownerId) =>
        new()
        {
            Id = repositoryId,
            OwnerUserId = ownerId,
            Slug = "repo",
            Name = "Repo",
            PhysicalPath = "/tmp/repo.git",
        };

    private static RepositoryProtectedBranchRulesController CreateController(
        IQueryProcessor queryProcessor,
        UserId userId,
        string username = "test-user"
    )
    {
        var userContext = Substitute.For<IUserContext>();
        userContext.User.Returns(
            new UserIdentity { IdentityProviderId = userId.Value.ToString(), Username = username }
        );
        return new RepositoryProtectedBranchRulesController(queryProcessor, userContext);
    }
}
