using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.RepositoryMember.Contracts;

namespace OpenGitBase.Features.RepositoryMember.Contracts;

public class DeleteRepositoryMemberQuery
    : DeleteCommand<RepositoryMemberDto, RepositoryMemberId, Guid, DeleteRepositoryMemberQuery>;
