#pragma warning disable SA1402 // File may only contain a single type
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.Discussion.Entities;

namespace OpenGitBase.Features.Discussion.QueryHandlers;

public class CreateDiscussionQueryHandler : IQueryHandler<CreateDiscussionQuery, DiscussionDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _systemClock;

    public CreateDiscussionQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ISystemClock systemClock
    )
    {
        _contextFactory = contextFactory;
        _systemClock = systemClock;
    }

    public async Task<Option<DiscussionDto>> RunQueryAsync(
        CreateDiscussionQuery query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(query.Title) || query.RepositoryId == Guid.Empty)
        {
            return Option<DiscussionDto>.None;
        }

        var utcNow = _systemClock.UtcNow;
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var number = await DiscussionProjection
            .AllocateNumberAsync(context, query.RepositoryId, cancellationToken)
            .ConfigureAwait(false);

        var entity = new DiscussionEntity
        {
            Id = Guid.NewGuid(),
            RepositoryId = query.RepositoryId,
            Number = number,
            Title = query.Title.Trim(),
            Body = string.IsNullOrWhiteSpace(query.Body) ? null : query.Body.Trim(),
            Status = (int)DiscussionStatus.Open,
            HasEverBeenEngaged = false,
            CreatorUserId = query.CreatorUserId.Value,
            AssigneeUserId = query.AssigneeUserId?.Value,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };

        context.Set<DiscussionEntity>().Add(entity);
        await ApplyTagsAsync(context, entity, query.TagIds, cancellationToken).ConfigureAwait(false);
        await DiscussionProjection
            .EnsureSubscriptionAsync(
                context,
                entity.Id,
                query.CreatorUserId.Value,
                utcNow,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (query.AssigneeUserId is not null)
        {
            await DiscussionProjection
                .EnsureSubscriptionAsync(
                    context,
                    entity.Id,
                    query.AssigneeUserId.Value,
                    utcNow,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var loaded = await DiscussionProjection
            .FindByNumberAsync(context, query.RepositoryId, number, cancellationToken)
            .ConfigureAwait(false);

        if (loaded is null)
        {
            return Option<DiscussionDto>.None;
        }

        var userIds = new List<Guid> { loaded.CreatorUserId };
        if (loaded.AssigneeUserId is Guid assigneeUserId)
        {
            userIds.Add(assigneeUserId);
        }

        var usernames = await DiscussionProjection
            .ResolveUsernamesAsync(context, userIds, cancellationToken)
            .ConfigureAwait(false);

        return Option.From(DiscussionProjection.ToDto(loaded, usernames));
    }

    internal static async Task ApplyTagsAsync(
        OpenGitBaseDbContext context,
        DiscussionEntity entity,
        IReadOnlyList<Guid> tagIds,
        CancellationToken cancellationToken
    )
    {
        if (tagIds.Count == 0)
        {
            return;
        }

        var tags = await context
            .Set<RepositoryTagEntity>()
            .Where(t => t.RepositoryId == entity.RepositoryId && tagIds.Contains(t.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var tag in tags)
        {
            entity.TagAssignments.Add(
                new DiscussionTagAssignmentEntity { DiscussionId = entity.Id, TagId = tag.Id }
            );
        }
    }
}

public class ListDiscussionsByRepositoryQueryHandler
    : IQueryHandler<ListDiscussionsByRepositoryQuery, IReadOnlyList<DiscussionDto>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public ListDiscussionsByRepositoryQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<IReadOnlyList<DiscussionDto>>> RunQueryAsync(
        ListDiscussionsByRepositoryQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var discussions = DiscussionProjection.WithIncludes(
            context.Set<DiscussionEntity>().Where(d => d.RepositoryId == query.RepositoryId)
        );

        if (query.Status is not null)
        {
            discussions = discussions.Where(d => d.Status == (int)query.Status);
        }

        if (query.AssigneeUserId is not null)
        {
            discussions = discussions.Where(d => d.AssigneeUserId == query.AssigneeUserId);
        }

        if (query.TagId is not null)
        {
            discussions = discussions.Where(d =>
                d.TagAssignments.Any(a => a.TagId == query.TagId)
            );
        }

        var list = await discussions
            .OrderByDescending(d => d.UpdatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var userIds = list.SelectMany(d =>
        {
            var ids = new List<Guid> { d.CreatorUserId };
            if (d.AssigneeUserId is Guid assigneeUserId)
            {
                ids.Add(assigneeUserId);
            }

            return ids;
        });
        var usernames = await DiscussionProjection
            .ResolveUsernamesAsync(context, userIds, cancellationToken)
            .ConfigureAwait(false);

        return Option.From<IReadOnlyList<DiscussionDto>>(
            list.Select(entity => DiscussionProjection.ToDto(entity, usernames)).ToList()
        );
    }
}

public class GetDiscussionByNumberQueryHandler
    : IQueryHandler<GetDiscussionByNumberQuery, DiscussionDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public GetDiscussionByNumberQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<DiscussionDto>> RunQueryAsync(
        GetDiscussionByNumberQuery query,
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

        var userIds = new List<Guid> { entity.CreatorUserId };
        if (entity.AssigneeUserId is Guid assigneeUserId)
        {
            userIds.Add(assigneeUserId);
        }

        DiscussionDto dto;

        if (query.IncludeComments)
        {
            var allComments = await context
                .Set<DiscussionCommentEntity>()
                .Include(c => c.Anchor)
                .Where(c => c.DiscussionId == entity.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            userIds.AddRange(DiscussionProjection.CollectCommentUserIds(allComments));

            var usernames = await DiscussionProjection
                .ResolveUsernamesAsync(context, userIds, cancellationToken)
                .ConfigureAwait(false);

            dto = DiscussionProjection.ToDto(entity, usernames);
            dto.Comments = DiscussionProjection.BuildNestedCommentList(allComments, usernames);
        }
        else
        {
            var usernames = await DiscussionProjection
                .ResolveUsernamesAsync(context, userIds, cancellationToken)
                .ConfigureAwait(false);

            dto = DiscussionProjection.ToDto(entity, usernames);
        }

        return Option.From(dto);
    }
}
