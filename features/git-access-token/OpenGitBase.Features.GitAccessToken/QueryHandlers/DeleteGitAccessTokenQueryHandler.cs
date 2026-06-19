using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.GitAccessToken.Contracts;
using OpenGitBase.Features.GitAccessToken.Entities;

namespace OpenGitBase.Features.GitAccessToken.QueryHandlers;

public class DeleteGitAccessTokenQueryHandler : IQueryHandler<DeleteGitAccessTokenQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _systemClock;

    public DeleteGitAccessTokenQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ISystemClock systemClock
    )
    {
        _contextFactory = contextFactory;
        _systemClock = systemClock;
    }

    public async Task<Option<Unit>> RunQueryAsync(
        DeleteGitAccessTokenQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context
            .Set<GitAccessTokenEntity>()
            .FirstOrDefaultAsync(x => x.Id == query.Id.Value, cancellationToken);

        if (entity is null || entity.RevokedAt is not null)
        {
            return Option<Unit>.None;
        }

        entity.RevokedAt = _systemClock.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
