#pragma warning disable SA1402 // File may only contain a single type
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Repository.Contracts;

[Flags]
public enum AllowedPushRoles
{
    None = 0,
    Owner = 1,
    Admin = 2,
    Writer = 4,
}

public enum ForcePushPolicy
{
    DenyAll = 0,
    AllowAllowedPushers = 1,
    PlatformOnly = 2,
}

public enum LockedMergeStrategy
{
    MergeCommit = 0,
    Squash = 1,
    FastForward = 2,
}

public enum PushRuleType
{
    MaxFileSize = 0,
    ForbiddenPaths = 1,
    CommitMessageRegex = 2,
    RequireDco = 3,
}

public class PushRuleDto : ModelBase<PushRuleId, Guid>
{
    public PushRuleType RuleType { get; set; }

    public string ConfigJson { get; set; } = "{}";
}

public class ProtectedBranchRuleDto : ModelBase<ProtectedBranchRuleId, Guid>
{
    public RepositoryId RepositoryId { get; set; } = default!;

    public string Pattern { get; set; } = string.Empty;

    public bool BlockDirectPush { get; set; } = true;

    public AllowedPushRoles AllowedPushRoles { get; set; } =
        AllowedPushRoles.Owner | AllowedPushRoles.Admin;

    public IReadOnlyList<UserId> AllowedPushUserIds { get; set; } = [];

    public bool RequireMergeRequest { get; set; } = true;

    public int RequiredApprovalCount { get; set; }

    public int MergeRoleThreshold { get; set; } = 2;

    public ForcePushPolicy ForcePushPolicy { get; set; } = ForcePushPolicy.DenyAll;

    public bool DismissApprovalsOnPush { get; set; } = true;

    public LockedMergeStrategy? LockedMergeStrategy { get; set; }

    public IReadOnlyList<PushRuleDto> PushRules { get; set; } = [];
}
