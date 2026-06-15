using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.PublicGitSshKey.Contracts;
using OpenGitBase.Features.PublicGitSshKey.Entities;

namespace OpenGitBase.Features.PublicGitSshKey.QueryHandlers;

public class DeleteUserSshKeysQueryHandler : IQueryHandler<DeleteUserSshKeysQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public DeleteUserSshKeysQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<Unit>> RunQueryAsync(
        DeleteUserSshKeysQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var sshKeys = await context
            .Set<PublicGitSshKeyEntity>()
            .Where(x => x.OwnerUserId == query.UserId.Value)
            .ToListAsync(cancellationToken);
        context.RemoveRange(sshKeys);
        await context.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
