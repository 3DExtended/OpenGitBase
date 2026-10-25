using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Repository.Contracts;

/// <summary>
/// Records a verified LFS object together with the <see cref="Copies"/> that reached quorum. Idempotent: recording
/// an object that is already recorded succeeds with <see cref="RecordLfsObjectResult.Created"/> false (and adds
/// nothing), also when two verifies of one oid race.
/// </summary>
public sealed class RecordLfsObjectQuery : IQuery<RecordLfsObjectResult, RecordLfsObjectQuery>
{
    public RepositoryId RepositoryId { get; set; } = default!;

    public string Oid { get; set; } = string.Empty;

    public long Size { get; set; }

    public int KeyVersion { get; set; }

    public UserId? CreatedByUserId { get; set; }

    public IReadOnlyList<LfsObjectCopySpec> Copies { get; set; } = [];
}
