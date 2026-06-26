#pragma warning disable SA1402 // File may only contain a single type
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.Discussion.Entities;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Discussion.QueryHandlers;

public class ResolveSubThreadDiscussionCommentQueryHandler
    : IQueryHandler<ResolveSubThreadDiscussionCommentQuery, DiscussionCommentDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _systemClock;
    private readonly IQueryProcessor _queryProcessor;

    public ResolveSubThreadDiscussionCommentQueryHandler(
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
        ResolveSubThreadDiscussionCommentQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var comment = await context
            .Set<DiscussionCommentEntity>()
            .Include(c => c.Anchor)
            .Include(c => c.Discussion)
            .FirstOrDefaultAsync(c => c.Id == query.CommentId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (
            comment is null
            || comment.DeletedAt is not null
            || comment.ParentCommentId is not null
            || comment.Discussion is null
        )
        {
            return Option<DiscussionCommentDto>.None;
        }

        if (
            comment.AuthorUserId != query.ActingUserId.Value
            && !query.IsWriterPlus
        )
        {
            return Option<DiscussionCommentDto>.None;
        }

        var utcNow = _systemClock.UtcNow;
        if (comment.ResolvedAt is null)
        {
            comment.ResolvedAt = utcNow;
            comment.ResolvedByUserId = query.ActingUserId.Value;
            comment.UpdatedAt = utcNow;
            comment.Discussion.UpdatedAt = utcNow;

            var recipients = new List<UserId> { UserId.From(comment.AuthorUserId) };
            if (comment.Discussion.AssigneeUserId is Guid assigneeId)
            {
                recipients.Add(UserId.From(assigneeId));
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await _queryProcessor
                .RunQueryAsync(
                    new CreateDiscussionNotificationQuery
                    {
                        DiscussionId = DiscussionId.From(comment.Discussion.Id),
                        CommentId = DiscussionCommentId.From(comment.Id),
                        EventType = NotificationEventType.SubThreadResolved,
                        ActorUserId = query.ActingUserId,
                        Message = "Sub-thread resolved",
                        AdditionalRecipientUserIds = recipients,
                        RestrictToExplicitRecipients = true,
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        else
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var allComments = await context
            .Set<DiscussionCommentEntity>()
            .Include(c => c.Anchor)
            .Where(c => c.DiscussionId == comment.DiscussionId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var usernames = await DiscussionProjection
            .ResolveUsernamesAsync(
                context,
                DiscussionProjection.CollectCommentUserIds(allComments),
                cancellationToken
            )
            .ConfigureAwait(false);

        var nested = DiscussionProjection.BuildNestedCommentList(allComments, usernames);
        var root = nested.FirstOrDefault(c => c.Id.Value == comment.Id);
        return root is null ? Option<DiscussionCommentDto>.None : Option.From(root);
    }
}

public class UnresolveSubThreadDiscussionCommentQueryHandler
    : IQueryHandler<UnresolveSubThreadDiscussionCommentQuery, DiscussionCommentDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _systemClock;

    public UnresolveSubThreadDiscussionCommentQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ISystemClock systemClock
    )
    {
        _contextFactory = contextFactory;
        _systemClock = systemClock;
    }

    public async Task<Option<DiscussionCommentDto>> RunQueryAsync(
        UnresolveSubThreadDiscussionCommentQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var comment = await context
            .Set<DiscussionCommentEntity>()
            .Include(c => c.Anchor)
            .Include(c => c.Discussion)
            .FirstOrDefaultAsync(c => c.Id == query.CommentId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (
            comment is null
            || comment.DeletedAt is not null
            || comment.ParentCommentId is not null
            || comment.Discussion is null
        )
        {
            return Option<DiscussionCommentDto>.None;
        }

        if (
            comment.AuthorUserId != query.ActingUserId.Value
            && !query.IsWriterPlus
        )
        {
            return Option<DiscussionCommentDto>.None;
        }

        var utcNow = _systemClock.UtcNow;
        comment.ResolvedAt = null;
        comment.ResolvedByUserId = null;
        comment.UpdatedAt = utcNow;
        comment.Discussion.UpdatedAt = utcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var allComments = await context
            .Set<DiscussionCommentEntity>()
            .Include(c => c.Anchor)
            .Where(c => c.DiscussionId == comment.DiscussionId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var usernames = await DiscussionProjection
            .ResolveUsernamesAsync(
                context,
                DiscussionProjection.CollectCommentUserIds(allComments),
                cancellationToken
            )
            .ConfigureAwait(false);

        var nested = DiscussionProjection.BuildNestedCommentList(allComments, usernames);
        var root = nested.FirstOrDefault(c => c.Id.Value == comment.Id);
        return root is null ? Option<DiscussionCommentDto>.None : Option.From(root);
    }
}
