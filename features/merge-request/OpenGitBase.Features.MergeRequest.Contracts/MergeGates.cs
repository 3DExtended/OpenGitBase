#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1201 // Elements should appear in the correct order
#pragma warning disable SA1204 // Static members should appear before non-static members
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.MergeRequest.Contracts;

public sealed class MergeGateEvaluationResult
{
    public bool IsSatisfied { get; init; }

    public IReadOnlyList<string> PendingGateNames { get; init; } = [];

    public static MergeGateEvaluationResult Satisfied() =>
        new() { IsSatisfied = true };

    public static MergeGateEvaluationResult Pending(params string[] gateNames) =>
        new() { IsSatisfied = false, PendingGateNames = gateNames };
}

public interface IMergeGate
{
    string Name { get; }

    Task<MergeGateEvaluationResult> EvaluateAsync(
        MergeRequestGateContext context,
        CancellationToken cancellationToken
    );
}

public sealed class MergeRequestGateContext
{
    public required MergeRequestEntitySnapshot MergeRequest { get; init; }

    public required MergeRequestTargetPolicy TargetPolicy { get; init; }

    public required IReadOnlyList<MergeRequestApprovalDto> ApprovalsAtHead { get; init; }
}

public sealed class MergeRequestEntitySnapshot
{
    public Guid Id { get; init; }

    public Guid RepositoryId { get; init; }

    public int Number { get; init; }

    public MergeRequestStatus Status { get; init; }

    public bool IsDraft { get; init; }

    public Guid CreatorUserId { get; init; }

    public string SourceHeadSha { get; init; } = string.Empty;

    public string TargetRef { get; init; } = string.Empty;
}

public sealed class MergeRequestTargetPolicy
{
    public int RequiredApprovalCount { get; init; }

    public bool DismissApprovalsOnPush { get; init; }

    public int MergeRoleThreshold { get; init; } = 2;

    public LockedMergeStrategy? LockedMergeStrategy { get; init; }

    public static MergeRequestTargetPolicy Unprotected { get; } =
        new()
        {
            RequiredApprovalCount = 0,
            DismissApprovalsOnPush = false,
            MergeRoleThreshold = 2,
        };
}
