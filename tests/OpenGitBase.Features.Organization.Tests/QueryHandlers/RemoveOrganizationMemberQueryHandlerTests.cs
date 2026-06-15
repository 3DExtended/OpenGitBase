using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Organization;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Organization.QueryHandlers;
using OpenGitBase.Features.Organization.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Organization.Tests.QueryHandlers;

public class RemoveOrganizationMemberQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenMemberExists_RemovesMember()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            typeof(RemoveOrganizationMemberQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (organizationId, _, _) = await OrganizationTestData.SeedAsync(seedContext);
        var memberUserId = UserId.From(Guid.NewGuid());
        await OrganizationTestData.SeedMemberAsync(
            seedContext,
            organizationId,
            memberUserId,
            OrganizationMemberRole.Member
        );

        var handler = scope.GetHandler<RemoveOrganizationMemberQueryHandler>();
        var result = await handler.RunQueryAsync(
            new RemoveOrganizationMemberQuery
            {
                OrganizationId = organizationId,
                UserId = memberUserId,
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertUnit(result);

        await using var verifyContext = await scope.CreateDbContextAsync();
        var remaining = await verifyContext
            .Set<OrganizationMemberEntity>()
            .CountAsync(x => x.OrganizationId == organizationId.Value && x.UserId == memberUserId.Value);
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task RunQueryAsync_WhenMemberMissing_ReturnsNone()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            typeof(RemoveOrganizationMemberQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (organizationId, _, _) = await OrganizationTestData.SeedAsync(seedContext);

        var handler = scope.GetHandler<RemoveOrganizationMemberQueryHandler>();
        var result = await handler.RunQueryAsync(
            new RemoveOrganizationMemberQuery
            {
                OrganizationId = organizationId,
                UserId = UserId.From(Guid.NewGuid()),
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }
}
