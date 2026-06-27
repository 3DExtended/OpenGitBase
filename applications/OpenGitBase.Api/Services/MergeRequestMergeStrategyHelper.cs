#pragma warning disable SA1402 // File may only contain a single type
﻿using OpenGitBase.Api.Models;
using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Services;

internal static class MergeRequestMergeStrategyHelper
{
    public static MergeRequestMergeStrategyDto? ResolveStrategy(
        MergeRequestMergeStrategyDto requested,
        LockedMergeStrategy? locked
    )
    {
        if (locked is null)
        {
            return requested;
        }

        var lockedDto = locked switch
        {
            LockedMergeStrategy.MergeCommit => MergeRequestMergeStrategyDto.MergeCommit,
            LockedMergeStrategy.Squash => MergeRequestMergeStrategyDto.Squash,
            LockedMergeStrategy.FastForward => MergeRequestMergeStrategyDto.FastForward,
            _ => requested,
        };

        return requested == lockedDto ? requested : null;
    }

    public static string ToStorageStrategy(MergeRequestMergeStrategyDto strategy) =>
        strategy switch
        {
            MergeRequestMergeStrategyDto.Squash => "squash",
            MergeRequestMergeStrategyDto.FastForward => "fast_forward",
            _ => "merge_commit",
        };

    public static string MapMergeabilityStatus(string status) =>
        status.ToLowerInvariant() switch
        {
            "mergeable" => nameof(MergeRequestMergeabilityStatus.Mergeable),
            "conflicts" => nameof(MergeRequestMergeabilityStatus.Conflicts),
            _ => nameof(MergeRequestMergeabilityStatus.Unknown),
        };
}
