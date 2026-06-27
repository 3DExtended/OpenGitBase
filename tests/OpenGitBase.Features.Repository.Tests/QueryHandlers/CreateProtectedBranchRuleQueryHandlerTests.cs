using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.Repository.QueryHandlers;

namespace OpenGitBase.Features.Repository.Tests.QueryHandlers;

public class CreateProtectedBranchRuleQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_PersistsNestedSettings()
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

        var handler = scope.ServiceProvider.GetRequiredService<CreateProtectedBranchRuleQueryHandler>();
        var createResult = await handler.RunQueryAsync(
            new CreateProtectedBranchRuleQuery
            {
                ModelToCreate = ProtectedBranchRuleQueryTestSetup.CreateRuleModel(
                    RepositoryId.From(repositoryId)
                ),
            },
            CancellationToken.None
        );

        Assert.True(createResult.IsSome);

        await using var readContext = await contextFactory.CreateDbContextAsync();
        var saved = await readContext
            .Set<ProtectedBranchRuleEntity>()
            .Include(item => item.AllowedUsers)
            .Include(item => item.PushRules)
            .FirstOrDefaultAsync(item => item.Id == createResult.Get().Value);
        Assert.NotNull(saved);
        Assert.Equal("@default", saved.Pattern);
        Assert.Equal(2, saved.AllowedUsers.Count);
        Assert.Equal(2, saved.PushRules.Count);
    }
}
