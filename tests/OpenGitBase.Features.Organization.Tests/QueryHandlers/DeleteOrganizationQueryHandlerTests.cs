using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Organization;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Organization.QueryHandlers;
using OpenGitBase.Features.Organization.Tests.Testing;

namespace OpenGitBase.Features.Organization.Tests.QueryHandlers;

public class DeleteOrganizationQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEntityExists_DeletesAndReturnsUnit()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            typeof(DeleteOrganizationQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (id, _, _) = await OrganizationTestData.SeedAsync(seedContext);

        var handler = scope.GetHandler<DeleteOrganizationQueryHandler>();
        var result = await handler.RunQueryAsync(
            new DeleteOrganizationQuery { Id = id },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertUnit(result);

        await using var verifyContext = await scope.CreateDbContextAsync();
        Assert.Empty(verifyContext.Set<OrganizationEntity>());
    }

    [Fact]
    public async Task RunQueryAsync_WhenEntityMissing_ReturnsNone()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            typeof(DeleteOrganizationQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        var handler = scope.GetHandler<DeleteOrganizationQueryHandler>();
        var result = await handler.RunQueryAsync(
            new DeleteOrganizationQuery { Id = OrganizationId.From(Guid.NewGuid()) },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }
}
