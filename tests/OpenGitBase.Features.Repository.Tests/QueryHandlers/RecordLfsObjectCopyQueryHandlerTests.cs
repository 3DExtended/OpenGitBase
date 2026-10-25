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

public class RecordLfsObjectCopyQueryHandlerTests
{
    private const string Oid = "4444444444444444444444444444444444444444444444444444444444444444";

    [Fact]
    public async Task RunQueryAsync_AddsACopyOfARecordedObjectOnce()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();
        var repositoryId = await SeedRecordedObjectAsync(scope);
        var node = Guid.NewGuid();
        var handler = new RecordLfsObjectCopyQueryHandler(scope.GetService<IDbContextFactory<OpenGitBaseDbContext>>());

        var first = await handler.RunQueryAsync(Copy(repositoryId, node), CancellationToken.None);
        var second = await handler.RunQueryAsync(Copy(repositoryId, node), CancellationToken.None);

        QueryHandlerResultAssert.AssertSome(first, result => Assert.True(result.Created));
        QueryHandlerResultAssert.AssertSome(second, result => Assert.False(result.Created));
        await using var context = await scope.CreateDbContextAsync();
        var copy = await context.Set<LfsObjectCopyEntity>().SingleAsync();
        Assert.Equal((node, LfsObjectCopyKind.Encrypted), (copy.StorageNodeId, copy.Kind));
    }

    [Fact]
    public async Task RunQueryAsync_ForAnObjectThatIsNotRecorded_ReturnsNone()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();
        await using var seedContext = await scope.CreateDbContextAsync();
        var (repositoryId, _) = await RepositoryTestData.SeedPublicRepositoryAsync(seedContext);
        var handler = new RecordLfsObjectCopyQueryHandler(scope.GetService<IDbContextFactory<OpenGitBaseDbContext>>());

        var result = await handler.RunQueryAsync(Copy(repositoryId, Guid.NewGuid()), CancellationToken.None);

        QueryHandlerResultAssert.AssertNone(result);
    }

    private static RecordLfsObjectCopyQuery Copy(RepositoryId repositoryId, Guid node) =>
        new()
        {
            RepositoryId = repositoryId,
            Oid = Oid,
            StorageNodeId = node,
            Kind = LfsObjectCopyKind.Encrypted,
        };

    private static async Task<RepositoryId> SeedRecordedObjectAsync(
        InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMapsterConfig> scope
    )
    {
        await using var context = await scope.CreateDbContextAsync();
        var (repositoryId, _) = await RepositoryTestData.SeedPublicRepositoryAsync(context);
        context
            .Set<LfsObjectEntity>()
            .Add(new LfsObjectEntity { RepositoryId = repositoryId.Value, Oid = Oid, Size = 10, KeyVersion = 1 });
        await context.SaveChangesAsync();
        return repositoryId;
    }

    private static InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMapsterConfig> CreateScope() =>
        new(typeof(RecordLfsObjectCopyQueryHandler).Assembly, typeof(UserEntity).Assembly);
}
