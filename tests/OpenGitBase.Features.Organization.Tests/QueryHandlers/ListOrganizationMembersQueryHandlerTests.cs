using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Organization;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.QueryHandlers;
using OpenGitBase.Features.Organization.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Organization.Tests.QueryHandlers;

public class ListOrganizationMembersQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenMembersExist_ReturnsMembersWithUsernames()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            typeof(ListOrganizationMembersQueryHandler).Assembly
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

        var handler = scope.GetHandler<ListOrganizationMembersQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListOrganizationMembersQuery { OrganizationId = organizationId },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            result,
            members =>
            {
                var member = Assert.Single(members);
                Assert.Equal(ownerUserId, member.UserId);
                Assert.Equal(OrganizationMemberRole.Owner, member.Role);
                Assert.False(string.IsNullOrWhiteSpace(member.Username));
            }
        );
    }
}
