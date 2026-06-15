using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Organization;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Organization.QueryHandlers;
using OpenGitBase.Features.Organization.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Organization.Tests.QueryHandlers;

public class AddOrganizationMemberQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenMemberMissing_AddsMember()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            typeof(AddOrganizationMemberQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (organizationId, _, _) = await OrganizationTestData.SeedAsync(seedContext);
        var memberUserId = UserId.From(Guid.NewGuid());
        seedContext.Set<UserEntity>().Add(OrganizationTestData.CreateUser(memberUserId.Value, "member"));
        await seedContext.SaveChangesAsync();

        var handler = scope.GetHandler<AddOrganizationMemberQueryHandler>();
        var result = await handler.RunQueryAsync(
            new AddOrganizationMemberQuery
            {
                OrganizationId = organizationId,
                UserId = memberUserId,
                Role = OrganizationMemberRole.Member,
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertUnit(result);

        await using var verifyContext = await scope.CreateDbContextAsync();
        var member = await verifyContext
            .Set<OrganizationMemberEntity>()
            .SingleAsync(x =>
                x.OrganizationId == organizationId.Value && x.UserId == memberUserId.Value
            );
        Assert.Equal(OrganizationMemberRole.Member, member.Role);
    }

    [Fact]
    public async Task RunQueryAsync_WhenMemberAlreadyExists_ReturnsNone()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            typeof(AddOrganizationMemberQueryHandler).Assembly
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

        var handler = scope.GetHandler<AddOrganizationMemberQueryHandler>();
        var result = await handler.RunQueryAsync(
            new AddOrganizationMemberQuery
            {
                OrganizationId = organizationId,
                UserId = ownerUserId,
                Role = OrganizationMemberRole.Member,
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }
}
