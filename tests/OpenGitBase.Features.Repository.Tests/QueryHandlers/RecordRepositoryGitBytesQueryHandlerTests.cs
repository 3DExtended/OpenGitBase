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

public class RecordRepositoryGitBytesQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenFirstMeasured_AddsGitBytesToWhatTheCounterAlreadyHeld()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var (repositoryId, _) = await RepositoryTestData.SeedPublicRepositoryAsync(context);

        var result = await CreateHandler(scope)
            .RunQueryAsync(NewQuery(repositoryId, 5_000), CancellationToken.None);

        // The seed holds 1024 bytes that were never measured as git data (as LFS bytes are).
        QueryHandlerResultAssert.AssertSome(result, total => Assert.Equal(6_024, total));
        Assert.Equal((6_024, 5_000), await CountersAsync(scope, repositoryId));
    }

    [Fact]
    public async Task RunQueryAsync_WhenMeasuredAgain_ReplacesThePreviousGitBytesOnly()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var (repositoryId, _) = await RepositoryTestData.SeedPublicRepositoryAsync(context);
        var handler = CreateHandler(scope);
        await handler.RunQueryAsync(NewQuery(repositoryId, 5_000), CancellationToken.None);

        var grown = await handler.RunQueryAsync(NewQuery(repositoryId, 8_000), CancellationToken.None);
        QueryHandlerResultAssert.AssertSome(grown, total => Assert.Equal(9_024, total));

        var shrunk = await handler.RunQueryAsync(NewQuery(repositoryId, 2_000), CancellationToken.None);
        QueryHandlerResultAssert.AssertSome(shrunk, total => Assert.Equal(3_024, total));
        Assert.Equal((3_024, 2_000), await CountersAsync(scope, repositoryId));
    }

    [Fact]
    public async Task RunQueryAsync_WhenOtherBytesWereAddedSinceTheLastMeasurement_KeepsThem()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var (repositoryId, _) = await RepositoryTestData.SeedPublicRepositoryAsync(context);
        var handler = CreateHandler(scope);
        await handler.RunQueryAsync(NewQuery(repositoryId, 5_000), CancellationToken.None);

        // What Git LFS verify does: an atomic increment of the counter alone.
        await using (var lfsContext = await scope.CreateDbContextAsync())
        {
            await lfsContext
                .Set<RepositoryEntity>()
                .Where(x => x.Id == repositoryId.Value)
                .ExecuteUpdateAsync(setters =>
                    setters.SetProperty(x => x.StorageBytesUsed, x => x.StorageBytesUsed + 70_000)
                );
        }

        var result = await handler.RunQueryAsync(NewQuery(repositoryId, 6_000), CancellationToken.None);

        QueryHandlerResultAssert.AssertSome(result, total => Assert.Equal(1_024 + 70_000 + 6_000, total));
        Assert.Equal((77_024, 6_000), await CountersAsync(scope, repositoryId));
    }

    [Fact]
    public async Task RunQueryAsync_WhenRepositoryIsMissing_ReturnsNone()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();

        var result = await CreateHandler(scope)
            .RunQueryAsync(NewQuery(RepositoryId.From(Guid.NewGuid()), 5_000), CancellationToken.None);

        QueryHandlerResultAssert.AssertNone(result);
    }

    [Fact]
    public async Task RunQueryAsync_WhenGitBytesAreNegative_ReturnsNoneAndChangesNothing()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var (repositoryId, _) = await RepositoryTestData.SeedPublicRepositoryAsync(context);

        var result = await CreateHandler(scope)
            .RunQueryAsync(NewQuery(repositoryId, -1), CancellationToken.None);

        QueryHandlerResultAssert.AssertNone(result);
        Assert.Equal((1_024, 0), await CountersAsync(scope, repositoryId));
    }

    private static RecordRepositoryGitBytesQueryHandler CreateHandler(
        InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMapsterConfig> scope
    ) => new(scope.GetService<IDbContextFactory<OpenGitBaseDbContext>>());

    private static RecordRepositoryGitBytesQuery NewQuery(RepositoryId repositoryId, long gitBytes) =>
        new() { RepositoryId = repositoryId, GitBytes = gitBytes };

    private static InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMapsterConfig> CreateScope() =>
        new(typeof(RecordRepositoryGitBytesQueryHandler).Assembly, typeof(UserEntity).Assembly);

    private static async Task<(long StorageBytesUsed, long GitBytesUsed)> CountersAsync(
        InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMapsterConfig> scope,
        RepositoryId repositoryId
    )
    {
        await using var context = await scope.CreateDbContextAsync();
        var row = await context
            .Set<RepositoryEntity>()
            .Where(x => x.Id == repositoryId.Value)
            .Select(x => new { x.StorageBytesUsed, x.GitBytesUsed })
            .SingleAsync();
        return (row.StorageBytesUsed, row.GitBytesUsed);
    }
}
