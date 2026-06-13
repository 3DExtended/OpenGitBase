using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.RepositoryMember.Contracts;

namespace OpenGitBase.Features.RepositoryMember.Contracts;

public class UpdateRepositoryMemberQuery
    : UpdateCommand<RepositoryMemberDto, RepositoryMemberId, Guid, UpdateRepositoryMemberQuery>;
