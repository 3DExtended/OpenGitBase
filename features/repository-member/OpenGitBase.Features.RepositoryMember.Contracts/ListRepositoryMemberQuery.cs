using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;

namespace OpenGitBase.Features.RepositoryMember.Contracts;

public class ListRepositoryMemberQuery
    : IQuery<IReadOnlyList<RepositoryMemberDto>, ListRepositoryMemberQuery>
{
    public RepositoryId RepositoryId { get; set; } = default!;
}
