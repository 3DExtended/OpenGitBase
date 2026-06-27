#pragma warning disable SA1402 // File may only contain a single type
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Features.Repository.Entities;

public class ProtectedBranchRuleEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public Guid RepositoryId { get; set; }

    public string Pattern { get; set; } = string.Empty;

    public bool BlockDirectPush { get; set; } = true;

    public AllowedPushRoles AllowedPushRoles { get; set; } =
        AllowedPushRoles.Owner | AllowedPushRoles.Admin;

    public bool RequireMergeRequest { get; set; } = true;

    public int RequiredApprovalCount { get; set; }

    public int MergeRoleThreshold { get; set; } = 2;

    public ForcePushPolicy ForcePushPolicy { get; set; } = ForcePushPolicy.DenyAll;

    public bool DismissApprovalsOnPush { get; set; } = true;

    public LockedMergeStrategy? LockedMergeStrategy { get; set; }

    public ICollection<ProtectedBranchAllowedUserEntity> AllowedUsers { get; set; } = [];

    public ICollection<PushRuleEntity> PushRules { get; set; } = [];
}

public class ProtectedBranchAllowedUserEntity
{
    public Guid ProtectedBranchRuleId { get; set; }

    public Guid UserId { get; set; }

    public ProtectedBranchRuleEntity ProtectedBranchRule { get; set; } = default!;
}

public class PushRuleEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public Guid ProtectedBranchRuleId { get; set; }

    public PushRuleType RuleType { get; set; }

    public string ConfigJson { get; set; } = "{}";

    public ProtectedBranchRuleEntity ProtectedBranchRule { get; set; } = default!;
}
