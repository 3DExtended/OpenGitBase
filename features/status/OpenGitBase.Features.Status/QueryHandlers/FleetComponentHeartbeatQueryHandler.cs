using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Status.Contracts;
using OpenGitBase.Features.Status.Entities;

namespace OpenGitBase.Features.Status.QueryHandlers;

public sealed class FleetComponentHeartbeatQueryHandler
    : IQueryHandler<FleetComponentHeartbeatQuery, FleetComponentHeartbeatResult>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public FleetComponentHeartbeatQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<FleetComponentHeartbeatResult>> RunQueryAsync(
        FleetComponentHeartbeatQuery query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(query.InstanceId))
        {
            return Option<FleetComponentHeartbeatResult>.None;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var component = await context
            .Set<FleetComponentEntity>()
            .FirstOrDefaultAsync(
                entity =>
                    entity.ComponentType == query.ComponentType
                    && entity.InstanceId == query.InstanceId.Trim(),
                cancellationToken
            )
            .ConfigureAwait(false);

        if (component is null)
        {
            return Option<FleetComponentHeartbeatResult>.None;
        }

        component.LastHeartbeatAt = DateTimeOffset.UtcNow;
        component.IsHealthy = true;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Option.From(new FleetComponentHeartbeatResult { Acknowledged = true });
    }
}
