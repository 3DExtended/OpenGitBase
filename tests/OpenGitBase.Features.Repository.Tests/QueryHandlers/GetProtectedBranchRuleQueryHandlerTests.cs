using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.QueryHandlers;

namespace OpenGitBase.Features.Repository.Tests.QueryHandlers;

public class GetProtectedBranchRuleQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_ReturnsRuleWithPushRules()
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

        var handler = scope.ServiceProvider.GetRequiredService<GetProtectedBranchRuleQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetProtectedBranchRuleQuery { ModelId = ruleId },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(ruleId, result.Get().Id);
        Assert.Contains(
            result.Get().PushRules,
            rule => rule.RuleType == PushRuleType.RequireDco
        );
    }
}
