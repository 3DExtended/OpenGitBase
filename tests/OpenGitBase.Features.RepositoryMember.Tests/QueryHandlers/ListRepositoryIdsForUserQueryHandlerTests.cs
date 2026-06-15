using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.RepositoryMember;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.RepositoryMember.QueryHandlers;
using OpenGitBase.Features.RepositoryMember.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.RepositoryMember.Tests.QueryHandlers;

public class ListRepositoryIdsForUserQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_ReturnsRepositoryIdsForMember()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMemberMapsterConfig>(
            typeof(ListRepositoryIdsForUserQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var seed = await RepositoryMemberTestData.SeedRepositoryWithMemberAsync(seedContext);

        var handler = scope.GetHandler<ListRepositoryIdsForUserQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListRepositoryIdsForUserQuery { UserId = seed.MemberUserId },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            result,
            ids =>
            {
                var id = Assert.Single(ids);
                Assert.Equal(seed.RepositoryId.Value, id);
            }
        );
    }
}
