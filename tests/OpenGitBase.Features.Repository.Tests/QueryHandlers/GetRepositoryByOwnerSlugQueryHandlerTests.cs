using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.QueryHandlers;
using OpenGitBase.Features.Repository.Tests.Testing;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Repository.Tests.QueryHandlers;

public class GetRepositoryByOwnerSlugQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenUserOwnedRepositoryExists_ReturnsRepository()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMapsterConfig>(
            typeof(GetRepositoryByOwnerSlugQueryHandler).Assembly,
            typeof(OpenGitBase.Features.Organization.OrganizationMapsterConfig).Assembly,
            typeof(UserEntity).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (repositoryId, _) = await RepositoryTestData.SeedPublicRepositoryAsync(
            seedContext,
            "demo-user",
            "hello-world"
        );

        var handler = scope.GetHandler<GetRepositoryByOwnerSlugQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetRepositoryByOwnerSlugQuery { OwnerSlug = "demo-user", Slug = "hello-world" },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            result,
            repository => Assert.Equal(repositoryId, repository.Id)
        );
    }

    [Fact]
    public async Task RunQueryAsync_WhenRepositoryMissing_ReturnsNone()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMapsterConfig>(
            typeof(GetRepositoryByOwnerSlugQueryHandler).Assembly,
            typeof(UserEntity).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        await RepositoryTestData.SeedPublicRepositoryAsync(seedContext, "demo-user", "existing-repo");

        var handler = scope.GetHandler<GetRepositoryByOwnerSlugQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetRepositoryByOwnerSlugQuery { OwnerSlug = "demo-user", Slug = "missing" },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }

    [Fact]
    public async Task RunQueryAsync_WhenOrganizationOwnedRepositoryExists_ReturnsRepository()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMapsterConfig>(
            typeof(GetRepositoryByOwnerSlugQueryHandler).Assembly,
            typeof(OpenGitBase.Features.Organization.OrganizationMapsterConfig).Assembly,
            typeof(UserEntity).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        await RepositoryTestData.SeedOrganizationOwnedRepositoryAsync(
            seedContext,
            "acme-corp",
            "public-app"
        );

        var handler = scope.GetHandler<GetRepositoryByOwnerSlugQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetRepositoryByOwnerSlugQuery { OwnerSlug = "acme-corp", Slug = "public-app" },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            result,
            repository =>
            {
                Assert.Equal("public-app", repository.Slug);
                Assert.Equal("Public App", repository.Name);
            }
        );
    }
}
