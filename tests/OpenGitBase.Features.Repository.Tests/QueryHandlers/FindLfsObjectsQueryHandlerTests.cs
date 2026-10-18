using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.Repository.QueryHandlers;
using OpenGitBase.Features.Repository.Tests.Testing;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Repository.Tests.QueryHandlers;

public class FindLfsObjectsQueryHandlerTests
{
    private const string RecordedOid = "1111111111111111111111111111111111111111111111111111111111111111";
    private const string UnknownOid = "2222222222222222222222222222222222222222222222222222222222222222";

    [Fact]
    public async Task RunQueryAsync_ReturnsOnlyObjectsRecordedForThatRepository()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var (repositoryId, _) = await RepositoryTestData.SeedPublicRepositoryAsync(context);
        var (otherRepositoryId, _) = await RepositoryTestData.SeedPublicRepositoryAsync(
            context,
            ownerUsername: "other-user",
            slug: "other"
        );
        context.Set<LfsObjectEntity>().Add(NewObject(repositoryId.Value, RecordedOid, 42));
        context.Set<LfsObjectEntity>().Add(NewObject(otherRepositoryId.Value, UnknownOid, 7));
        await context.SaveChangesAsync();
        var handler = new FindLfsObjectsQueryHandler(scope.GetService<IDbContextFactory<OpenGitBaseDbContext>>());

        var result = await handler.RunQueryAsync(
            new FindLfsObjectsQuery { RepositoryId = repositoryId, Oids = [RecordedOid, UnknownOid] },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            result,
            objects =>
            {
                var recorded = Assert.Single(objects);
                Assert.Equal(RecordedOid, recorded.Oid);
                Assert.Equal(42, recorded.Size);
            }
        );
    }

    [Fact]
    public async Task RunQueryAsync_WhenNoOidsAreAsked_ReturnsAnEmptyList()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();
        var handler = new FindLfsObjectsQueryHandler(scope.GetService<IDbContextFactory<OpenGitBaseDbContext>>());

        var result = await handler.RunQueryAsync(
            new FindLfsObjectsQuery { RepositoryId = RepositoryId.From(Guid.NewGuid()), Oids = [] },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(result, Assert.Empty);
    }

    private static InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMapsterConfig> CreateScope() =>
        new(typeof(FindLfsObjectsQueryHandler).Assembly, typeof(UserEntity).Assembly);

    private static LfsObjectEntity NewObject(Guid repositoryId, string oid, long size) =>
        new()
        {
            RepositoryId = repositoryId,
            Oid = oid,
            Size = size,
            CreatedAt = DateTimeOffset.UtcNow,
        };
}
