using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.QueryHandlers;
using OpenGitBase.Features.Repository.Tests.Testing;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Repository.Tests.QueryHandlers;

public class ListPublicRepositoriesQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenPublicRepositoriesExist_ReturnsRepositories()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMapsterConfig>(
            typeof(ListPublicRepositoriesQueryHandler).Assembly,
            typeof(UserEntity).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        await RepositoryTestData.SeedPublicRepositoryAsync(seedContext, "demo-user", "hello-world");

        var handler = scope.GetHandler<ListPublicRepositoriesQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListPublicRepositoriesQuery(),
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(result, repos => Assert.Single(repos));
    }

    [Fact]
    public async Task RunQueryAsync_WhenSearchMatches_ReturnsFilteredRepositories()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMapsterConfig>(
            typeof(ListPublicRepositoriesQueryHandler).Assembly,
            typeof(UserEntity).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        await RepositoryTestData.SeedPublicRepositoryAsync(seedContext, "demo-user", "hello-world");

        var handler = scope.GetHandler<ListPublicRepositoriesQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListPublicRepositoriesQuery { Search = "hello" },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(result, repos => Assert.Single(repos));
    }
}
