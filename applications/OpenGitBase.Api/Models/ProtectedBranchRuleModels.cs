#pragma warning disable SA1402 // File may only contain a single type
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Models;

public class UpsertPushRuleRequest
{
    public Guid? Id { get; set; }

    public PushRuleType RuleType { get; set; }

    public string ConfigJson { get; set; } = "{}";
}

public class UpsertProtectedBranchRuleRequest
{
    public string Pattern { get; set; } = string.Empty;

    public bool BlockDirectPush { get; set; } = true;

    public AllowedPushRoles AllowedPushRoles { get; set; } =
        AllowedPushRoles.Owner | AllowedPushRoles.Admin;

    public IReadOnlyList<Guid> AllowedPushUserIds { get; set; } = [];

    public bool RequireMergeRequest { get; set; } = true;

    public int RequiredApprovalCount { get; set; }

    public int MergeRoleThreshold { get; set; } = 2;

    public ForcePushPolicy ForcePushPolicy { get; set; } = ForcePushPolicy.DenyAll;

    public bool DismissApprovalsOnPush { get; set; } = true;

    public LockedMergeStrategy? LockedMergeStrategy { get; set; }

    public IReadOnlyList<UpsertPushRuleRequest> PushRules { get; set; } = [];
}
