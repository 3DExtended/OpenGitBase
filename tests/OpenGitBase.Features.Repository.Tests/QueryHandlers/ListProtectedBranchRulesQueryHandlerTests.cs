using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.QueryHandlers;

namespace OpenGitBase.Features.Repository.Tests.QueryHandlers;

public class ListProtectedBranchRulesQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_ListsRulesForRepositoryOnly()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = ProtectedBranchRuleQueryTestSetup.BuildServices(connection);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();

        var repositoryA = Guid.NewGuid();
        var repositoryB = Guid.NewGuid();
        await ProtectedBranchRuleQueryTestSetup.SeedRepositoryAsync(contextFactory, repositoryA);
        await ProtectedBranchRuleQueryTestSetup.SeedRepositoryAsync(contextFactory, repositoryB);
        await ProtectedBranchRuleQueryTestSetup.SeedRuleAsync(
            contextFactory,
            RepositoryId.From(repositoryA)
        );
        await ProtectedBranchRuleQueryTestSetup.SeedRuleAsync(
            contextFactory,
            RepositoryId.From(repositoryB),
            "release/*"
        );

        var handler = scope.ServiceProvider.GetRequiredService<ListProtectedBranchRulesQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListProtectedBranchRulesQuery { RepositoryId = RepositoryId.From(repositoryA) },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Single(result.Get());
        Assert.Equal("@default", result.Get()[0].Pattern);
    }
}
