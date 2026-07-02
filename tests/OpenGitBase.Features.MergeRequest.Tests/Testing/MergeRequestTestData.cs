using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.Discussion.Entities;
using OpenGitBase.Features.MergeRequest;
using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.MergeRequest.Entities;
using OpenGitBase.Features.MergeRequest.QueryHandlers;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.MergeRequest.Tests.Testing;

public static class MergeRequestTestData
{
    public const string Title = "Add feature";
    public const string UpdatedTitle = "Add feature (revised)";
    public const string SourceRef = "feature/auth";
    public const string TargetRef = "main";
    public const string SourceHeadSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    public const string TargetBaseSha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    public static readonly Guid RepositoryId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    public static readonly UserId CreatorUserId = UserId.From(Guid.Parse("11111111-2222-3333-4444-555555555555"));
    public static readonly UserId ApproverUserId = UserId.From(Guid.Parse("22222222-3333-4444-5555-666666666666"));

    public static CreateMergeRequestQuery CreateQuery(bool isDraft = false) =>
        new()
        {
            RepositoryId = RepositoryId,
            CreatorUserId = CreatorUserId,
            Title = Title,
            Body = "Description",
            SourceRef = SourceRef,
            TargetRef = TargetRef,
            SourceHeadSha = SourceHeadSha,
            TargetBaseSha = TargetBaseSha,
            IsDraft = isDraft,
        };

    public static async Task<(MergeRequestId Id, MergeRequestEntity Entity)> SeedAsync(
        OpenGitBaseDbContext context,
        int number = 1,
        MergeRequestStatus status = MergeRequestStatus.Open,
        string sourceRef = SourceRef,
        string targetRef = TargetRef,
        DateTimeOffset? updatedAt = null
    )
    {
        var id = Guid.NewGuid();
        var utcNow = updatedAt ?? DateTimeOffset.UtcNow;
        var entity = new MergeRequestEntity
        {
            Id = id,
            RepositoryId = RepositoryId,
            Number = number,
            Title = Title,
            Body = "Seeded body",
            Status = (int)status,
            IsDraft = status == MergeRequestStatus.Draft,
            SourceRef = sourceRef,
            TargetRef = targetRef,
            SourceHeadSha = SourceHeadSha,
            TargetBaseSha = TargetBaseSha,
            CreatorUserId = CreatorUserId.Value,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };
        context.Set<MergeRequestEntity>().Add(entity);
        await context.SaveChangesAsync().ConfigureAwait(false);
        return (MergeRequestId.From(id), entity);
    }

    public static async Task SeedUsersAsync(OpenGitBaseDbContext context)
    {
        foreach (var (userId, username) in new[]
        {
            (CreatorUserId.Value, "creator"),
            (ApproverUserId.Value, "approver"),
        })
        {
            if (await context.Set<UserEntity>().AnyAsync(user => user.Id == userId).ConfigureAwait(false))
            {
                continue;
            }

            context.Set<UserEntity>().Add(
                new UserEntity
                {
                    Id = userId,
                    Username = username,
                    NormalizedUsername = username.ToUpperInvariant(),
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
        }

        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public static async Task SeedRepositoryAsync(OpenGitBaseDbContext context)
    {
        await SeedUsersAsync(context).ConfigureAwait(false);
        if (await context.Set<RepositoryEntity>().AnyAsync(r => r.Id == RepositoryId).ConfigureAwait(false))
        {
            return;
        }

        context.Set<RepositoryEntity>().Add(
            new RepositoryEntity
            {
                Id = RepositoryId,
                Name = "demo",
                Slug = "demo",
                OwnerUserId = CreatorUserId.Value,
                PhysicalPath = "/tmp/demo.git",
                DefaultBranchName = TargetRef,
            }
        );
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public static async Task SeedProtectedBranchRuleAsync(
        OpenGitBaseDbContext context,
        int requiredApprovalCount = 1,
        bool dismissApprovalsOnPush = false,
        string pattern = "main"
    )
    {
        await SeedRepositoryAsync(context).ConfigureAwait(false);
        var ruleId = Guid.NewGuid();
        context.Set<ProtectedBranchRuleEntity>().Add(
            new ProtectedBranchRuleEntity
            {
                Id = ruleId,
                RepositoryId = RepositoryId,
                Pattern = pattern,
                BlockDirectPush = true,
                AllowedPushRoles = AllowedPushRoles.Admin | AllowedPushRoles.Owner,
                RequireMergeRequest = true,
                RequiredApprovalCount = requiredApprovalCount,
                MergeRoleThreshold = 2,
                ForcePushPolicy = ForcePushPolicy.AllowAllowedPushers,
                DismissApprovalsOnPush = dismissApprovalsOnPush,
            }
        );
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public static async Task<DiscussionEntity> SeedDiscussionAsync(
        OpenGitBaseDbContext context,
        int number = 1,
        string title = "Linked discussion",
        DiscussionStatus status = DiscussionStatus.Open
    )
    {
        await SeedRepositoryAsync(context).ConfigureAwait(false);
        var entity = new DiscussionEntity
        {
            Id = Guid.NewGuid(),
            RepositoryId = RepositoryId,
            Number = number,
            Title = title,
            Body = "Discussion body",
            Status = (int)status,
            HasEverBeenEngaged = false,
            CreatorUserId = CreatorUserId.Value,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        context.Set<DiscussionEntity>().Add(entity);
        await context.SaveChangesAsync().ConfigureAwait(false);
        return entity;
    }
}
