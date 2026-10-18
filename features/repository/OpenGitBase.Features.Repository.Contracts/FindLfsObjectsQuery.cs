using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Repository.Contracts;

/// <summary>Returns which of the given oids are recorded (verified) LFS objects of the repository.</summary>
public sealed class FindLfsObjectsQuery : IQuery<IReadOnlyList<LfsObjectDto>, FindLfsObjectsQuery>
{
    public RepositoryId RepositoryId { get; set; } = default!;

    public IReadOnlyCollection<string> Oids { get; set; } = [];
}
