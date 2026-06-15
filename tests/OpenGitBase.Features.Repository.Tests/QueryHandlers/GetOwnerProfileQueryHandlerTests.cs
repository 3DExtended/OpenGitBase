using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Organization;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.QueryHandlers;
using OpenGitBase.Features.Repository.Tests.Testing;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Repository.Tests.QueryHandlers;

public class GetOwnerProfileQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenUserOwnerExists_ReturnsUserProfile()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        await RepositoryTestData.SeedPublicRepositoryAsync(seedContext, "demo-user", "hello-world");

        var handler = scope.GetHandler<GetOwnerProfileQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetOwnerProfileQuery { OwnerSlug = "demo-user" },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            result,
            profile =>
            {
                Assert.Equal("user", profile.Kind);
                Assert.Equal("demo-user", profile.Slug);
                Assert.Single(profile.Repositories);
            }
        );
    }

    [Fact]
    public async Task RunQueryAsync_WhenOrganizationOwnerExists_ReturnsOrganizationProfile()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        await RepositoryTestData.SeedOrganizationOwnedRepositoryAsync(seedContext);

        var handler = scope.GetHandler<GetOwnerProfileQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetOwnerProfileQuery { OwnerSlug = "acme-corp" },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            result,
            profile =>
            {
                Assert.Equal("organization", profile.Kind);
                Assert.Equal("acme-corp", profile.Slug);
                Assert.Single(profile.Repositories);
            }
        );
    }

    [Fact]
    public async Task RunQueryAsync_WhenOwnerMissing_ReturnsNone()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();

        var handler = scope.GetHandler<GetOwnerProfileQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetOwnerProfileQuery { OwnerSlug = "missing-owner" },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }

    private static InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMapsterConfig> CreateScope() =>
        new(
            typeof(GetOwnerProfileQueryHandler).Assembly,
            typeof(OrganizationMapsterConfig).Assembly,
            typeof(UserEntity).Assembly
        );
}
