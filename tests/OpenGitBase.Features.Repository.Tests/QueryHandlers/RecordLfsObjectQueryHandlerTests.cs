using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
    public async Task RunQueryAsync_WhenObjectIsNew_AddsItsSizeToTheRepositoryUsage()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var (repositoryId, ownerUserId) = await RepositoryTestData.SeedPublicRepositoryAsync(context);
        var before = await StorageBytesUsedAsync(scope, repositoryId);

        await CreateHandler(scope).RunQueryAsync(NewQuery(repositoryId, ownerUserId), CancellationToken.None);

        Assert.Equal(before + 5_000_000, await StorageBytesUsedAsync(scope, repositoryId));
    }

    [Fact]
    public async Task RunQueryAsync_WhenVerifyIsRetried_AddsTheSizeOnlyOnce()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var (repositoryId, ownerUserId) = await RepositoryTestData.SeedPublicRepositoryAsync(context);
        var before = await StorageBytesUsedAsync(scope, repositoryId);
        var handler = CreateHandler(scope);

        await handler.RunQueryAsync(NewQuery(repositoryId, ownerUserId), CancellationToken.None);
        await handler.RunQueryAsync(NewQuery(repositoryId, ownerUserId), CancellationToken.None);

        Assert.Equal(before + 5_000_000, await StorageBytesUsedAsync(scope, repositoryId));
    }

    [Fact]
    public async Task RunQueryAsync_WhenAConcurrentVerifyRecordsTheObjectFirst_AddsTheSizeOnlyOnce()
    {
        await using var scope = CreateScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var (repositoryId, ownerUserId) = await RepositoryTestData.SeedPublicRepositoryAsync(context);
        var before = await StorageBytesUsedAsync(scope, repositoryId);
        var competitor = CreateHandler(scope);
        var racingFactory = new InterceptedContextFactory(
            scope,
            new BeforeTransactionInterceptor(() =>
                competitor.RunQueryAsync(NewQuery(repositoryId, ownerUserId), CancellationToken.None)
            )
        );

        var result = await new RecordLfsObjectQueryHandler(racingFactory)
            .RunQueryAsync(NewQuery(repositoryId, ownerUserId), CancellationToken.None);

        QueryHandlerResultAssert.AssertSome(result, recorded => Assert.False(recorded.Created));
        await using var verifyContext = await scope.CreateDbContextAsync();
        Assert.Equal(1, await verifyContext.Set<LfsObjectEntity>().CountAsync());
        Assert.Equal(before + 5_000_000, await StorageBytesUsedAsync(scope, repositoryId));
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

    private static async Task<long> StorageBytesUsedAsync(
        InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMapsterConfig> scope,
        RepositoryId repositoryId
    )
    {
        await using var context = await scope.CreateDbContextAsync();
        return await context
            .Set<RepositoryEntity>()
            .Where(x => x.Id == repositoryId.Value)
            .Select(x => x.StorageBytesUsed)
            .SingleAsync();
    }

    /// <summary>Contexts on the scope's database with an extra interceptor, to stage a race deterministically.</summary>
    private sealed class InterceptedContextFactory : IDbContextFactory<OpenGitBaseDbContext>
    {
        private readonly InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMapsterConfig> _scope;
        private readonly IInterceptor _interceptor;

        public InterceptedContextFactory(
            InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMapsterConfig> scope,
            IInterceptor interceptor
        )
        {
            _scope = scope;
            _interceptor = interceptor;
        }

        public OpenGitBaseDbContext CreateDbContext() =>
            new(
                new DbContextOptionsBuilder<OpenGitBaseDbContext>(
                    _scope.GetService<DbContextOptions<OpenGitBaseDbContext>>()
                )
                    .AddInterceptors(_interceptor)
                    .Options,
                _scope.GetService<IFeatureAssemblyProvider>()
            );
    }

    /// <summary>Runs another verify of the same object after this one checked for it but before it inserts.</summary>
    private sealed class BeforeTransactionInterceptor : DbTransactionInterceptor
    {
        private readonly Func<Task> _competitor;
        private bool _fired;

        public BeforeTransactionInterceptor(Func<Task> competitor)
        {
            _competitor = competitor;
        }

        public override async ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
            DbConnection connection,
            TransactionStartingEventData eventData,
            InterceptionResult<DbTransaction> result,
            CancellationToken cancellationToken = default
        )
        {
            if (!_fired)
            {
                _fired = true;
                await _competitor();
            }

            return result;
        }
    }
}
