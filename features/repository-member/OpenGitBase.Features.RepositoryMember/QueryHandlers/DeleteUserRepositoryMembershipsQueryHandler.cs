using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.RepositoryMember.Entities;

namespace OpenGitBase.Features.RepositoryMember.QueryHandlers;

public class DeleteUserRepositoryMembershipsQueryHandler
    : IQueryHandler<DeleteUserRepositoryMembershipsQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public DeleteUserRepositoryMembershipsQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<Unit>> RunQueryAsync(
        DeleteUserRepositoryMembershipsQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var memberships = await context
            .Set<RepositoryMemberEntity>()
            .Where(x => x.UserId == query.UserId.Value)
            .ToListAsync(cancellationToken);
        context.RemoveRange(memberships);
        await context.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
