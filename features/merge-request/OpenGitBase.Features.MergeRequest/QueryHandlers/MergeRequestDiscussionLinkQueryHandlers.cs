#pragma warning disable SA1402 // File may only contain a single type
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.MergeRequest.Entities;

namespace OpenGitBase.Features.MergeRequest.QueryHandlers;

public class ListMergeRequestDiscussionLinksQueryHandler
    : IQueryHandler<ListMergeRequestDiscussionLinksQuery, IReadOnlyList<MergeRequestDiscussionLinkDto>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public ListMergeRequestDiscussionLinksQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<IReadOnlyList<MergeRequestDiscussionLinkDto>>> RunQueryAsync(
        ListMergeRequestDiscussionLinksQuery query,
        CancellationToken cancellationToken
    )
    {
        if (query.RepositoryId == Guid.Empty || query.Number <= 0)
        {
            return Option.From<IReadOnlyList<MergeRequestDiscussionLinkDto>>([]);
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var mergeRequest = await MergeRequestProjection
            .FindByNumberAsync(context, query.RepositoryId, query.Number, cancellationToken)
            .ConfigureAwait(false);

        if (mergeRequest is null)
        {
            return Option.From<IReadOnlyList<MergeRequestDiscussionLinkDto>>([]);
        }

        var links = await context
            .Set<MergeRequestDiscussionLinkEntity>()
            .AsNoTracking()
            .Where(link => link.MergeRequestId == mergeRequest.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (links.Count == 0)
        {
            return Option.From<IReadOnlyList<MergeRequestDiscussionLinkDto>>([]);
        }

        var discussionIds = links.Select(link => link.DiscussionId).Distinct().ToList();
        var discussions = await context
            .Set<OpenGitBase.Features.Discussion.Entities.DiscussionEntity>()
            .AsNoTracking()
            .Where(discussion => discussionIds.Contains(discussion.Id))
            .ToDictionaryAsync(discussion => discussion.Id, cancellationToken)
            .ConfigureAwait(false);

        return Option.From<IReadOnlyList<MergeRequestDiscussionLinkDto>>(
            links
                .Select(link =>
                {
                    discussions.TryGetValue(link.DiscussionId, out var discussion);
                    return new MergeRequestDiscussionLinkDto
                    {
                        DiscussionNumber = discussion?.Number ?? 0,
                        RelationshipType = (MergeRequestRelationshipType)link.RelationshipType,
                        DiscussionTitle = discussion?.Title,
                        DiscussionStatus = discussion is null
                            ? null
                            : ((DiscussionStatus)discussion.Status).ToString(),
                    };
                })
                .Where(dto => dto.DiscussionNumber > 0)
                .OrderBy(dto => dto.DiscussionNumber)
                .ToList()
        );
    }
}

public class CreateMergeRequestDiscussionLinkQueryHandler
    : IQueryHandler<CreateMergeRequestDiscussionLinkQuery, MergeRequestDiscussionLinkDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IQueryProcessor _queryProcessor;

    public CreateMergeRequestDiscussionLinkQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IQueryProcessor queryProcessor
    )
    {
        _contextFactory = contextFactory;
        _queryProcessor = queryProcessor;
    }

    public async Task<Option<MergeRequestDiscussionLinkDto>> RunQueryAsync(
        CreateMergeRequestDiscussionLinkQuery query,
        CancellationToken cancellationToken
    )
    {
        if (query.RepositoryId == Guid.Empty || query.Number <= 0 || query.DiscussionNumber <= 0)
        {
            return Option<MergeRequestDiscussionLinkDto>.None;
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var mergeRequest = await MergeRequestProjection
            .FindByNumberAsync(context, query.RepositoryId, query.Number, cancellationToken)
            .ConfigureAwait(false);

        if (mergeRequest is null)
        {
            return Option<MergeRequestDiscussionLinkDto>.None;
        }

        var discussionResult = await _queryProcessor
            .RunQueryAsync(
                new GetDiscussionByNumberQuery
                {
                    RepositoryId = query.RepositoryId,
                    Number = query.DiscussionNumber,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (discussionResult.IsNone)
        {
            return Option<MergeRequestDiscussionLinkDto>.None;
        }

        var discussion = discussionResult.Get();
        var exists = await context
            .Set<MergeRequestDiscussionLinkEntity>()
            .AnyAsync(
                link =>
                    link.MergeRequestId == mergeRequest.Id
                    && link.DiscussionId == discussion.Id.Value
                    && link.RelationshipType == (int)query.RelationshipType,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (exists)
        {
            return Option.From(
                new MergeRequestDiscussionLinkDto
                {
                    DiscussionNumber = discussion.Number,
                    RelationshipType = query.RelationshipType,
                    DiscussionTitle = discussion.Title,
                    DiscussionStatus = discussion.Status.ToString(),
                }
            );
        }

        context.Set<MergeRequestDiscussionLinkEntity>().Add(
            new MergeRequestDiscussionLinkEntity
            {
                MergeRequestId = mergeRequest.Id,
                DiscussionId = discussion.Id.Value,
                RelationshipType = (int)query.RelationshipType,
            }
        );
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Option.From(
            new MergeRequestDiscussionLinkDto
            {
                DiscussionNumber = discussion.Number,
                RelationshipType = query.RelationshipType,
                DiscussionTitle = discussion.Title,
                DiscussionStatus = discussion.Status.ToString(),
            }
        );
    }
}

public class DeleteMergeRequestDiscussionLinkQueryHandler
    : IQueryHandler<DeleteMergeRequestDiscussionLinkQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IQueryProcessor _queryProcessor;

    public DeleteMergeRequestDiscussionLinkQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IQueryProcessor queryProcessor
    )
    {
        _contextFactory = contextFactory;
        _queryProcessor = queryProcessor;
    }

    public async Task<Option<Unit>> RunQueryAsync(
        DeleteMergeRequestDiscussionLinkQuery query,
        CancellationToken cancellationToken
    )
    {
        if (query.RepositoryId == Guid.Empty || query.Number <= 0 || query.DiscussionNumber <= 0)
        {
            return Option<Unit>.None;
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var mergeRequest = await MergeRequestProjection
            .FindByNumberAsync(context, query.RepositoryId, query.Number, cancellationToken)
            .ConfigureAwait(false);

        if (mergeRequest is null)
        {
            return Option<Unit>.None;
        }

        var discussionResult = await _queryProcessor
            .RunQueryAsync(
                new GetDiscussionByNumberQuery
                {
                    RepositoryId = query.RepositoryId,
                    Number = query.DiscussionNumber,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (discussionResult.IsNone)
        {
            return Option<Unit>.None;
        }

        var discussionId = discussionResult.Get().Id.Value;
        var link = await context
            .Set<MergeRequestDiscussionLinkEntity>()
            .FirstOrDefaultAsync(
                entity =>
                    entity.MergeRequestId == mergeRequest.Id
                    && entity.DiscussionId == discussionId
                    && entity.RelationshipType == (int)query.RelationshipType,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (link is null)
        {
            return Option<Unit>.None;
        }

        context.Set<MergeRequestDiscussionLinkEntity>().Remove(link);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(Unit.Value);
    }
}

public class SyncMergeRequestDiscussionLinksFromBodyQueryHandler
    : IQueryHandler<SyncMergeRequestDiscussionLinksFromBodyQuery, Unit>
{
    private readonly IQueryProcessor _queryProcessor;

    public SyncMergeRequestDiscussionLinksFromBodyQueryHandler(IQueryProcessor queryProcessor)
    {
        _queryProcessor = queryProcessor;
    }

    public async Task<Option<Unit>> RunQueryAsync(
        SyncMergeRequestDiscussionLinksFromBodyQuery query,
        CancellationToken cancellationToken
    )
    {
        if (query.RepositoryId == Guid.Empty || query.Number <= 0)
        {
            return Option.From(Unit.Value);
        }

        var numbers = MergeRequestDiscussionLinkBodyParser.ParseDiscussionNumbers(query.Body);
        if (numbers.Count == 0)
        {
            return Option.From(Unit.Value);
        }

        foreach (var discussionNumber in numbers)
        {
            await _queryProcessor
                .RunQueryAsync(
                    new CreateMergeRequestDiscussionLinkQuery
                    {
                        RepositoryId = query.RepositoryId,
                        Number = query.Number,
                        DiscussionNumber = discussionNumber,
                        RelationshipType = MergeRequestRelationshipType.Related,
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        return Option.From(Unit.Value);
    }
}
