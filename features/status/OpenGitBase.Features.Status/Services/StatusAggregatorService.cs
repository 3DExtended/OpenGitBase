using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Status.Contracts;
using OpenGitBase.Features.Status.Entities;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Features.Status.Services;

public sealed class StatusAggregatorService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IQueryProcessor _queryProcessor;
    private readonly StatusProbeEngine _probeEngine;
    private readonly StorageGroupStatusBuilder _storageGroupStatusBuilder;
    private readonly MessageBusGroupStatusBuilder _messageBusGroupStatusBuilder;
    private readonly StatusHistoryService _historyService;
    private readonly StatusOutageWindowService _outageWindowService;
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IConfiguration _configuration;
    private readonly StatusProbeOptions _options;
    private readonly ISystemClock _clock;
    private readonly ILogger<StatusAggregatorService> _logger;

    public StatusAggregatorService(
        IQueryProcessor queryProcessor,
        StatusProbeEngine probeEngine,
        StorageGroupStatusBuilder storageGroupStatusBuilder,
        MessageBusGroupStatusBuilder messageBusGroupStatusBuilder,
        StatusHistoryService historyService,
        StatusOutageWindowService outageWindowService,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IConfiguration configuration,
        StatusProbeOptions options,
        ISystemClock clock,
        ILogger<StatusAggregatorService> logger
    )
    {
        _queryProcessor = queryProcessor;
        _probeEngine = probeEngine;
        _storageGroupStatusBuilder = storageGroupStatusBuilder;
        _messageBusGroupStatusBuilder = messageBusGroupStatusBuilder;
        _historyService = historyService;
        _outageWindowService = outageWindowService;
        _contextFactory = contextFactory;
        _configuration = configuration;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    public async Task<bool> TryRunAggregationCycleAsync(CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        if (
            !await PostgresAdvisoryLockService
                .TryAcquireAsync(context, _options.AdvisoryLockKey, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            return false;
        }

        try
        {
            var snapshot = await BuildSnapshotAsync(cancellationToken).ConfigureAwait(false);
            await _outageWindowService
                .ApplySnapshotAsync(snapshot, cancellationToken)
                .ConfigureAwait(false);
            await PersistSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
            await _historyService
                .RecordSnapshotAsync(snapshot, cancellationToken)
                .ConfigureAwait(false);
            _logger.LogInformation(
                "Status aggregation completed with overall status {Status}",
                snapshot.OverallStatus
            );
            return true;
        }
        finally
        {
            await PostgresAdvisoryLockService
                .ReleaseAsync(context, _options.AdvisoryLockKey, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task<PublicStatusSnapshotDto> BuildSnapshotAsync(CancellationToken cancellationToken)
    {
        var checkedAt = _clock.UtcNow;
        var fleetComponents = await _queryProcessor
            .RunQueryAsync(new ListFleetComponentsQuery(), cancellationToken)
            .ConfigureAwait(false);
        var storageNodes = await _queryProcessor
            .RunQueryAsync(new ListStorageNodeQuery(), cancellationToken)
            .ConfigureAwait(false);

        var fleetComponentList = fleetComponents.IsSome
            ? fleetComponents.Get()
            : Array.Empty<FleetComponentDto>();
        var storageNodeList = storageNodes.IsSome
            ? storageNodes.Get()
            : Array.Empty<StorageNodeDto>();

        var groups = new List<StatusGroupSnapshot>();
        foreach (var componentGroup in new[]
                 {
                     StatusComponentGroup.Website,
                     StatusComponentGroup.Api,
                     StatusComponentGroup.Git,
                 })
        {
            groups.Add(
                await BuildFleetGroupAsync(
                    componentGroup,
                    fleetComponentList,
                    checkedAt,
                    cancellationToken
                )
            );
        }

        groups.Add(_storageGroupStatusBuilder.Build(storageNodeList, checkedAt));
        groups.Add(await BuildDataStoresGroupAsync(cancellationToken).ConfigureAwait(false));
        groups.Add(await BuildMessageBusGroupAsync(cancellationToken).ConfigureAwait(false));

        var incident = await LoadActiveIncidentAsync(cancellationToken).ConfigureAwait(false);
        var overall = StatusRollupEngine.RollupOverall(groups.Select(group => group.Status));

        return new PublicStatusSnapshotDto
        {
            OverallStatus = overall,
            CheckedAt = checkedAt,
            Groups = groups,
            Incident = incident,
        };
    }

    private async Task<StatusGroupSnapshot> BuildFleetGroupAsync(
        StatusComponentGroup group,
        IReadOnlyList<FleetComponentDto> components,
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken
    )
    {
        var fleetType = group switch
        {
            StatusComponentGroup.Website => FleetComponentType.Website,
            StatusComponentGroup.Api => FleetComponentType.Api,
            StatusComponentGroup.Git => FleetComponentType.Git,
            _ => FleetComponentType.Website,
        };

        var instances = new List<StatusInstanceSnapshot>();
        foreach (var component in components.Where(item => item.ComponentType == fleetType))
        {
            if (!component.IsHealthy)
            {
                instances.Add(
                    new StatusInstanceSnapshot
                    {
                        InstanceId = component.InstanceId,
                        Status = PublicHealthStatus.Unhealthy,
                        LastCheckedAt = checkedAt,
                        Message = "Registration stale",
                    }
                );
                continue;
            }

            var probeUrl = FleetProbeUrlNormalizer.Normalize(
                component.InstanceId,
                component.ProbeUrl
            );
            instances.Add(
                await _probeEngine
                    .ProbeHttpAsync(
                        component.InstanceId,
                        probeUrl,
                        _options.TimeoutMs,
                        _options.SlowThresholdMs,
                        cancellationToken
                    )
                    .ConfigureAwait(false)
            );
        }

        return new StatusGroupSnapshot
        {
            Group = group,
            Status = StatusRollupEngine.RollupGroup(instances.Select(instance => instance.Status)),
            Instances = instances,
        };
    }

    private async Task<StatusGroupSnapshot> BuildDataStoresGroupAsync(
        CancellationToken cancellationToken
    )
    {
        var targets = DataStoreProbeTargetResolver.Resolve(
            _configuration["Sql:ConnectionString"],
            _configuration["REDIS_URL"]
        );

        var instances = new List<StatusInstanceSnapshot>();
        foreach (var target in targets)
        {
            instances.Add(
                await _probeEngine
                    .ProbeTcpAsync(
                        target.InstanceId,
                        target.Host,
                        target.Port,
                        _options.TimeoutMs,
                        _options.SlowThresholdMs,
                        cancellationToken
                    )
                    .ConfigureAwait(false)
            );
        }

        return new StatusGroupSnapshot
        {
            Group = StatusComponentGroup.DataStores,
            Status = StatusRollupEngine.RollupGroup(instances.Select(instance => instance.Status)),
            Instances = instances,
        };
    }

    private async Task<StatusGroupSnapshot> BuildMessageBusGroupAsync(
        CancellationToken cancellationToken
    )
    {
        var targets = KafkaProbeTargetResolver.Resolve(_configuration["Kafka:BootstrapServers"]);

        var instances = new List<StatusInstanceSnapshot>();
        foreach (var target in targets)
        {
            instances.Add(
                await _probeEngine
                    .ProbeTcpAsync(
                        target.InstanceId,
                        target.Host,
                        target.Port,
                        _options.TimeoutMs,
                        _options.SlowThresholdMs,
                        cancellationToken
                    )
                    .ConfigureAwait(false)
            );
        }

        return _messageBusGroupStatusBuilder.Build(instances);
    }

    private async Task<PublicStatusIncidentDto?> LoadActiveIncidentAsync(
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var banner = await context
            .Set<StatusIncidentBannerEntity>()
            .AsNoTracking()
            .Where(entity => entity.IsActive)
            .OrderByDescending(entity => entity.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (banner is null)
        {
            return null;
        }

        return new PublicStatusIncidentDto
        {
            Message = banner.Message,
            Severity = banner.Severity,
            UpdatedAt = banner.UpdatedAt,
        };
    }

    private async Task PersistSnapshotAsync(
        PublicStatusSnapshotDto snapshot,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context
            .Set<StatusSnapshotEntity>()
            .FirstOrDefaultAsync(
                item => item.Id == StatusSnapshotEntity.SingletonId,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (entity is null)
        {
            entity = new StatusSnapshotEntity { Id = StatusSnapshotEntity.SingletonId };
            context.Set<StatusSnapshotEntity>().Add(entity);
        }

        entity.CheckedAt = snapshot.CheckedAt;
        entity.OverallStatus = snapshot.OverallStatus;
        entity.PayloadJson = JsonSerializer.Serialize(snapshot, JsonOptions);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
