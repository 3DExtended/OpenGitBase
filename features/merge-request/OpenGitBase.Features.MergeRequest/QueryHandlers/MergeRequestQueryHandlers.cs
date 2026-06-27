#pragma warning disable SA1402 // File may only contain a single type
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.MergeRequest.Entities;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Features.MergeRequest.QueryHandlers;

public class CreateMergeRequestQueryHandler : IQueryHandler<CreateMergeRequestQuery, MergeRequestDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _systemClock;
    private readonly IQueryProcessor _queryProcessor;
    private readonly MergeRequestGateCoordinator _gateCoordinator;

    public CreateMergeRequestQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ISystemClock systemClock,
        IQueryProcessor queryProcessor
    )
    {
        _contextFactory = contextFactory;
        _systemClock = systemClock;
        _queryProcessor = queryProcessor;
        _gateCoordinator = new MergeRequestGateCoordinator(systemClock);
    }

    public async Task<Option<MergeRequestDto>> RunQueryAsync(
        CreateMergeRequestQuery query,
        CancellationToken cancellationToken
    )
    {
        if (
            query.RepositoryId == Guid.Empty
            || string.IsNullOrWhiteSpace(query.Title)
            || string.IsNullOrWhiteSpace(query.SourceRef)
            || string.IsNullOrWhiteSpace(query.TargetRef)
            || string.IsNullOrWhiteSpace(query.SourceHeadSha)
            || string.IsNullOrWhiteSpace(query.TargetBaseSha)
            || string.Equals(query.SourceRef, query.TargetRef, StringComparison.OrdinalIgnoreCase)
        )
        {
            return Option<MergeRequestDto>.None;
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        if (
            await MergeRequestProjection
                .HasActivePairAsync(
                    context,
                    query.RepositoryId,
                    query.SourceRef.Trim(),
                    query.TargetRef.Trim(),
                    cancellationToken
                )
                .ConfigureAwait(false)
        )
        {
            return Option<MergeRequestDto>.None;
        }

        var utcNow = _systemClock.UtcNow;
        var number = await MergeRequestProjection
            .AllocateNumberAsync(context, query.RepositoryId, cancellationToken)
            .ConfigureAwait(false);

        var status = query.IsDraft ? MergeRequestStatus.Draft : MergeRequestStatus.Open;
        var entity = new MergeRequestEntity
        {
            Id = Guid.NewGuid(),
            RepositoryId = query.RepositoryId,
            Number = number,
            Title = query.Title.Trim(),
            Body = string.IsNullOrWhiteSpace(query.Body) ? null : query.Body.Trim(),
            Status = (int)status,
            IsDraft = query.IsDraft,
            SourceRef = query.SourceRef.Trim(),
            TargetRef = query.TargetRef.Trim(),
            SourceHeadSha = query.SourceHeadSha.Trim(),
            TargetBaseSha = query.TargetBaseSha.Trim(),
            CreatorUserId = query.CreatorUserId.Value,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };

        context.Set<MergeRequestEntity>().Add(entity);
        if (!query.IsDraft)
        {
            var defaultBranch = await _queryProcessor
                .RunQueryAsync(
                    new GetRepositoryQuery { ModelId = RepositoryId.From(query.RepositoryId) },
                    cancellationToken
                )
                .ConfigureAwait(false);
            var targetPolicy = await _gateCoordinator
                .LoadTargetPolicyAsync(
                    _queryProcessor,
                    query.RepositoryId,
                    entity.TargetRef,
                    defaultBranch.IsSome ? defaultBranch.Get().DefaultBranchName : null,
                    cancellationToken
                )
                .ConfigureAwait(false);
            await _gateCoordinator
                .EvaluateAndTransitionAsync(context, entity, targetPolicy, cancellationToken)
                .ConfigureAwait(false);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(entity.Body))
        {
            await _queryProcessor
                .RunQueryAsync(
                    new SyncMergeRequestDiscussionLinksFromBodyQuery
                    {
                        RepositoryId = query.RepositoryId,
                        Number = entity.Number,
                        Body = entity.Body,
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        var usernames = await MergeRequestProjection
            .ResolveUsernamesAsync(context, [entity.CreatorUserId], cancellationToken)
            .ConfigureAwait(false);

        var policy = await _gateCoordinator
            .LoadTargetPolicyAsync(
                _queryProcessor,
                query.RepositoryId,
                entity.TargetRef,
                null,
                cancellationToken
            )
            .ConfigureAwait(false);
        var approvals = await _gateCoordinator
            .LoadApprovalsAsync(context, entity.Id, cancellationToken)
            .ConfigureAwait(false);

        return Option.From(
            MergeRequestProjection.ToDto(
                entity,
                usernames,
                approvals,
                policy.RequiredApprovalCount
            )
        );
    }
}

public class ListMergeRequestsByRepositoryQueryHandler
    : IQueryHandler<ListMergeRequestsByRepositoryQuery, IReadOnlyList<MergeRequestDto>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public ListMergeRequestsByRepositoryQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<IReadOnlyList<MergeRequestDto>>> RunQueryAsync(
        ListMergeRequestsByRepositoryQuery query,
        CancellationToken cancellationToken
    )
    {
        if (query.RepositoryId == Guid.Empty)
        {
            return Option.From<IReadOnlyList<MergeRequestDto>>([]);
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var entitiesQuery = context
            .Set<MergeRequestEntity>()
            .AsNoTracking()
            .Where(entity => entity.RepositoryId == query.RepositoryId);

        if (query.Status is MergeRequestStatus status)
        {
            entitiesQuery = entitiesQuery.Where(entity => entity.Status == (int)status);
        }

        var entities = await entitiesQuery.ToListAsync(cancellationToken).ConfigureAwait(false);
        entities = entities.OrderByDescending(entity => entity.UpdatedAt).ToList();

        var usernames = await MergeRequestProjection
            .ResolveUsernamesAsync(
                context,
                entities.Select(entity => entity.CreatorUserId),
                cancellationToken
            )
            .ConfigureAwait(false);

        return Option.From<IReadOnlyList<MergeRequestDto>>(
            entities.Select(entity => MergeRequestProjection.ToDto(entity, usernames)).ToList()
        );
    }
}

public class GetMergeRequestByNumberQueryHandler
    : IQueryHandler<GetMergeRequestByNumberQuery, MergeRequestDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public GetMergeRequestByNumberQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<MergeRequestDto>> RunQueryAsync(
        GetMergeRequestByNumberQuery query,
        CancellationToken cancellationToken
    )
    {
        if (query.RepositoryId == Guid.Empty || query.Number <= 0)
        {
            return Option<MergeRequestDto>.None;
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var entity = await MergeRequestProjection
            .FindByNumberAsync(context, query.RepositoryId, query.Number, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return Option<MergeRequestDto>.None;
        }

        var usernames = await MergeRequestProjection
            .ResolveUsernamesAsync(context, [entity.CreatorUserId], cancellationToken)
            .ConfigureAwait(false);

        return Option.From(MergeRequestProjection.ToDto(entity, usernames));
    }
}

public class UpdateMergeRequestMetadataQueryHandler
    : IQueryHandler<UpdateMergeRequestMetadataQuery, MergeRequestDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _systemClock;
    private readonly IQueryProcessor _queryProcessor;

    public UpdateMergeRequestMetadataQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ISystemClock systemClock,
        IQueryProcessor queryProcessor
    )
    {
        _contextFactory = contextFactory;
        _systemClock = systemClock;
        _queryProcessor = queryProcessor;
    }

    public async Task<Option<MergeRequestDto>> RunQueryAsync(
        UpdateMergeRequestMetadataQuery query,
        CancellationToken cancellationToken
    )
    {
        if (query.RepositoryId == Guid.Empty || query.Number <= 0)
        {
            return Option<MergeRequestDto>.None;
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var entity = await MergeRequestProjection
            .FindByNumberAsync(context, query.RepositoryId, query.Number, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return Option<MergeRequestDto>.None;
        }

        if (!string.IsNullOrWhiteSpace(query.Title))
        {
            entity.Title = query.Title.Trim();
        }

        if (query.ClearBody)
        {
            entity.Body = null;
        }
        else if (query.Body is not null)
        {
            entity.Body = string.IsNullOrWhiteSpace(query.Body) ? null : query.Body.Trim();
        }

        entity.UpdatedAt = _systemClock.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (query.Body is not null || query.ClearBody)
        {
            await _queryProcessor
                .RunQueryAsync(
                    new SyncMergeRequestDiscussionLinksFromBodyQuery
                    {
                        RepositoryId = query.RepositoryId,
                        Number = query.Number,
                        Body = entity.Body,
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        var usernames = await MergeRequestProjection
            .ResolveUsernamesAsync(context, [entity.CreatorUserId], cancellationToken)
            .ConfigureAwait(false);

        return Option.From(MergeRequestProjection.ToDto(entity, usernames));
    }
}

public class PublishMergeRequestQueryHandler
    : IQueryHandler<PublishMergeRequestQuery, MergeRequestDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _systemClock;
    private readonly IQueryProcessor _queryProcessor;
    private readonly MergeRequestGateCoordinator _gateCoordinator;

    public PublishMergeRequestQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ISystemClock systemClock,
        IQueryProcessor queryProcessor
    )
    {
        _contextFactory = contextFactory;
        _systemClock = systemClock;
        _queryProcessor = queryProcessor;
        _gateCoordinator = new MergeRequestGateCoordinator(systemClock);
    }

    public async Task<Option<MergeRequestDto>> RunQueryAsync(
        PublishMergeRequestQuery query,
        CancellationToken cancellationToken
    )
    {
        if (query.RepositoryId == Guid.Empty || query.Number <= 0)
        {
            return Option<MergeRequestDto>.None;
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var entity = await MergeRequestProjection
            .FindByNumberAsync(context, query.RepositoryId, query.Number, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null || entity.Status != (int)MergeRequestStatus.Draft)
        {
            return Option<MergeRequestDto>.None;
        }

        entity.Status = (int)MergeRequestStatus.Open;
        entity.IsDraft = false;
        entity.UpdatedAt = _systemClock.UtcNow;

        var defaultBranch = await _queryProcessor
            .RunQueryAsync(
                new GetRepositoryQuery { ModelId = RepositoryId.From(query.RepositoryId) },
                cancellationToken
            )
            .ConfigureAwait(false);
        var targetPolicy = await _gateCoordinator
            .LoadTargetPolicyAsync(
                _queryProcessor,
                query.RepositoryId,
                entity.TargetRef,
                defaultBranch.IsSome ? defaultBranch.Get().DefaultBranchName : null,
                cancellationToken
            )
            .ConfigureAwait(false);
        await _gateCoordinator
            .EvaluateAndTransitionAsync(context, entity, targetPolicy, cancellationToken)
            .ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Option.From(
            await _gateCoordinator
                .BuildDtoAsync(context, entity, targetPolicy, cancellationToken)
                .ConfigureAwait(false)
        );
    }
}

public class CloseMergeRequestQueryHandler : IQueryHandler<CloseMergeRequestQuery, MergeRequestDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _systemClock;

    public CloseMergeRequestQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ISystemClock systemClock
    )
    {
        _contextFactory = contextFactory;
        _systemClock = systemClock;
    }

    public async Task<Option<MergeRequestDto>> RunQueryAsync(
        CloseMergeRequestQuery query,
        CancellationToken cancellationToken
    )
    {
        if (query.RepositoryId == Guid.Empty || query.Number <= 0)
        {
            return Option<MergeRequestDto>.None;
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var entity = await MergeRequestProjection
            .FindByNumberAsync(context, query.RepositoryId, query.Number, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return Option<MergeRequestDto>.None;
        }

        var status = (MergeRequestStatus)entity.Status;
        if (status is not (MergeRequestStatus.Draft or MergeRequestStatus.Open or MergeRequestStatus.Approved))
        {
            return Option<MergeRequestDto>.None;
        }

        entity.Status = (int)MergeRequestStatus.Closed;
        entity.IsDraft = false;
        entity.UpdatedAt = _systemClock.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var usernames = await MergeRequestProjection
            .ResolveUsernamesAsync(context, [entity.CreatorUserId], cancellationToken)
            .ConfigureAwait(false);

        return Option.From(MergeRequestProjection.ToDto(entity, usernames));
    }
}

public class RefreshMergeRequestShasQueryHandler
    : IQueryHandler<RefreshMergeRequestShasQuery, MergeRequestDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _systemClock;
    private readonly IQueryProcessor _queryProcessor;
    private readonly MergeRequestGateCoordinator _gateCoordinator;

    public RefreshMergeRequestShasQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ISystemClock systemClock,
        IQueryProcessor queryProcessor
    )
    {
        _contextFactory = contextFactory;
        _systemClock = systemClock;
        _queryProcessor = queryProcessor;
        _gateCoordinator = new MergeRequestGateCoordinator(systemClock);
    }

    public async Task<Option<MergeRequestDto>> RunQueryAsync(
        RefreshMergeRequestShasQuery query,
        CancellationToken cancellationToken
    )
    {
        if (
            query.RepositoryId == Guid.Empty
            || query.Number <= 0
            || string.IsNullOrWhiteSpace(query.SourceHeadSha)
            || string.IsNullOrWhiteSpace(query.TargetBaseSha)
        )
        {
            return Option<MergeRequestDto>.None;
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var entity = await MergeRequestProjection
            .FindByNumberAsync(context, query.RepositoryId, query.Number, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return Option<MergeRequestDto>.None;
        }

        var sourceSha = query.SourceHeadSha.Trim();
        var targetSha = query.TargetBaseSha.Trim();
        var previousSourceSha = entity.SourceHeadSha;
        var sourceChanged = !string.Equals(
            previousSourceSha,
            sourceSha,
            StringComparison.OrdinalIgnoreCase
        );
        var targetChanged = !string.Equals(
            entity.TargetBaseSha,
            targetSha,
            StringComparison.OrdinalIgnoreCase
        );

        if (sourceChanged || targetChanged)
        {
            entity.SourceHeadSha = sourceSha;
            entity.TargetBaseSha = targetSha;
        }

        var defaultBranch = await _queryProcessor
            .RunQueryAsync(
                new GetRepositoryQuery { ModelId = RepositoryId.From(query.RepositoryId) },
                cancellationToken
            )
            .ConfigureAwait(false);
        var targetPolicy = await _gateCoordinator
            .LoadTargetPolicyAsync(
                _queryProcessor,
                query.RepositoryId,
                entity.TargetRef,
                defaultBranch.IsSome ? defaultBranch.Get().DefaultBranchName : null,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (sourceChanged)
        {
            await _gateCoordinator
                .HandleSourceHeadChangedAsync(
                    context,
                    entity,
                    targetPolicy,
                    previousSourceSha,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        else if (targetChanged)
        {
            entity.UpdatedAt = _systemClock.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Option.From(
            await _gateCoordinator
                .BuildDtoAsync(context, entity, targetPolicy, cancellationToken)
                .ConfigureAwait(false)
        );
    }
}
