using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.Repository.QueryHandlers;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Repository.Tests.QueryHandlers;

internal static class ProtectedBranchRuleQueryTestSetup
{
    public static ServiceCollection BuildServices(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(RepositoryMapsterConfig).Assembly])
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        services.AddLogging();
        var mapsterConfig = new TypeAdapterConfig();
        new RepositoryMapsterConfig().Register(mapsterConfig);
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(
            sp.GetRequiredService<TypeAdapterConfig>()
        ));
        services.AddCqrs(options =>
            options.WithQueryHandlersFrom(typeof(CreateProtectedBranchRuleQueryHandler).Assembly)
        );
        return services;
    }

    public static async Task SeedRepositoryAsync(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        Guid repositoryId
    )
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
        context.Set<RepositoryEntity>().Add(
            new RepositoryEntity
            {
                Id = repositoryId,
                Name = "Repo",
                Slug = $"repo-{repositoryId:N}",
                OwnerUserId = Guid.NewGuid(),
                PhysicalPath = $"/tmp/{repositoryId:N}.git",
            }
        );
        await context.SaveChangesAsync();
    }

    public static async Task<ProtectedBranchRuleId> SeedRuleAsync(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        RepositoryId repositoryId,
        string pattern = "@default"
    )
    {
        var model = CreateRuleModel(repositoryId);
        model.Pattern = pattern;

        await using var context = await contextFactory.CreateDbContextAsync();
        var ruleId = Guid.NewGuid();
        var entity = new ProtectedBranchRuleEntity
        {
            Id = ruleId,
            RepositoryId = model.RepositoryId.Value,
            Pattern = model.Pattern,
            BlockDirectPush = model.BlockDirectPush,
            AllowedPushRoles = model.AllowedPushRoles,
            RequireMergeRequest = model.RequireMergeRequest,
            RequiredApprovalCount = model.RequiredApprovalCount,
            MergeRoleThreshold = model.MergeRoleThreshold,
            ForcePushPolicy = model.ForcePushPolicy,
            DismissApprovalsOnPush = model.DismissApprovalsOnPush,
            LockedMergeStrategy = model.LockedMergeStrategy,
            AllowedUsers = model
                .AllowedPushUserIds.Select(id => new ProtectedBranchAllowedUserEntity
                {
                    ProtectedBranchRuleId = ruleId,
                    UserId = id.Value,
                })
                .ToList(),
            PushRules = model
                .PushRules.Select(rule => new PushRuleEntity
                {
                    Id = Guid.NewGuid(),
                    ProtectedBranchRuleId = ruleId,
                    RuleType = rule.RuleType,
                    ConfigJson = rule.ConfigJson,
                })
                .ToList(),
        };

        context.Set<ProtectedBranchRuleEntity>().Add(entity);
        await context.SaveChangesAsync();
        return ProtectedBranchRuleId.From(entity.Id);
    }

    public static ProtectedBranchRuleDto CreateRuleModel(RepositoryId repositoryId) =>
        new()
        {
            RepositoryId = repositoryId,
            Pattern = "@default",
            BlockDirectPush = true,
            AllowedPushRoles = AllowedPushRoles.Admin | AllowedPushRoles.Owner,
            AllowedPushUserIds = [UserId.From(Guid.NewGuid()), UserId.From(Guid.NewGuid())],
            RequireMergeRequest = true,
            RequiredApprovalCount = 1,
            MergeRoleThreshold = 2,
            ForcePushPolicy = ForcePushPolicy.AllowAllowedPushers,
            DismissApprovalsOnPush = true,
            LockedMergeStrategy = LockedMergeStrategy.MergeCommit,
            PushRules =
            [
                new PushRuleDto
                {
                    RuleType = PushRuleType.MaxFileSize,
                    ConfigJson = "{\"maxBytes\":1048576}",
                },
                new PushRuleDto
                {
                    RuleType = PushRuleType.RequireDco,
                    ConfigJson = "{\"required\":true}",
                },
            ],
        };
}
