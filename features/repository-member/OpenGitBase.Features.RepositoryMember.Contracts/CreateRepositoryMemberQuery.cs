using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.RepositoryMember.Contracts;

namespace OpenGitBase.Features.RepositoryMember.Contracts;

public class CreateRepositoryMemberQuery
    : CreateQuery<RepositoryMemberDto, RepositoryMemberId, Guid, CreateRepositoryMemberQuery>;
