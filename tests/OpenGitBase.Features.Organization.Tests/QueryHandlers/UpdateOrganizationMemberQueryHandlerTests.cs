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

public class UpdateOrganizationMemberQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEntityExists_UpdatesAndReturnsUnit()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (organizationId, _, ownerUserId) = await OrganizationTestData.SeedAsync(seedContext);
        await OrganizationTestData.SeedMemberAsync(
            seedContext,
            organizationId,
            ownerUserId,
            OrganizationMemberRole.Member
        );

        var member = await seedContext
            .Set<OrganizationMemberEntity>()
            .FirstAsync(x => x.UserId == ownerUserId.Value);

        var handler = scope.GetHandler<UpdateOrganizationMemberQueryHandler>();
        var result = await handler.RunQueryAsync(
            new UpdateOrganizationMemberQuery
            {
                UpdatedModel = new OrganizationMemberDto
                {
                    Id = OrganizationMemberId.From(member.Id),
                    OrganizationId = organizationId,
                    UserId = ownerUserId,
                    Role = OrganizationMemberRole.Owner,
                },
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertUnit(result);

        await using var verifyContext = await scope.CreateDbContextAsync();
        var entity = await verifyContext
            .Set<OrganizationMemberEntity>()
            .FindAsync(member.Id);
        Assert.NotNull(entity);
        Assert.Equal(OrganizationMemberRole.Owner, entity.Role);
    }

    [Fact]
    public async Task RunQueryAsync_WhenEntityMissing_ReturnsNone()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();

        var handler = scope.GetHandler<UpdateOrganizationMemberQueryHandler>();
        var result = await handler.RunQueryAsync(
            new UpdateOrganizationMemberQuery
            {
                UpdatedModel = new OrganizationMemberDto
                {
                    Id = OrganizationMemberId.From(Guid.NewGuid()),
                    OrganizationId = OrganizationId.From(Guid.NewGuid()),
                    UserId = UserId.From(Guid.NewGuid()),
                    Role = OrganizationMemberRole.Owner,
                },
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }

    private static InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig> CreateScope() =>
        new(typeof(UpdateOrganizationMemberQueryHandler).Assembly);
}
