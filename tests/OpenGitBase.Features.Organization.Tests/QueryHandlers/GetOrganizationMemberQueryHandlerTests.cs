using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Organization;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Organization.QueryHandlers;
using OpenGitBase.Features.Organization.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Organization.Tests.QueryHandlers;

public class GetOrganizationMemberQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenMemberExists_ReturnsMember()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            typeof(GetOrganizationMemberQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (organizationId, _, _) = await OrganizationTestData.SeedAsync(seedContext);
        var memberUserId = UserId.From(Guid.NewGuid());
        await OrganizationTestData.SeedMemberAsync(
            seedContext,
            organizationId,
            memberUserId,
            OrganizationMemberRole.Owner
        );

        var handler = scope.GetHandler<GetOrganizationMemberQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetOrganizationMemberQuery
            {
                OrganizationId = organizationId,
                UserId = memberUserId,
            },
            CancellationToken.None
        );

        var member = QueryHandlerResultAssert.AssertSome(result);
        Assert.Equal(OrganizationMemberRole.Owner, member.Role);
    }

    [Fact]
    public async Task RunQueryAsync_WhenMemberMissing_ReturnsNone()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            typeof(GetOrganizationMemberQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (organizationId, _, _) = await OrganizationTestData.SeedAsync(seedContext);

        var handler = scope.GetHandler<GetOrganizationMemberQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetOrganizationMemberQuery
            {
                OrganizationId = organizationId,
                UserId = UserId.From(Guid.NewGuid()),
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }
}
