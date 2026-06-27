#pragma warning disable SA1402 // File may only contain a single type
namespace OpenGitBase.Api.Models;

public sealed class GitRefUpdateRequest
{
    public string RefName { get; set; } = string.Empty;

    public string OldSha { get; set; } = string.Empty;

    public string NewSha { get; set; } = string.Empty;

    public bool? IsForcePush { get; set; }
}

public sealed class GitPushCommitRequest
{
    public string Sha { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public IReadOnlyList<string> ChangedPaths { get; set; } = [];

    public long MaxBlobBytes { get; set; }
}

public sealed class RepositoryPushValidationRequest
{
    public string PhysicalPath { get; set; } = string.Empty;

    public IReadOnlyList<GitRefUpdateRequest> RefUpdates { get; set; } = [];

    public IReadOnlyList<GitPushCommitRequest> Commits { get; set; } = [];

    public Guid? ResolvedUserId { get; set; }

    public string? EffectiveRole { get; set; }

    public bool IsPlatformMergeIdentity { get; set; }

    public bool ValidatePushRulesOnly { get; set; }
}

public sealed class RepositoryPushValidationResponse
{
    public bool Allowed { get; init; }

    public string? Reason { get; init; }
}
