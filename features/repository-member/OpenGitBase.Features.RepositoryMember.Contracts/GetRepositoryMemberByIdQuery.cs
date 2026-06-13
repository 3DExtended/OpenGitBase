using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.RepositoryMember.Contracts;

public class GetRepositoryMemberByIdQuery
    : SingleModelQuery<
        RepositoryMemberDto,
        RepositoryMemberId,
        Guid,
        GetRepositoryMemberByIdQuery
    > { }
