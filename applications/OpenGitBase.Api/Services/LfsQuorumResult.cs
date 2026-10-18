using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Services;

/// <summary>
/// What verify learned from replication: whether quorum was reached, the key version of the encrypted copies, the
/// copies that make up the quorum, and the copies still under way (null unless quorum was reached).
/// </summary>
public sealed record LfsQuorumResult(
    LfsQuorumStatus Status,
    int KeyVersion,
    IReadOnlyList<LfsObjectCopySpec> Copies,
    LfsPendingCopies? Pending
);
