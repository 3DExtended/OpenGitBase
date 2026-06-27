#pragma warning disable SA1402 // File may only contain a single type
﻿using OpenGitBase.Features.MergeRequest.Contracts;

namespace OpenGitBase.Features.MergeRequest;

public sealed class MergeGateRegistry
{
    private readonly IReadOnlyList<IMergeGate> _gates;

    public MergeGateRegistry(IEnumerable<IMergeGate> gates)
    {
        _gates = gates.ToList();
    }

    public static MergeGateRegistry CreateDefault() =>
        new([new RequiredApprovalsGate()]);

    public async Task<MergeGateEvaluationResult> EvaluateAllAsync(
        MergeRequestGateContext context,
        CancellationToken cancellationToken
    )
    {
        var pending = new List<string>();
        foreach (var gate in _gates)
        {
            var result = await gate
                .EvaluateAsync(context, cancellationToken)
                .ConfigureAwait(false);
            if (!result.IsSatisfied)
            {
                pending.AddRange(result.PendingGateNames);
            }
        }

        return pending.Count == 0
            ? MergeGateEvaluationResult.Satisfied()
            : MergeGateEvaluationResult.Pending(pending.ToArray());
    }
}

internal sealed class RequiredApprovalsGate : IMergeGate
{
    public string Name => "RequiredApprovals";

    public Task<MergeGateEvaluationResult> EvaluateAsync(
        MergeRequestGateContext context,
        CancellationToken cancellationToken
    )
    {
        var required = context.TargetPolicy.RequiredApprovalCount;
        var approvalCount = context.ApprovalsAtHead.Count;

        if (approvalCount >= required)
        {
            return Task.FromResult(MergeGateEvaluationResult.Satisfied());
        }

        return Task.FromResult(
            MergeGateEvaluationResult.Pending(
                $"{Name}: {approvalCount} of {required} approvals at current head."
            )
        );
    }
}
