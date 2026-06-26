#pragma warning disable SA1402 // File may only contain a single type
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.Discussion.Entities;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Discussion.QueryHandlers;

public class UpdateDiscussionMetadataQueryHandler
    : IQueryHandler<UpdateDiscussionMetadataQuery, DiscussionDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _systemClock;
    private readonly IQueryProcessor _queryProcessor;

    public UpdateDiscussionMetadataQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ISystemClock systemClock,
        IQueryProcessor queryProcessor
    )
    {
        _contextFactory = contextFactory;
        _systemClock = systemClock;
        _queryProcessor = queryProcessor;
    }

    public async Task<Option<DiscussionDto>> RunQueryAsync(
        UpdateDiscussionMetadataQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var entity = await DiscussionProjection
            .FindByNumberAsync(context, query.RepositoryId, query.Number, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return Option<DiscussionDto>.None;
        }

        if (
            entity.Status is (int)DiscussionStatus.Resolved
                or (int)DiscussionStatus.Dismissed
        )
        {
            return Option<DiscussionDto>.None;
        }

        var utcNow = _systemClock.UtcNow;
        if (!string.IsNullOrWhiteSpace(query.Title))
        {
            entity.Title = query.Title.Trim();
        }

        if (query.ClearAssignee)
        {
            entity.AssigneeUserId = null;
        }
        else if (query.AssigneeUserId is not null)
        {
            var previousAssignee = entity.AssigneeUserId;
            entity.AssigneeUserId = query.AssigneeUserId.Value;
            if (previousAssignee != entity.AssigneeUserId)
            {
                await DiscussionProjection
                    .EnsureSubscriptionAsync(
                        context,
                        entity.Id,
                        entity.AssigneeUserId.Value,
                        utcNow,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                await _queryProcessor
                    .RunQueryAsync(
                        new CreateDiscussionNotificationQuery
                        {
                            DiscussionId = DiscussionId.From(entity.Id),
                            EventType = NotificationEventType.AssigneeChanged,
                            ActorUserId = query.ActingUserId,
                            Message = "Assignee changed",
                        },
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
        }

        if (query.TagIds is not null)
        {
            entity.TagAssignments.Clear();
            await CreateDiscussionQueryHandler
                .ApplyTagsAsync(context, entity, query.TagIds, cancellationToken)
                .ConfigureAwait(false);
        }

        entity.UpdatedAt = utcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Option.From(DiscussionProjection.ToDto(entity));
    }
}

public class ResolveDiscussionQueryHandler : IQueryHandler<ResolveDiscussionQuery, DiscussionDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _systemClock;
    private readonly IQueryProcessor _queryProcessor;

    public ResolveDiscussionQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ISystemClock systemClock,
        IQueryProcessor queryProcessor
    )
    {
        _contextFactory = contextFactory;
        _systemClock = systemClock;
        _queryProcessor = queryProcessor;
    }

    public async Task<Option<DiscussionDto>> RunQueryAsync(
        ResolveDiscussionQuery query,
        CancellationToken cancellationToken
    )
    {
        return await TransitionAsync(
            query.RepositoryId,
            query.Number,
            DiscussionStatus.Resolved,
            NotificationEventType.Resolved,
            cancellationToken
        ).ConfigureAwait(false);
    }

    private async Task<Option<DiscussionDto>> TransitionAsync(
        Guid repositoryId,
        int number,
        DiscussionStatus target,
        NotificationEventType eventType,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var entity = await DiscussionProjection
            .FindByNumberAsync(context, repositoryId, number, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return Option<DiscussionDto>.None;
        }

        if (
            target == DiscussionStatus.Resolved
            && entity.Status
                is (int)DiscussionStatus.Resolved
                    or (int)DiscussionStatus.Dismissed
        )
        {
            return Option<DiscussionDto>.None;
        }

        entity.Status = (int)target;
        entity.UpdatedAt = _systemClock.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _queryProcessor
            .RunQueryAsync(
                new CreateDiscussionNotificationQuery
                {
                    DiscussionId = DiscussionId.From(entity.Id),
                    EventType = eventType,
                    ActorUserId = UserId.From(Guid.Empty),
                    Message = target.ToString(),
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return Option.From(DiscussionProjection.ToDto(entity));
    }
}

public class DismissDiscussionQueryHandler : IQueryHandler<DismissDiscussionQuery, DiscussionDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _systemClock;
    private readonly IQueryProcessor _queryProcessor;

    public DismissDiscussionQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ISystemClock systemClock,
        IQueryProcessor queryProcessor
    )
    {
        _contextFactory = contextFactory;
        _systemClock = systemClock;
        _queryProcessor = queryProcessor;
    }

    public async Task<Option<DiscussionDto>> RunQueryAsync(
        DismissDiscussionQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var entity = await DiscussionProjection
            .FindByNumberAsync(context, query.RepositoryId, query.Number, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return Option<DiscussionDto>.None;
        }

        if (
            entity.Status
                is (int)DiscussionStatus.Resolved
                    or (int)DiscussionStatus.Dismissed
        )
        {
            return Option<DiscussionDto>.None;
        }

        entity.Status = (int)DiscussionStatus.Dismissed;
        entity.UpdatedAt = _systemClock.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _queryProcessor
            .RunQueryAsync(
                new CreateDiscussionNotificationQuery
                {
                    DiscussionId = DiscussionId.From(entity.Id),
                    EventType = NotificationEventType.Dismissed,
                    ActorUserId = UserId.From(Guid.Empty),
                    Message = "Discussion dismissed",
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return Option.From(DiscussionProjection.ToDto(entity));
    }
}
