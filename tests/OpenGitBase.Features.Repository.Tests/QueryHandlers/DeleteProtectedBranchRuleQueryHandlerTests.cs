using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.Repository.QueryHandlers;

namespace OpenGitBase.Features.Repository.Tests.QueryHandlers;

public class DeleteProtectedBranchRuleQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_DeletesRule()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = ProtectedBranchRuleQueryTestSetup.BuildServices(connection);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();

        var repositoryId = Guid.NewGuid();
        await ProtectedBranchRuleQueryTestSetup.SeedRepositoryAsync(contextFactory, repositoryId);
        var ruleId = await ProtectedBranchRuleQueryTestSetup.SeedRuleAsync(
            contextFactory,
            RepositoryId.From(repositoryId)
        );

        var handler = scope.ServiceProvider.GetRequiredService<DeleteProtectedBranchRuleQueryHandler>();
        var result = await handler.RunQueryAsync(
            new DeleteProtectedBranchRuleQuery
            {
                RepositoryId = RepositoryId.From(repositoryId),
                Id = ruleId,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);

        await using var readContext = await contextFactory.CreateDbContextAsync();
        var exists = await readContext
            .Set<ProtectedBranchRuleEntity>()
            .AnyAsync(item => item.Id == ruleId.Value);
        Assert.False(exists);
    }

    [Fact]
    public async Task RunQueryAsync_WhenRuleBelongsToAnotherRepository_KeepsRule()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = ProtectedBranchRuleQueryTestSetup.BuildServices(connection);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();

        var callerRepositoryId = Guid.NewGuid();
        var otherRepositoryId = Guid.NewGuid();
        await ProtectedBranchRuleQueryTestSetup.SeedRepositoryAsync(contextFactory, callerRepositoryId);
        await ProtectedBranchRuleQueryTestSetup.SeedRepositoryAsync(contextFactory, otherRepositoryId);
        var otherRuleId = await ProtectedBranchRuleQueryTestSetup.SeedRuleAsync(
            contextFactory,
            RepositoryId.From(otherRepositoryId)
        );

        var handler = scope.ServiceProvider.GetRequiredService<DeleteProtectedBranchRuleQueryHandler>();
        var result = await handler.RunQueryAsync(
            new DeleteProtectedBranchRuleQuery
            {
                RepositoryId = RepositoryId.From(callerRepositoryId),
                Id = otherRuleId,
            },
            CancellationToken.None
        );

        Assert.True(result.IsNone);

        await using var readContext = await contextFactory.CreateDbContextAsync();
        var exists = await readContext
            .Set<ProtectedBranchRuleEntity>()
            .AnyAsync(item => item.Id == otherRuleId.Value);
        Assert.True(exists);
    }
}
