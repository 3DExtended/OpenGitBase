using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Services;

/// <summary>
/// The outcome of copying an LFS object to one storage node. <paramref name="SourceFailedVerification"/> marks a
/// restore whose encrypted source failed authentication or the oid check, so that copy is damaged, not just
/// unreachable.
/// </summary>
public sealed record LfsCopyAttempt(
    StorageNodeEndpoint Node,
    LfsObjectCopyKind Kind,
    bool Succeeded,
    bool SourceFailedVerification = false
);
