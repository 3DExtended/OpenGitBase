using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Status.Contracts;
using OpenGitBase.Features.Status.Entities;
using OpenGitBase.Features.Status.Services;

namespace OpenGitBase.Features.Status.QueryHandlers;

public sealed class RegisterFleetComponentQueryHandler
    : IQueryHandler<RegisterFleetComponentQuery, RegisterFleetComponentResult>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly FleetComponentOptions _options;

    public RegisterFleetComponentQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        FleetComponentOptions options
    )
    {
        _contextFactory = contextFactory;
        _options = options;
    }

    public async Task<Option<RegisterFleetComponentResult>> RunQueryAsync(
        RegisterFleetComponentQuery query,
        CancellationToken cancellationToken
    )
    {
        if (
            string.IsNullOrWhiteSpace(query.InstanceId)
            || string.IsNullOrWhiteSpace(query.ProbeUrl)
        )
        {
            return Option<RegisterFleetComponentResult>.None;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var instanceId = query.InstanceId.Trim();
        var existing = await context
            .Set<FleetComponentEntity>()
            .FirstOrDefaultAsync(
                entity =>
                    entity.ComponentType == query.ComponentType
                    && entity.InstanceId == instanceId,
                cancellationToken
            )
            .ConfigureAwait(false);

        var probeUrl = FleetProbeUrlNormalizer.Normalize(instanceId, query.ProbeUrl);
        var now = DateTimeOffset.UtcNow;
        if (existing is null)
        {
            existing = new FleetComponentEntity
            {
                Id = Guid.NewGuid(),
                ComponentType = query.ComponentType,
                InstanceId = instanceId,
                ProbeUrl = probeUrl,
                Version = string.IsNullOrWhiteSpace(query.Version) ? null : query.Version.Trim(),
                RegisteredAt = now,
                LastHeartbeatAt = now,
                IsHealthy = true,
            };
            context.Set<FleetComponentEntity>().Add(existing);
        }
        else
        {
            existing.ProbeUrl = probeUrl;
            existing.Version = string.IsNullOrWhiteSpace(query.Version) ? null : query.Version.Trim();
            existing.LastHeartbeatAt = now;
            existing.IsHealthy = true;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Option.From(
            new RegisterFleetComponentResult
            {
                FleetComponentId = FleetComponentId.From(existing.Id),
                HeartbeatIntervalSeconds = _options.HeartbeatIntervalSeconds,
            }
        );
    }
}
