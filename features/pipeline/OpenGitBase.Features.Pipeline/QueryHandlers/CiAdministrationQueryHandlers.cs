using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Features.Pipeline.QueryHandlers;

#pragma warning disable SA1402

public sealed class RecordDependencyInstallOutcomeQueryHandler
    : IQueryHandler<RecordDependencyInstallOutcomeQuery, bool>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public RecordDependencyInstallOutcomeQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<bool>> RunQueryAsync(
        RecordDependencyInstallOutcomeQuery query,
        CancellationToken cancellationToken
    )
    {
        if (query.JobId.Value == Guid.Empty || string.IsNullOrWhiteSpace(query.RecipeKey))
        {
            return Option<bool>.None;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Set<DependencyInstallOutcomeEntity>()
            .Add(
                new DependencyInstallOutcomeEntity
                {
                    Id = Guid.NewGuid(),
                    RecipeKey = query.RecipeKey,
                    Success = query.Success,
                    ExitCode = query.ExitCode,
                    DurationMs = query.DurationMs,
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(true);
    }
}

public sealed class RequestDependencyLayerPromotionQueryHandler
    : IQueryHandler<RequestDependencyLayerPromotionQuery, DependencyPromotionRequestDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;

    public RequestDependencyLayerPromotionQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper
    )
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
    }

    public async Task<Option<DependencyPromotionRequestDto>> RunQueryAsync(
        RequestDependencyLayerPromotionQuery query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(query.RecipeKey) || query.RequestedByUserId == Guid.Empty)
        {
            return Option<DependencyPromotionRequestDto>.None;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var outcomes = await context
            .Set<DependencyInstallOutcomeEntity>()
            .Where(entity => entity.RecipeKey == query.RecipeKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var lastFive = outcomes
            .OrderByDescending(entity => entity.CreatedAt)
            .Take(5)
            .ToList();
        if (lastFive.Count < 5 || lastFive.Any(entity => !entity.Success))
        {
            return Option<DependencyPromotionRequestDto>.None;
        }

        var request = new DependencyPromotionRequestEntity
        {
            Id = Guid.NewGuid(),
            RecipeKey = query.RecipeKey,
            RequestedByUserId = query.RequestedByUserId,
            Status = DependencyPromotionRequestStatus.Queued,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        context.Set<DependencyPromotionRequestEntity>().Add(request);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var dto = _mapper.Map<DependencyPromotionRequestDto>(request);
        dto.PromotionJobScheduled = true;
        return Option.From(dto);
    }
}

public sealed class SubmitDomainAllowanceRequestQueryHandler
    : IQueryHandler<SubmitDomainAllowanceRequestQuery, DomainAllowanceRequestDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;

    public SubmitDomainAllowanceRequestQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper
    )
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
    }

    public async Task<Option<DomainAllowanceRequestDto>> RunQueryAsync(
        SubmitDomainAllowanceRequestQuery query,
        CancellationToken cancellationToken
    )
    {
        if (
            string.IsNullOrWhiteSpace(query.Domain)
            || string.IsNullOrWhiteSpace(query.Justification)
            || query.RequestedByUserId == Guid.Empty
        )
        {
            return Option<DomainAllowanceRequestDto>.None;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = new DomainAllowanceRequestEntity
        {
            Id = Guid.NewGuid(),
            Domain = query.Domain.Trim(),
            Justification = query.Justification.Trim(),
            Scope = query.Scope,
            OrganizationId = query.OrganizationId,
            Status = DomainAllowanceRequestStatus.Pending,
            RequestedByUserId = query.RequestedByUserId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        context.Set<DomainAllowanceRequestEntity>().Add(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(_mapper.Map<DomainAllowanceRequestDto>(entity));
    }
}

public sealed class ReviewDomainAllowanceRequestQueryHandler
    : IQueryHandler<ReviewDomainAllowanceRequestQuery, DomainAllowanceRequestDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;

    public ReviewDomainAllowanceRequestQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper
    )
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
    }

    public async Task<Option<DomainAllowanceRequestDto>> RunQueryAsync(
        ReviewDomainAllowanceRequestQuery query,
        CancellationToken cancellationToken
    )
    {
        if (query.RequestId.Value == Guid.Empty || query.ReviewedByUserId == Guid.Empty)
        {
            return Option<DomainAllowanceRequestDto>.None;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var request = await context
            .Set<DomainAllowanceRequestEntity>()
            .FirstOrDefaultAsync(entity => entity.Id == query.RequestId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (request is null || request.Status != DomainAllowanceRequestStatus.Pending)
        {
            return Option<DomainAllowanceRequestDto>.None;
        }

        request.Status = query.Approve
            ? DomainAllowanceRequestStatus.Approved
            : DomainAllowanceRequestStatus.Denied;
        request.ReviewedByUserId = query.ReviewedByUserId;
        request.ReviewedAt = DateTimeOffset.UtcNow;

        if (query.Approve)
        {
            if (request.Scope == DomainAllowanceRequestScope.Platform)
            {
                var exists = await context
                    .Set<PlatformEgressAllowlistEntity>()
                    .AnyAsync(entity => entity.Domain == request.Domain, cancellationToken)
                    .ConfigureAwait(false);
                if (!exists)
                {
                    context.Set<PlatformEgressAllowlistEntity>()
                        .Add(
                            new PlatformEgressAllowlistEntity
                            {
                                Id = Guid.NewGuid(),
                                Domain = request.Domain,
                                ApprovedByUserId = query.ReviewedByUserId,
                                CreatedAt = DateTimeOffset.UtcNow,
                            }
                        );
                }
            }
            else if (request.OrganizationId.HasValue)
            {
                var exists = await context
                    .Set<OrgEgressAllowlistEntity>()
                    .AnyAsync(
                        entity =>
                            entity.OrganizationId == request.OrganizationId.Value
                            && entity.Domain == request.Domain,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                if (!exists)
                {
                    context.Set<OrgEgressAllowlistEntity>()
                        .Add(
                            new OrgEgressAllowlistEntity
                            {
                                Id = Guid.NewGuid(),
                                OrganizationId = request.OrganizationId.Value,
                                Domain = request.Domain,
                                ApprovedByUserId = query.ReviewedByUserId,
                                CreatedAt = DateTimeOffset.UtcNow,
                            }
                        );
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(_mapper.Map<DomainAllowanceRequestDto>(request));
    }
}

public sealed class ResolveEffectiveEgressAllowlistQueryHandler
    : IQueryHandler<ResolveEffectiveEgressAllowlistQuery, IReadOnlyList<string>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public ResolveEffectiveEgressAllowlistQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<IReadOnlyList<string>>> RunQueryAsync(
        ResolveEffectiveEgressAllowlistQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var platformDomains = await context
            .Set<PlatformEgressAllowlistEntity>()
            .OrderBy(entity => entity.Domain)
            .Select(entity => entity.Domain)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!string.Equals(query.RunsOn, "organization-self-hosted", StringComparison.OrdinalIgnoreCase))
        {
            return Option.From((IReadOnlyList<string>)platformDomains);
        }

        if (!query.OrganizationId.HasValue)
        {
            return Option.From((IReadOnlyList<string>)platformDomains);
        }

        var orgDomains = await context
            .Set<OrgEgressAllowlistEntity>()
            .Where(entity => entity.OrganizationId == query.OrganizationId.Value)
            .OrderBy(entity => entity.Domain)
            .Select(entity => entity.Domain)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var merged = platformDomains
            .Concat(orgDomains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(domain => domain, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Option.From((IReadOnlyList<string>)merged);
    }
}

public sealed class ListDomainAllowanceRequestsQueryHandler
    : IQueryHandler<ListDomainAllowanceRequestsQuery, IReadOnlyList<DomainAllowanceRequestDto>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;

    public ListDomainAllowanceRequestsQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper
    )
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
    }

    public async Task<Option<IReadOnlyList<DomainAllowanceRequestDto>>> RunQueryAsync(
        ListDomainAllowanceRequestsQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await context.Set<DomainAllowanceRequestEntity>().ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var filtered = entities
            .Where(entity => !query.Scope.HasValue || entity.Scope == query.Scope.Value)
            .Where(entity => !query.OrganizationId.HasValue || entity.OrganizationId == query.OrganizationId)
            .Where(entity => !query.Status.HasValue || entity.Status == query.Status.Value)
            .OrderByDescending(entity => entity.CreatedAt)
            .Select(entity => _mapper.Map<DomainAllowanceRequestDto>(entity))
            .ToList();
        return Option.From((IReadOnlyList<DomainAllowanceRequestDto>)filtered);
    }
}

public sealed class ListDependencyInstallAnalyticsQueryHandler
    : IQueryHandler<ListDependencyInstallAnalyticsQuery, IReadOnlyList<DependencyInstallAnalyticsDto>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public ListDependencyInstallAnalyticsQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<IReadOnlyList<DependencyInstallAnalyticsDto>>> RunQueryAsync(
        ListDependencyInstallAnalyticsQuery query,
        CancellationToken cancellationToken
    )
    {
        _ = query;
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var outcomes = await context.Set<DependencyInstallOutcomeEntity>().ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var analytics = outcomes
            .GroupBy(entity => entity.RecipeKey, StringComparer.Ordinal)
            .Select(group =>
            {
                var ordered = group.OrderByDescending(entity => entity.CreatedAt).ToList();
                var successCount = ordered.Count(entity => entity.Success);
                var lastFive = ordered.Take(5).ToList();
                var eligible = lastFive.Count == 5 && lastFive.All(entity => entity.Success);
                var durations = ordered.Select(entity => entity.DurationMs).OrderBy(value => value).ToList();
                var median = durations.Count == 0
                    ? 0
                    : durations[durations.Count / 2];
                return new DependencyInstallAnalyticsDto
                {
                    RecipeKey = group.Key,
                    InstallCount = ordered.Count,
                    SuccessCount = successCount,
                    SuccessRate = ordered.Count == 0 ? 0 : (double)successCount / ordered.Count,
                    MedianDurationMs = median,
                    PromotionEligible = eligible,
                    PromotionBlockedReason = eligible
                        ? null
                        : "Promotion blocked until the last five installs succeed.",
                };
            })
            .OrderBy(dto => dto.RecipeKey, StringComparer.Ordinal)
            .ToList();
        return Option.From((IReadOnlyList<DependencyInstallAnalyticsDto>)analytics);
    }
}

public sealed class ListDependencyPromotionRequestsQueryHandler
    : IQueryHandler<ListDependencyPromotionRequestsQuery, IReadOnlyList<DependencyPromotionRequestDto>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;

    public ListDependencyPromotionRequestsQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper
    )
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
    }

    public async Task<Option<IReadOnlyList<DependencyPromotionRequestDto>>> RunQueryAsync(
        ListDependencyPromotionRequestsQuery query,
        CancellationToken cancellationToken
    )
    {
        _ = query;
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await context.Set<DependencyPromotionRequestEntity>().ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var requests = entities
            .OrderByDescending(entity => entity.CreatedAt)
            .Select(entity => _mapper.Map<DependencyPromotionRequestDto>(entity))
            .ToList();
        return Option.From((IReadOnlyList<DependencyPromotionRequestDto>)requests);
    }
}

public sealed class ResolvePromotedDependencyLayerQueryHandler
    : IQueryHandler<ResolvePromotedDependencyLayerQuery, BaseImageArtifactDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public ResolvePromotedDependencyLayerQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<BaseImageArtifactDto>> RunQueryAsync(
        ResolvePromotedDependencyLayerQuery query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(query.RecipeKey))
        {
            return Option<BaseImageArtifactDto>.None;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var promotions = await context
            .Set<DependencyPromotionRequestEntity>()
            .Where(entity => entity.RecipeKey == query.RecipeKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var completed = promotions
            .Where(entity => entity.Status == DependencyPromotionRequestStatus.Completed)
            .OrderByDescending(entity => entity.CompletedAt ?? entity.CreatedAt)
            .FirstOrDefault();
        if (
            completed is null
            || string.IsNullOrWhiteSpace(completed.ContentHash)
            || string.IsNullOrWhiteSpace(completed.LayerStoreObjectKey)
        )
        {
            return Option<BaseImageArtifactDto>.None;
        }

        return Option.From(
            new BaseImageArtifactDto
            {
                Slug = query.RecipeKey,
                ContentHash = completed.ContentHash,
                LayerStoreObjectKey = completed.LayerStoreObjectKey,
            }
        );
    }
}

#pragma warning restore SA1402
