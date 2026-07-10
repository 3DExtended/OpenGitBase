using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Status.Contracts;
using OpenGitBase.Features.Status.Entities;

namespace OpenGitBase.Features.Status.QueryHandlers;

public sealed class ListFleetComponentsQueryHandler
    : IQueryHandler<ListFleetComponentsQuery, IReadOnlyList<FleetComponentDto>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;
    private readonly FleetComponentOptions _options;

    public ListFleetComponentsQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper,
        FleetComponentOptions options
    )
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
        _options = options;
    }

    public async Task<Option<IReadOnlyList<FleetComponentDto>>> RunQueryAsync(
        ListFleetComponentsQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await MarkStaleComponentsUnhealthyAsync(context, cancellationToken).ConfigureAwait(false);

        var components = await context
            .Set<FleetComponentEntity>()
            .AsNoTracking()
            .OrderBy(entity => entity.ComponentType)
            .ThenBy(entity => entity.InstanceId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Option.From<IReadOnlyList<FleetComponentDto>>(
            components.Select(component => _mapper.Map<FleetComponentDto>(component)).ToList()
        );
    }

    internal async Task MarkStaleComponentsUnhealthyAsync(
        OpenGitBaseDbContext context,
        CancellationToken cancellationToken
    )
    {
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-_options.MissedHeartbeatThresholdSeconds);
        var healthyComponents = await context
            .Set<FleetComponentEntity>()
            .Where(component => component.IsHealthy)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var staleComponents = healthyComponents
            .Where(
                component =>
                    component.LastHeartbeatAt is null || component.LastHeartbeatAt < cutoff
            )
            .ToList();

        if (staleComponents.Count == 0)
        {
            return;
        }

        foreach (var component in staleComponents)
        {
            component.IsHealthy = false;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
