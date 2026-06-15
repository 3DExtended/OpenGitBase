using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.QueryHandlers;
using OpenGitBase.Features.Repository.Tests.Testing;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Repository.Tests.QueryHandlers;

public class ListRepositoriesForUserQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_IncludesOwnedAndMemberRepositories()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMapsterConfig>(
            typeof(ListRepositoriesForUserQueryHandler).Assembly,
            typeof(UserEntity).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (ownedRepositoryId, ownerUserId) = await RepositoryTestData.SeedPublicRepositoryAsync(
            seedContext,
            "owner-user",
            "owned-repo"
        );
        var (memberRepositoryId, _) = await RepositoryTestData.SeedPublicRepositoryAsync(
            seedContext,
            "other-owner",
            "member-repo"
        );

        var memberUserId = UserId.From(Guid.NewGuid());
        seedContext.Set<UserEntity>().Add(RepositoryTestData.CreateUser(memberUserId.Value, "member"));

        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<ListRepositoryIdsForUserQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var query = callInfo.Arg<ListRepositoryIdsForUserQuery>();
                if (query.UserId == memberUserId)
                {
                    return Option.From((IReadOnlyList<Guid>)new List<Guid> { memberRepositoryId.Value });
                }

                return Option.From((IReadOnlyList<Guid>)Array.Empty<Guid>());
            });
        queryProcessor
            .RunQueryAsync(Arg.Any<ListUserOrganizationsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From((IReadOnlyList<OrganizationDto>)Array.Empty<OrganizationDto>()));

        var contextFactory = scope.GetService<IDbContextFactory<OpenGitBaseDbContext>>();
        var mapper = scope.GetService<IMapper>();
        var handler = new ListRepositoriesForUserQueryHandler(
            mapper,
            contextFactory,
            queryProcessor
        );

        var result = await handler.RunQueryAsync(
            new ListRepositoriesForUserQuery { UserId = ownerUserId },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            result,
            repos =>
            {
                Assert.Contains(repos, repo => repo.Id == ownedRepositoryId);
                Assert.DoesNotContain(repos, repo => repo.Id == memberRepositoryId);
            }
        );

        var memberResult = await handler.RunQueryAsync(
            new ListRepositoriesForUserQuery { UserId = memberUserId },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            memberResult,
            repos => Assert.Contains(repos, repo => repo.Id == memberRepositoryId)
        );
    }
}
