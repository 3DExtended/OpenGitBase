using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.QueryHandlers;
using OpenGitBase.Features.Repository.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Repository.Tests.QueryHandlers;

public class ListUserOwnedRepositoriesQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenUserOwnsRepositories_ReturnsSummaries()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMapsterConfig>(
            typeof(ListUserOwnedRepositoriesQueryHandler).Assembly,
            typeof(UserEntity).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (_, ownerUserId) = await RepositoryTestData.SeedPublicRepositoryAsync(seedContext);

        var handler = scope.GetHandler<ListUserOwnedRepositoriesQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListUserOwnedRepositoriesQuery { UserId = ownerUserId },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            result,
            repos =>
            {
                var repo = Assert.Single(repos);
                Assert.Equal("hello-world", repo.Slug);
                Assert.Equal("Hello World", repo.Name);
            }
        );
    }
}
