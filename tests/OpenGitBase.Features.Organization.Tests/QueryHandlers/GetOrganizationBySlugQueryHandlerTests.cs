using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Organization;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.QueryHandlers;
using OpenGitBase.Features.Organization.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Organization.Tests.QueryHandlers;

public class GetOrganizationBySlugQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenSlugExists_ReturnsOrganization()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            typeof(GetOrganizationBySlugQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (organizationId, _, _) = await OrganizationTestData.SeedAsync(seedContext);

        var handler = scope.GetHandler<GetOrganizationBySlugQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetOrganizationBySlugQuery { Slug = OrganizationTestData.SampleSlug },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            result,
            dto =>
            {
                Assert.Equal(organizationId, dto.Id);
                Assert.Equal(OrganizationTestData.SampleName, dto.Name);
            }
        );
    }

    [Fact]
    public async Task RunQueryAsync_WhenSlugMissing_ReturnsNone()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            typeof(GetOrganizationBySlugQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        var handler = scope.GetHandler<GetOrganizationBySlugQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetOrganizationBySlugQuery { Slug = "missing-org" },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }
}
