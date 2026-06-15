using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Organization;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Organization.QueryHandlers;
using OpenGitBase.Features.Organization.Tests.Testing;

namespace OpenGitBase.Features.Organization.Tests.QueryHandlers;

public class UpdateOrganizationQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEntityExists_UpdatesAndReturnsUnit()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            typeof(UpdateOrganizationQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (id, entity, ownerUserId) = await OrganizationTestData.SeedAsync(seedContext);

        var handler = scope.GetHandler<UpdateOrganizationQueryHandler>();
        var result = await handler.RunQueryAsync(
            new UpdateOrganizationQuery
            {
                UpdatedModel = new OrganizationDto
                {
                    Id = id,
                    Name = OrganizationTestData.UpdatedName,
                    Slug = entity.Slug,
                    OwnerUserId = ownerUserId.Value,
                },
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertUnit(result);

        await using var verifyContext = await scope.CreateDbContextAsync();
        var updatedEntity = await verifyContext.Set<OrganizationEntity>().FindAsync(id.Value);
        Assert.NotNull(updatedEntity);
        Assert.Equal(OrganizationTestData.UpdatedName, updatedEntity.Name);
    }

    [Fact]
    public async Task RunQueryAsync_WhenEntityMissing_ReturnsNone()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            typeof(UpdateOrganizationQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        var handler = scope.GetHandler<UpdateOrganizationQueryHandler>();
        var result = await handler.RunQueryAsync(
            new UpdateOrganizationQuery
            {
                UpdatedModel = new OrganizationDto
                {
                    Id = OrganizationId.From(Guid.NewGuid()),
                    Name = OrganizationTestData.UpdatedName,
                },
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }
}
