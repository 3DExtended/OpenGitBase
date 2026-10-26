using Microsoft.EntityFrameworkCore;
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

    [Fact]
    public async Task RunQueryAsync_CreatesTheOrganizationOnceWhenTheCommitAcknowledgementIsLost()
    {
        var lostCommit = new LostCommitSimulator();
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            lostCommit.Configure,
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

        lostCommit.LoseNextCommitInserting("Organization");
        var result = await scope
            .GetHandler<CreateOrganizationQueryHandler>()
            .RunQueryAsync(
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

        Assert.Equal(1, lostCommit.LostCommits);
        var id = QueryHandlerResultAssert.AssertSome(result);
        await using var context = await scope.CreateDbContextAsync();
        var organization = Assert.Single(await context.Set<OrganizationEntity>().ToListAsync());
        Assert.Equal(id.Value, organization.Id);
        var owner = Assert.Single(await context.Set<OrganizationMemberEntity>().ToListAsync());
        Assert.Equal(OrganizationMemberRole.Owner, owner.Role);
    }
}
