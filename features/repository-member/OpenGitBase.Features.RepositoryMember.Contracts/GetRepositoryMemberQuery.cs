using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.RepositoryMember.Contracts;

public class GetRepositoryMemberQuery : IQuery<RepositoryMemberDto, GetRepositoryMemberQuery>
{
    public UserId UserId { get; set; } = default!;
    public RepositoryId RepositoryId { get; set; } = default!;
}
