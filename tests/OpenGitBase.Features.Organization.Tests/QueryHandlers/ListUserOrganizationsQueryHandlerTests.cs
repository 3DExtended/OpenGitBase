using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Organization;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.QueryHandlers;
using OpenGitBase.Features.Organization.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Organization.Tests.QueryHandlers;

public class ListUserOrganizationsQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenUserIsMember_ReturnsOrganizations()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            typeof(ListUserOrganizationsQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (organizationId, _, ownerUserId) = await OrganizationTestData.SeedAsync(seedContext);
        await OrganizationTestData.SeedMemberAsync(
            seedContext,
            organizationId,
            ownerUserId,
            OrganizationMemberRole.Owner
        );

        var handler = scope.GetHandler<ListUserOrganizationsQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListUserOrganizationsQuery { UserId = ownerUserId },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            result,
            orgs =>
            {
                var org = Assert.Single(orgs);
                Assert.Equal(organizationId, org.Id);
            }
        );
    }
}
