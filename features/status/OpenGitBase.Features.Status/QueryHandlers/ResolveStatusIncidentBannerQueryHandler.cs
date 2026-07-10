using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Status.Contracts;
using OpenGitBase.Features.Status.Entities;

namespace OpenGitBase.Features.Status.QueryHandlers;

public sealed class ResolveStatusIncidentBannerQueryHandler
    : IQueryHandler<ResolveStatusIncidentBannerQuery, bool>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public ResolveStatusIncidentBannerQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<bool>> RunQueryAsync(
        ResolveStatusIncidentBannerQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var active = await context
            .Set<StatusIncidentBannerEntity>()
            .Where(entity => entity.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (active.Count == 0)
        {
            return Option.From(true);
        }

        foreach (var banner in active)
        {
            banner.IsActive = false;
            banner.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(true);
    }
}
