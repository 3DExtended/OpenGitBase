using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.RepositoryMember.Entities;

namespace OpenGitBase.Features.RepositoryMember.QueryHandlers;

public class UpdateRepositoryMemberQueryHandler
    : UpdateCommandHandlerBase<
        UpdateRepositoryMemberQuery,
        RepositoryMemberDto,
        RepositoryMemberId,
        Guid,
        OpenGitBaseDbContext,
        Entities.RepositoryMemberEntity
    >
{
    public UpdateRepositoryMemberQueryHandler(
        IMapper mapper,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
        : base(mapper, contextFactory) { }
}
