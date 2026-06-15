using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.QueryHandlers;
using OpenGitBase.Features.Repository.Tests.Testing;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Repository.Tests.QueryHandlers;

public class ListRecentPublicRepositoriesQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_ReturnsRecentPublicRepositories()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMapsterConfig>(
            typeof(ListRecentPublicRepositoriesQueryHandler).Assembly,
            typeof(UserEntity).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        await RepositoryTestData.SeedPublicRepositoryAsync(seedContext, "demo-user", "hello-world");

        var handler = scope.GetHandler<ListRecentPublicRepositoriesQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListRecentPublicRepositoriesQuery { Limit = 5 },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(result, repos => Assert.Single(repos));
    }
}
