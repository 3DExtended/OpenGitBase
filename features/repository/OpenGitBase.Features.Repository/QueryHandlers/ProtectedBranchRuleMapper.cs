using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Repository.QueryHandlers;

public static class ProtectedBranchRuleMapper
{
    public static IQueryable<ProtectedBranchRuleEntity> BaseQuery(OpenGitBaseDbContext context) =>
        context
            .Set<ProtectedBranchRuleEntity>()
            .AsNoTracking()
            .Include(item => item.AllowedUsers)
            .Include(item => item.PushRules);

    public static bool IsValidModel(ProtectedBranchRuleDto model) =>
        model is not null
        && model.RepositoryId is not null
        && !string.IsNullOrWhiteSpace(model.Pattern)
        && model.RequiredApprovalCount >= 0
        && model.PushRules.All(rule => !string.IsNullOrWhiteSpace(rule.ConfigJson));

    public static ProtectedBranchRuleEntity ToEntity(ProtectedBranchRuleDto model)
    {
        var entity = new ProtectedBranchRuleEntity
        {
            Id = model.Id.Value == Guid.Empty ? Guid.NewGuid() : model.Id.Value,
            RepositoryId = model.RepositoryId.Value,
            Pattern = model.Pattern.Trim(),
            BlockDirectPush = model.BlockDirectPush,
            AllowedPushRoles = model.AllowedPushRoles,
            RequireMergeRequest = model.RequireMergeRequest,
            RequiredApprovalCount = model.RequiredApprovalCount,
            MergeRoleThreshold = model.MergeRoleThreshold,
            ForcePushPolicy = model.ForcePushPolicy,
            DismissApprovalsOnPush = model.DismissApprovalsOnPush,
            LockedMergeStrategy = model.LockedMergeStrategy,
        };

        ApplyChildren(entity, model);
        return entity;
    }

    public static void ApplyUpdate(ProtectedBranchRuleEntity entity, ProtectedBranchRuleDto model)
    {
        entity.Pattern = model.Pattern.Trim();
        entity.BlockDirectPush = model.BlockDirectPush;
        entity.AllowedPushRoles = model.AllowedPushRoles;
        entity.RequireMergeRequest = model.RequireMergeRequest;
        entity.RequiredApprovalCount = model.RequiredApprovalCount;
        entity.MergeRoleThreshold = model.MergeRoleThreshold;
        entity.ForcePushPolicy = model.ForcePushPolicy;
        entity.DismissApprovalsOnPush = model.DismissApprovalsOnPush;
        entity.LockedMergeStrategy = model.LockedMergeStrategy;
    }

    public static ProtectedBranchRuleDto ToDto(ProtectedBranchRuleEntity entity) =>
        new()
        {
            Id = ProtectedBranchRuleId.From(entity.Id),
            RepositoryId = RepositoryId.From(entity.RepositoryId),
            Pattern = entity.Pattern,
            BlockDirectPush = entity.BlockDirectPush,
            AllowedPushRoles = entity.AllowedPushRoles,
            AllowedPushUserIds = entity
                .AllowedUsers.OrderBy(item => item.UserId)
                .Select(item => UserId.From(item.UserId))
                .ToList(),
            RequireMergeRequest = entity.RequireMergeRequest,
            RequiredApprovalCount = entity.RequiredApprovalCount,
            MergeRoleThreshold = entity.MergeRoleThreshold,
            ForcePushPolicy = entity.ForcePushPolicy,
            DismissApprovalsOnPush = entity.DismissApprovalsOnPush,
            LockedMergeStrategy = entity.LockedMergeStrategy,
            PushRules = entity
                .PushRules.OrderBy(item => item.RuleType).ThenBy(item => item.Id)
                .Select(item => new PushRuleDto
                {
                    Id = PushRuleId.From(item.Id),
                    RuleType = item.RuleType,
                    ConfigJson = item.ConfigJson,
                })
                .ToList(),
        };

    public static void ApplyChildren(ProtectedBranchRuleEntity entity, ProtectedBranchRuleDto model)
    {
        foreach (var userId in model.AllowedPushUserIds.Select(item => item.Value).Distinct())
        {
            entity.AllowedUsers.Add(
                new ProtectedBranchAllowedUserEntity
                {
                    ProtectedBranchRuleId = entity.Id,
                    UserId = userId,
                }
            );
        }

        foreach (var pushRule in model.PushRules)
        {
            entity.PushRules.Add(
                new PushRuleEntity
                {
                    Id = pushRule.Id.Value == Guid.Empty ? Guid.NewGuid() : pushRule.Id.Value,
                    ProtectedBranchRuleId = entity.Id,
                    RuleType = pushRule.RuleType,
                    ConfigJson = pushRule.ConfigJson,
                }
            );
        }
    }
}
