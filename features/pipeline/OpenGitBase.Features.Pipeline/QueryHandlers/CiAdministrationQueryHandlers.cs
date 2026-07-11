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
        var lastFive = await context
            .Set<DependencyInstallOutcomeEntity>()
            .Where(entity => entity.RecipeKey == query.RecipeKey)
            .Take(5)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        lastFive = lastFive.OrderByDescending(entity => entity.CreatedAt).Take(5).ToList();
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
        return Option.From(_mapper.Map<DependencyPromotionRequestDto>(request));
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

#pragma warning restore SA1402
