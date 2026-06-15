using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Organization;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Organization.QueryHandlers;
using OpenGitBase.Features.Organization.Tests.Testing;

namespace OpenGitBase.Features.Organization.Tests.QueryHandlers;

public class CreateOrganizationQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_PersistsEntity_ReturnsNewId()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            typeof(CreateOrganizationQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        var ownerUserId = Guid.NewGuid();
        await using (var seedContext = await scope.CreateDbContextAsync())
        {
            seedContext
                .Set<OpenGitBase.Features.Users.Entities.UserEntity>()
                .Add(OrganizationTestData.CreateUser(ownerUserId));
            await seedContext.SaveChangesAsync();
        }

        var handler = scope.GetHandler<CreateOrganizationQueryHandler>();
        var result = await handler.RunQueryAsync(
            new CreateOrganizationQuery
            {
                ModelToCreate = new OrganizationDto
                {
                    Name = OrganizationTestData.SampleName,
                    Slug = OrganizationTestData.SampleSlug,
                    OwnerUserId = ownerUserId,
                },
                CreatorUserId = ownerUserId,
            },
            CancellationToken.None
        );

        var id = QueryHandlerResultAssert.AssertSome(result);
        QueryHandlerResultAssert.AssertIdentifierNonEmpty(id);

        await using var context = await scope.CreateDbContextAsync();
        var entity = await context.Set<OrganizationEntity>().FindAsync(id.Value);
        Assert.NotNull(entity);
        Assert.Equal(id.Value, entity.Id);
        Assert.Equal(OrganizationTestData.SampleName, entity.Name);
    }
}
