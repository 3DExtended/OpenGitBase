using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Organization;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.QueryHandlers;
using OpenGitBase.Features.Organization.Tests.Testing;

namespace OpenGitBase.Features.Organization.Tests.QueryHandlers;

public class ListOrganizationQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEmpty_ReturnsEmptyList()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            typeof(ListOrganizationQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        var handler = scope.GetHandler<ListOrganizationQueryHandler>();
        var result = await handler.RunQueryAsync(new ListOrganizationQuery(), CancellationToken.None);

        QueryHandlerResultAssert.AssertSome(result, list => Assert.Empty(list));
    }

    [Fact]
    public async Task RunQueryAsync_WhenSeeded_ReturnsAllItems()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            typeof(ListOrganizationQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (id, _, _) = await OrganizationTestData.SeedAsync(seedContext);

        var handler = scope.GetHandler<ListOrganizationQueryHandler>();
        var result = await handler.RunQueryAsync(new ListOrganizationQuery(), CancellationToken.None);

        QueryHandlerResultAssert.AssertSome(
            result,
            list =>
            {
                var item = Assert.Single(list);
                Assert.Equal(id, item.Id);
                Assert.Equal(OrganizationTestData.SampleName, item.Name);
            }
        );
    }

    [Fact]
    public async Task RunQueryAsync_WhenPartialIdSet_ReturnsNone()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            typeof(ListOrganizationQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (id, _, _) = await OrganizationTestData.SeedAsync(seedContext);

        var handler = scope.GetHandler<ListOrganizationQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListOrganizationQuery
            {
                Ids = new[] { id, OrganizationId.From(Guid.NewGuid()) },
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }
}
