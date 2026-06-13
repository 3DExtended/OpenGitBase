using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.RepositoryMember.Entities;

namespace OpenGitBase.Features.RepositoryMember.QueryHandlers;

public class DeleteRepositoryMemberQueryHandler
    : DeleteCommandHandlerBase<
        DeleteRepositoryMemberQuery,
        RepositoryMemberDto,
        RepositoryMemberId,
        Guid,
        OpenGitBaseDbContext,
        Entities.RepositoryMemberEntity
    >
{
    public DeleteRepositoryMemberQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
        : base(contextFactory) { }
}
