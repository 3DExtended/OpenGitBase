using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.Repository.QueryHandlers;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Repository.Tests.QueryHandlers;

public class UpdateProtectedBranchRuleQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_ReplacesUsersAndPushRules()
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
        var existingRuleId = await ProtectedBranchRuleQueryTestSetup.SeedRuleAsync(
            contextFactory,
            RepositoryId.From(repositoryId)
        );

        var handler = scope.ServiceProvider.GetRequiredService<UpdateProtectedBranchRuleQueryHandler>();
        var result = await handler.RunQueryAsync(
            new UpdateProtectedBranchRuleQuery
            {
                UpdatedModel = new ProtectedBranchRuleDto
                {
                    Id = existingRuleId,
                    RepositoryId = RepositoryId.From(repositoryId),
                    Pattern = "main",
                    BlockDirectPush = true,
                    AllowedPushRoles = AllowedPushRoles.Admin,
                    AllowedPushUserIds = [UserId.From(Guid.NewGuid())],
                    RequireMergeRequest = true,
                    RequiredApprovalCount = 3,
                    MergeRoleThreshold = 3,
                    ForcePushPolicy = ForcePushPolicy.PlatformOnly,
                    DismissApprovalsOnPush = true,
                    LockedMergeStrategy = LockedMergeStrategy.Squash,
                    PushRules =
                    [
                        new PushRuleDto
                        {
                            RuleType = PushRuleType.CommitMessageRegex,
                            ConfigJson = "{\"regex\":\"^ABC-\\\\d+\"}",
                        },
                    ],
                },
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);

        await using var readContext = await contextFactory.CreateDbContextAsync();
        var saved = await readContext
            .Set<ProtectedBranchRuleEntity>()
            .Include(item => item.AllowedUsers)
            .Include(item => item.PushRules)
            .FirstAsync(item => item.Id == existingRuleId.Value);
        Assert.Equal("main", saved.Pattern);
        Assert.Single(saved.AllowedUsers);
        Assert.Single(saved.PushRules);
        Assert.Equal(PushRuleType.CommitMessageRegex, saved.PushRules.Single().RuleType);
    }
}
