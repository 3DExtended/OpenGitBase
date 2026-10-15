using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.Repository.QueryHandlers;
using OpenGitBase.Features.Repository.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Repository.Tests.QueryHandlers;

public class RecordLfsObjectQueryHandlerTests
{
    private const string Oid = "3333333333333333333333333333333333333333333333333333333333333333";

    [Fact]
    public async Task RunQueryAsync_WhenObjectIsNew_RecordsItOnce()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var (repositoryId, ownerUserId) = await RepositoryTestData.SeedPublicRepositoryAsync(context);
        var handler = CreateHandler(scope);

        var result = await handler.RunQueryAsync(NewQuery(repositoryId, ownerUserId), CancellationToken.None);

        QueryHandlerResultAssert.AssertSome(result, recorded => Assert.True(recorded.Created));
        await using var verifyContext = await scope.CreateDbContextAsync();
        var row = await verifyContext.Set<LfsObjectEntity>().SingleAsync();
        Assert.Equal(repositoryId.Value, row.RepositoryId);
        Assert.Equal(Oid, row.Oid);
        Assert.Equal(5_000_000, row.Size);
        Assert.Equal(ownerUserId.Value, row.CreatedByUserId);
    }

    [Fact]
    public async Task RunQueryAsync_WhenObjectIsAlreadyRecorded_SucceedsWithoutASecondRow()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var (repositoryId, ownerUserId) = await RepositoryTestData.SeedPublicRepositoryAsync(context);
        var handler = CreateHandler(scope);
        await handler.RunQueryAsync(NewQuery(repositoryId, ownerUserId), CancellationToken.None);

        var second = await handler.RunQueryAsync(NewQuery(repositoryId, ownerUserId), CancellationToken.None);

        QueryHandlerResultAssert.AssertSome(second, recorded => Assert.False(recorded.Created));
        await using var verifyContext = await scope.CreateDbContextAsync();
        Assert.Equal(1, await verifyContext.Set<LfsObjectEntity>().CountAsync());
    }

    [Fact]
    public async Task RunQueryAsync_WhenRepositoryDoesNotExist_ReturnsNone()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();
        var handler = CreateHandler(scope);

        var result = await handler.RunQueryAsync(
            NewQuery(RepositoryId.From(Guid.NewGuid()), UserId.From(Guid.NewGuid())),
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }

    private static RecordLfsObjectQueryHandler CreateHandler(
        InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMapsterConfig> scope
    ) => new(scope.GetService<IDbContextFactory<OpenGitBaseDbContext>>());

    private static RecordLfsObjectQuery NewQuery(RepositoryId repositoryId, UserId userId) =>
        new()
        {
            RepositoryId = repositoryId,
            Oid = Oid,
            Size = 5_000_000,
            KeyVersion = 0,
            CreatedByUserId = userId,
        };

    private static InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMapsterConfig> CreateScope() =>
        new(typeof(RecordLfsObjectQueryHandler).Assembly, typeof(UserEntity).Assembly);
}
