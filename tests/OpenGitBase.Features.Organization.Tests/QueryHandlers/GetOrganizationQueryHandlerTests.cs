using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Organization;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.QueryHandlers;
using OpenGitBase.Features.Organization.Tests.Testing;

namespace OpenGitBase.Features.Organization.Tests.QueryHandlers;

public class GetOrganizationQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEntityExists_ReturnsMappedDto()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            typeof(GetOrganizationQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (id, _, _) = await OrganizationTestData.SeedAsync(seedContext);

        var handler = scope.GetHandler<GetOrganizationQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetOrganizationQuery { ModelId = id },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            result,
            dto =>
            {
                Assert.Equal(id, dto.Id);
                Assert.Equal(OrganizationTestData.SampleName, dto.Name);
            }
        );
    }

    [Fact]
    public async Task RunQueryAsync_WhenEntityMissing_ReturnsNone()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, OrganizationMapsterConfig>(
            typeof(GetOrganizationQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        var handler = scope.GetHandler<GetOrganizationQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetOrganizationQuery { ModelId = OrganizationId.From(Guid.NewGuid()) },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }
}
