#pragma warning disable SA1402 // File may only contain a single type
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.Discussion.Entities;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Discussion.QueryHandlers;

public partial class CreateDiscussionCommentQueryHandler
    : IQueryHandler<CreateDiscussionCommentQuery, DiscussionCommentDto>
{
    private static readonly Regex MentionRegex = MentionPattern();

    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _systemClock;
    private readonly IQueryProcessor _queryProcessor;

    public CreateDiscussionCommentQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ISystemClock systemClock,
        IQueryProcessor queryProcessor
    )
    {
        _contextFactory = contextFactory;
        _systemClock = systemClock;
        _queryProcessor = queryProcessor;
    }

    public async Task<Option<DiscussionCommentDto>> RunQueryAsync(
        CreateDiscussionCommentQuery query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(query.BodyMarkdown))
        {
            return Option<DiscussionCommentDto>.None;
        }

        var utcNow = _systemClock.UtcNow;
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var discussion = await DiscussionProjection
            .FindByNumberAsync(
                context,
                query.RepositoryId,
                query.DiscussionNumber,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (discussion is null)
        {
            return Option<DiscussionCommentDto>.None;
        }

        var wasClosed =
            discussion.Status is (int)DiscussionStatus.Resolved
                or (int)DiscussionStatus.Dismissed;

        var comment = new DiscussionCommentEntity
        {
            Id = Guid.NewGuid(),
            DiscussionId = discussion.Id,
            AuthorUserId = query.AuthorUserId.Value,
            BodyMarkdown = query.BodyMarkdown.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };

        if (query.Anchor is not null && !string.IsNullOrWhiteSpace(query.Anchor.FilePath))
        {
            comment.Anchor = new CommentAnchorEntity
            {
                Id = Guid.NewGuid(),
                CommentId = comment.Id,
                Ref = query.Anchor.Ref.Trim(),
                CommitSha = query.Anchor.CommitSha.Trim(),
                FilePath = query.Anchor.FilePath.Trim(),
                Line = query.Anchor.Line,
                EndLine = query.Anchor.EndLine,
            };
        }

        context.Set<DiscussionCommentEntity>().Add(comment);

        if (wasClosed)
        {
            discussion.Status = (int)DiscussionStatus.Open;
            await _queryProcessor
                .RunQueryAsync(
                    new CreateDiscussionNotificationQuery
                    {
                        DiscussionId = DiscussionId.From(discussion.Id),
                        EventType = NotificationEventType.Reopened,
                        ActorUserId = query.AuthorUserId,
                        Message = "Discussion reopened",
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        else if (
            !discussion.HasEverBeenEngaged
            && discussion.CreatorUserId != query.AuthorUserId.Value
            && discussion.Status == (int)DiscussionStatus.Open
        )
        {
            discussion.Status = (int)DiscussionStatus.Engaged;
            discussion.HasEverBeenEngaged = true;
        }

        discussion.UpdatedAt = utcNow;

        await DiscussionProjection
            .EnsureSubscriptionAsync(
                context,
                discussion.Id,
                query.AuthorUserId.Value,
                utcNow,
                cancellationToken
            )
            .ConfigureAwait(false);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var mentionedUserIds = ParseMentions(query.BodyMarkdown);
        await _queryProcessor
            .RunQueryAsync(
                new CreateDiscussionNotificationQuery
                {
                    DiscussionId = DiscussionId.From(discussion.Id),
                    EventType = NotificationEventType.NewComment,
                    ActorUserId = query.AuthorUserId,
                    Message = "New comment",
                    AdditionalRecipientUserIds = mentionedUserIds,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        foreach (var mentionedUserId in mentionedUserIds)
        {
            await DiscussionProjection
                .EnsureSubscriptionAsync(
                    context,
                    discussion.Id,
                    mentionedUserId.Value,
                    utcNow,
                    cancellationToken
                )
                .ConfigureAwait(false);

            await _queryProcessor
                .RunQueryAsync(
                    new CreateDiscussionNotificationQuery
                    {
                        DiscussionId = DiscussionId.From(discussion.Id),
                        EventType = NotificationEventType.Mention,
                        ActorUserId = query.AuthorUserId,
                        Message = "You were mentioned",
                        AdditionalRecipientUserIds = [mentionedUserId],
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        if (mentionedUserIds.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        comment.Anchor ??= await context
            .Set<CommentAnchorEntity>()
            .FirstOrDefaultAsync(a => a.CommentId == comment.Id, cancellationToken)
            .ConfigureAwait(false);

        return Option.From(DiscussionProjection.ToCommentDto(comment));
    }

    internal static IReadOnlyList<UserId> ParseMentions(string body) =>
        MentionRegex
            .Matches(body)
            .Select(m => m.Groups[1].Value)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(u => UserId.From(Guid.Parse(u)))
            .ToList();

    [GeneratedRegex(@"@\{([0-9a-fA-F\-]{36})\}")]
    private static partial Regex MentionPattern();
}

public class ListDiscussionCommentsQueryHandler
    : IQueryHandler<ListDiscussionCommentsQuery, IReadOnlyList<DiscussionCommentDto>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public ListDiscussionCommentsQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<IReadOnlyList<DiscussionCommentDto>>> RunQueryAsync(
        ListDiscussionCommentsQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var discussion = await DiscussionProjection
            .FindByNumberAsync(
                context,
                query.RepositoryId,
                query.DiscussionNumber,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (discussion is null)
        {
            return Option<IReadOnlyList<DiscussionCommentDto>>.None;
        }

        var comments = await context
            .Set<DiscussionCommentEntity>()
            .Include(c => c.Anchor)
            .Where(c => c.DiscussionId == discussion.Id && c.DeletedAt == null)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Option.From<IReadOnlyList<DiscussionCommentDto>>(
            comments.Select(DiscussionProjection.ToCommentDto).ToList()
        );
    }
}

public class UpdateDiscussionCommentQueryHandler
    : IQueryHandler<UpdateDiscussionCommentQuery, DiscussionCommentDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _systemClock;

    public UpdateDiscussionCommentQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ISystemClock systemClock
    )
    {
        _contextFactory = contextFactory;
        _systemClock = systemClock;
    }

    public async Task<Option<DiscussionCommentDto>> RunQueryAsync(
        UpdateDiscussionCommentQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var comment = await context
            .Set<DiscussionCommentEntity>()
            .Include(c => c.Anchor)
            .FirstOrDefaultAsync(c => c.Id == query.CommentId, cancellationToken)
            .ConfigureAwait(false);

        if (comment is null || comment.DeletedAt is not null)
        {
            return Option<DiscussionCommentDto>.None;
        }

        if (comment.AuthorUserId != query.ActingUserId.Value)
        {
            return Option<DiscussionCommentDto>.None;
        }

        var utcNow = _systemClock.UtcNow;
        comment.BodyMarkdown = query.BodyMarkdown.Trim();
        comment.UpdatedAt = utcNow;
        comment.EditedAt = utcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Option.From(DiscussionProjection.ToCommentDto(comment));
    }
}

public class SoftDeleteDiscussionCommentQueryHandler
    : IQueryHandler<SoftDeleteDiscussionCommentQuery, DiscussionCommentDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _systemClock;

    public SoftDeleteDiscussionCommentQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ISystemClock systemClock
    )
    {
        _contextFactory = contextFactory;
        _systemClock = systemClock;
    }

    public async Task<Option<DiscussionCommentDto>> RunQueryAsync(
        SoftDeleteDiscussionCommentQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var comment = await context
            .Set<DiscussionCommentEntity>()
            .Include(c => c.Anchor)
            .FirstOrDefaultAsync(c => c.Id == query.CommentId, cancellationToken)
            .ConfigureAwait(false);

        if (comment is null || comment.DeletedAt is not null)
        {
            return Option<DiscussionCommentDto>.None;
        }

        if (
            comment.AuthorUserId != query.ActingUserId.Value
            && !query.IsModerator
        )
        {
            return Option<DiscussionCommentDto>.None;
        }

        var utcNow = _systemClock.UtcNow;
        comment.DeletedAt = utcNow;
        comment.DeletedByUserId = query.ActingUserId.Value;
        comment.UpdatedAt = utcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Option.From(DiscussionProjection.ToCommentDto(comment));
    }
}
