using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Organization;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.QueryHandlers;
using OpenGitBase.Features.Organization.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Organization.Tests.QueryHandlers;

public class ListUserOwnedOrganizationsQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenUserOwnsOrganizations_ReturnsSummaries()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            typeof(ListUserOwnedOrganizationsQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var ownerUserId = UserId.From(Guid.NewGuid());
        await OrganizationTestData.SeedAsync(seedContext, ownerUserId.Value);

        var handler = scope.GetHandler<ListUserOwnedOrganizationsQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListUserOwnedOrganizationsQuery { UserId = ownerUserId },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            result,
            orgs =>
            {
                var org = Assert.Single(orgs);
                Assert.Equal(OrganizationTestData.SampleSlug, org.Slug);
                Assert.Equal(OrganizationTestData.SampleName, org.Name);
            }
        );
    }
}
