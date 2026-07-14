#pragma warning disable SA1402 // File may only contain a single type
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.Discussion.Entities;

namespace OpenGitBase.Features.Discussion.QueryHandlers;

public class ListDiscussionLinksQueryHandler
    : IQueryHandler<ListDiscussionLinksQuery, IReadOnlyList<DiscussionLinkDto>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public ListDiscussionLinksQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<IReadOnlyList<DiscussionLinkDto>>> RunQueryAsync(
        ListDiscussionLinksQuery query,
        CancellationToken cancellationToken
    )
    {
        if (query.RepositoryId == Guid.Empty || query.Number <= 0)
        {
            return Option.From<IReadOnlyList<DiscussionLinkDto>>([]);
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var sourceDiscussion = await DiscussionProjection
            .FindByNumberAsync(context, query.RepositoryId, query.Number, cancellationToken)
            .ConfigureAwait(false);

        if (sourceDiscussion is null)
        {
            return Option.From<IReadOnlyList<DiscussionLinkDto>>([]);
        }

        var links = await context
            .Set<DiscussionLinkEntity>()
            .AsNoTracking()
            .Where(link => link.SourceDiscussionId == sourceDiscussion.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (links.Count == 0)
        {
            return Option.From<IReadOnlyList<DiscussionLinkDto>>([]);
        }

        var targetIds = links.Select(link => link.TargetDiscussionId).Distinct().ToList();
        var targets = await context
            .Set<DiscussionEntity>()
            .AsNoTracking()
            .Where(discussion => targetIds.Contains(discussion.Id))
            .ToDictionaryAsync(discussion => discussion.Id, cancellationToken)
            .ConfigureAwait(false);

        return Option.From<IReadOnlyList<DiscussionLinkDto>>(
            links
                .Select(link =>
                {
                    targets.TryGetValue(link.TargetDiscussionId, out var target);
                    return new DiscussionLinkDto
                    {
                        TargetDiscussionNumber = target?.Number ?? 0,
                        RelationshipType = (DiscussionRelationshipType)link.RelationshipType,
                        TargetDiscussionTitle = target?.Title,
                        TargetDiscussionStatus = target is null
                            ? null
                            : ((DiscussionStatus)target.Status).ToString(),
                    };
                })
                .Where(dto => dto.TargetDiscussionNumber > 0)
                .OrderBy(dto => dto.TargetDiscussionNumber)
                .ToList()
        );
    }
}

public class CreateDiscussionLinkQueryHandler
    : IQueryHandler<CreateDiscussionLinkQuery, DiscussionLinkDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _clock;

    public CreateDiscussionLinkQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ISystemClock clock
    )
    {
        _contextFactory = contextFactory;
        _clock = clock;
    }

    public async Task<Option<DiscussionLinkDto>> RunQueryAsync(
        CreateDiscussionLinkQuery query,
        CancellationToken cancellationToken
    )
    {
        if (
            query.RepositoryId == Guid.Empty
            || query.Number <= 0
            || query.TargetDiscussionNumber <= 0
            || query.Number == query.TargetDiscussionNumber
        )
        {
            return Option<DiscussionLinkDto>.None;
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var sourceDiscussion = await DiscussionProjection
            .FindByNumberAsync(context, query.RepositoryId, query.Number, cancellationToken)
            .ConfigureAwait(false);

        if (sourceDiscussion is null)
        {
            return Option<DiscussionLinkDto>.None;
        }

        var targetDiscussion = await DiscussionProjection
            .FindByNumberAsync(
                context,
                query.RepositoryId,
                query.TargetDiscussionNumber,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (targetDiscussion is null)
        {
            return Option<DiscussionLinkDto>.None;
        }

        var exists = await context
            .Set<DiscussionLinkEntity>()
            .AnyAsync(
                link =>
                    link.SourceDiscussionId == sourceDiscussion.Id
                    && link.TargetDiscussionId == targetDiscussion.Id
                    && link.RelationshipType == (int)query.RelationshipType,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (exists)
        {
            return Option.From(
                new DiscussionLinkDto
                {
                    TargetDiscussionNumber = targetDiscussion.Number,
                    RelationshipType = query.RelationshipType,
                    TargetDiscussionTitle = targetDiscussion.Title,
                    TargetDiscussionStatus = ((DiscussionStatus)targetDiscussion.Status).ToString(),
                }
            );
        }

        context.Set<DiscussionLinkEntity>().Add(
            new DiscussionLinkEntity
            {
                SourceDiscussionId = sourceDiscussion.Id,
                TargetDiscussionId = targetDiscussion.Id,
                RelationshipType = (int)query.RelationshipType,
                CreatedAt = _clock.UtcNow,
            }
        );
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Option.From(
            new DiscussionLinkDto
            {
                TargetDiscussionNumber = targetDiscussion.Number,
                RelationshipType = query.RelationshipType,
                TargetDiscussionTitle = targetDiscussion.Title,
                TargetDiscussionStatus = ((DiscussionStatus)targetDiscussion.Status).ToString(),
            }
        );
    }
}

public class DeleteDiscussionLinkQueryHandler : IQueryHandler<DeleteDiscussionLinkQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public DeleteDiscussionLinkQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<Unit>> RunQueryAsync(
        DeleteDiscussionLinkQuery query,
        CancellationToken cancellationToken
    )
    {
        if (
            query.RepositoryId == Guid.Empty
            || query.Number <= 0
            || query.TargetDiscussionNumber <= 0
        )
        {
            return Option<Unit>.None;
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var sourceDiscussion = await DiscussionProjection
            .FindByNumberAsync(context, query.RepositoryId, query.Number, cancellationToken)
            .ConfigureAwait(false);

        if (sourceDiscussion is null)
        {
            return Option<Unit>.None;
        }

        var targetDiscussion = await DiscussionProjection
            .FindByNumberAsync(
                context,
                query.RepositoryId,
                query.TargetDiscussionNumber,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (targetDiscussion is null)
        {
            return Option<Unit>.None;
        }

        var link = await context
            .Set<DiscussionLinkEntity>()
            .FirstOrDefaultAsync(
                entity =>
                    entity.SourceDiscussionId == sourceDiscussion.Id
                    && entity.TargetDiscussionId == targetDiscussion.Id
                    && entity.RelationshipType == (int)query.RelationshipType,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (link is null)
        {
            return Option<Unit>.None;
        }

        context.Set<DiscussionLinkEntity>().Remove(link);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(Unit.Value);
    }
}
