#pragma warning disable SA1402 // File may only contain a single type
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.SendGrid;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.Discussion.Entities;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Discussion.QueryHandlers;

public class CreateRepositoryTagQueryHandler : IQueryHandler<CreateRepositoryTagQuery, RepositoryTagDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _systemClock;

    public CreateRepositoryTagQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ISystemClock systemClock
    )
    {
        _contextFactory = contextFactory;
        _systemClock = systemClock;
    }

    public async Task<Option<RepositoryTagDto>> RunQueryAsync(
        CreateRepositoryTagQuery query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(query.Name))
        {
            return Option<RepositoryTagDto>.None;
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var exists = await context
            .Set<RepositoryTagEntity>()
            .AnyAsync(
                t =>
                    t.RepositoryId == query.RepositoryId
                    && t.Name == query.Name.Trim(),
                cancellationToken
            )
            .ConfigureAwait(false);

        if (exists)
        {
            return Option<RepositoryTagDto>.None;
        }

        var entity = new RepositoryTagEntity
        {
            Id = Guid.NewGuid(),
            RepositoryId = query.RepositoryId,
            Name = query.Name.Trim(),
            Color = query.Color,
            CreatedAt = _systemClock.UtcNow,
        };

        context.Set<RepositoryTagEntity>().Add(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Option.From(
            new RepositoryTagDto
            {
                Id = RepositoryTagId.From(entity.Id),
                RepositoryId = entity.RepositoryId,
                Name = entity.Name,
                Color = entity.Color,
                CreatedAt = entity.CreatedAt,
            }
        );
    }
}

public class ListRepositoryTagsQueryHandler
    : IQueryHandler<ListRepositoryTagsQuery, IReadOnlyList<RepositoryTagDto>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public ListRepositoryTagsQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<IReadOnlyList<RepositoryTagDto>>> RunQueryAsync(
        ListRepositoryTagsQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var tags = await context
            .Set<RepositoryTagEntity>()
            .Where(t => t.RepositoryId == query.RepositoryId)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Option.From<IReadOnlyList<RepositoryTagDto>>(
            tags
                .Select(t => new RepositoryTagDto
                {
                    Id = RepositoryTagId.From(t.Id),
                    RepositoryId = t.RepositoryId,
                    Name = t.Name,
                    Color = t.Color,
                    CreatedAt = t.CreatedAt,
                })
                .ToList()
        );
    }
}

public class DeleteRepositoryTagQueryHandler : IQueryHandler<DeleteRepositoryTagQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public DeleteRepositoryTagQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<Unit>> RunQueryAsync(
        DeleteRepositoryTagQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var tag = await context
            .Set<RepositoryTagEntity>()
            .FirstOrDefaultAsync(
                t => t.Id == query.TagId && t.RepositoryId == query.RepositoryId,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (tag is null)
        {
            return Option<Unit>.None;
        }

        context.Set<RepositoryTagEntity>().Remove(tag);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(Unit.Value);
    }
}

public class BlockRepositoryUserQueryHandler : IQueryHandler<BlockRepositoryUserQuery, BlockedUserDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _systemClock;

    public BlockRepositoryUserQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ISystemClock systemClock
    )
    {
        _contextFactory = contextFactory;
        _systemClock = systemClock;
    }

    public async Task<Option<BlockedUserDto>> RunQueryAsync(
        BlockRepositoryUserQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var existing = await context
            .Set<RepositoryBlockedUserEntity>()
            .FirstOrDefaultAsync(
                b =>
                    b.RepositoryId == query.RepositoryId && b.UserId == query.UserId.Value,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return Option.From(
                new BlockedUserDto
                {
                    UserId = UserId.From(existing.UserId),
                    BlockedByUserId = UserId.From(existing.BlockedByUserId),
                    BlockedAt = existing.BlockedAt,
                    Reason = existing.Reason,
                }
            );
        }

        var entity = new RepositoryBlockedUserEntity
        {
            Id = Guid.NewGuid(),
            RepositoryId = query.RepositoryId,
            UserId = query.UserId.Value,
            BlockedByUserId = query.BlockedByUserId.Value,
            BlockedAt = _systemClock.UtcNow,
            Reason = query.Reason,
        };

        context.Set<RepositoryBlockedUserEntity>().Add(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Option.From(
            new BlockedUserDto
            {
                UserId = query.UserId,
                BlockedByUserId = query.BlockedByUserId,
                BlockedAt = entity.BlockedAt,
                Reason = entity.Reason,
            }
        );
    }
}

public class UnblockRepositoryUserQueryHandler : IQueryHandler<UnblockRepositoryUserQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public UnblockRepositoryUserQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<Unit>> RunQueryAsync(
        UnblockRepositoryUserQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var entity = await context
            .Set<RepositoryBlockedUserEntity>()
            .FirstOrDefaultAsync(
                b =>
                    b.RepositoryId == query.RepositoryId && b.UserId == query.UserId.Value,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (entity is null)
        {
            return Option<Unit>.None;
        }

        context.Set<RepositoryBlockedUserEntity>().Remove(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(Unit.Value);
    }
}

public class ListBlockedRepositoryUsersQueryHandler
    : IQueryHandler<ListBlockedRepositoryUsersQuery, IReadOnlyList<BlockedUserDto>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public ListBlockedRepositoryUsersQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<IReadOnlyList<BlockedUserDto>>> RunQueryAsync(
        ListBlockedRepositoryUsersQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var list = await context
            .Set<RepositoryBlockedUserEntity>()
            .Where(b => b.RepositoryId == query.RepositoryId)
            .OrderByDescending(b => b.BlockedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Option.From<IReadOnlyList<BlockedUserDto>>(
            list
                .Select(b => new BlockedUserDto
                {
                    UserId = UserId.From(b.UserId),
                    BlockedByUserId = UserId.From(b.BlockedByUserId),
                    BlockedAt = b.BlockedAt,
                    Reason = b.Reason,
                })
                .ToList()
        );
    }
}

public class IsRepositoryUserBlockedQueryHandler : IQueryHandler<IsRepositoryUserBlockedQuery, bool>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public IsRepositoryUserBlockedQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<bool>> RunQueryAsync(
        IsRepositoryUserBlockedQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var blocked = await context
            .Set<RepositoryBlockedUserEntity>()
            .AnyAsync(
                b =>
                    b.RepositoryId == query.RepositoryId && b.UserId == query.UserId.Value,
                cancellationToken
            )
            .ConfigureAwait(false);

        return Option.From(blocked);
    }
}

public class SubscribeDiscussionQueryHandler : IQueryHandler<SubscribeDiscussionQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _systemClock;

    public SubscribeDiscussionQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ISystemClock systemClock
    )
    {
        _contextFactory = contextFactory;
        _systemClock = systemClock;
    }

    public async Task<Option<Unit>> RunQueryAsync(
        SubscribeDiscussionQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        await DiscussionProjection
            .EnsureSubscriptionAsync(
                context,
                query.DiscussionId.Value,
                query.UserId.Value,
                _systemClock.UtcNow,
                cancellationToken
            )
            .ConfigureAwait(false);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(Unit.Value);
    }
}

public class UnsubscribeDiscussionQueryHandler : IQueryHandler<UnsubscribeDiscussionQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public UnsubscribeDiscussionQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<Unit>> RunQueryAsync(
        UnsubscribeDiscussionQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var subscription = await context
            .Set<DiscussionSubscriptionEntity>()
            .FirstOrDefaultAsync(
                s =>
                    s.DiscussionId == query.DiscussionId.Value
                    && s.UserId == query.UserId.Value,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (subscription is null)
        {
            return Option.From(Unit.Value);
        }

        subscription.IsActive = false;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(Unit.Value);
    }
}

public class ListNotificationsQueryHandler
    : IQueryHandler<ListNotificationsQuery, IReadOnlyList<NotificationDto>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IQueryProcessor _queryProcessor;

    public ListNotificationsQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IQueryProcessor queryProcessor
    )
    {
        _contextFactory = contextFactory;
        _queryProcessor = queryProcessor;
    }

    public async Task<Option<IReadOnlyList<NotificationDto>>> RunQueryAsync(
        ListNotificationsQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var notifications = context
            .Set<UserNotificationEntity>()
            .Include(n => n.Discussion)
            .Where(n => n.UserId == query.UserId.Value);

        if (query.UnreadOnly)
        {
            notifications = notifications.Where(n => n.ReadAt == null);
        }

        var list = await notifications
            .OrderByDescending(n => n.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = new List<NotificationDto>();
        foreach (var notification in list)
        {
            if (notification.Discussion is null)
            {
                continue;
            }

            var repo = await _queryProcessor
                .RunQueryAsync(
                    new GetRepositoryQuery
                    {
                        ModelId = RepositoryId.From(notification.Discussion.RepositoryId),
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (repo.IsNone)
            {
                continue;
            }

            var repository = repo.Get();
            result.Add(
                new NotificationDto
                {
                    Id = NotificationId.From(notification.Id),
                    UserId = UserId.From(notification.UserId),
                    DiscussionId = DiscussionId.From(notification.DiscussionId),
                    RepositoryId = notification.Discussion.RepositoryId,
                    DiscussionNumber = notification.Discussion.Number,
                    CommentId = notification.CommentId is null
                        ? null
                        : DiscussionCommentId.From(notification.CommentId.Value),
                    OwnerSlug = repository.OwnerSlug ?? string.Empty,
                    RepositorySlug = repository.Slug,
                    EventType = (NotificationEventType)notification.EventType,
                    Message = notification.Message,
                    ActorUserId = notification.ActorUserId is null
                        ? null
                        : UserId.From(notification.ActorUserId.Value),
                    CreatedAt = notification.CreatedAt,
                    ReadAt = notification.ReadAt,
                }
            );
        }

        return Option.From<IReadOnlyList<NotificationDto>>(result);
    }
}

public class MarkNotificationReadQueryHandler : IQueryHandler<MarkNotificationReadQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _systemClock;

    public MarkNotificationReadQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ISystemClock systemClock
    )
    {
        _contextFactory = contextFactory;
        _systemClock = systemClock;
    }

    public async Task<Option<Unit>> RunQueryAsync(
        MarkNotificationReadQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var notification = await context
            .Set<UserNotificationEntity>()
            .FirstOrDefaultAsync(
                n => n.Id == query.NotificationId.Value && n.UserId == query.UserId.Value,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (notification is null)
        {
            return Option<Unit>.None;
        }

        notification.ReadAt ??= _systemClock.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(Unit.Value);
    }
}

public class CreateDiscussionNotificationQueryHandler : IQueryHandler<CreateDiscussionNotificationQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _systemClock;
    private readonly IQueryProcessor _queryProcessor;
    private readonly IEmailProtectionService _emailProtectionService;

    public CreateDiscussionNotificationQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ISystemClock systemClock,
        IQueryProcessor queryProcessor,
        IEmailProtectionService emailProtectionService
    )
    {
        _contextFactory = contextFactory;
        _systemClock = systemClock;
        _queryProcessor = queryProcessor;
        _emailProtectionService = emailProtectionService;
    }

    public async Task<Option<Unit>> RunQueryAsync(
        CreateDiscussionNotificationQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var discussion = await context
            .Set<DiscussionEntity>()
            .FirstOrDefaultAsync(d => d.Id == query.DiscussionId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (discussion is null)
        {
            return Option<Unit>.None;
        }

        var repoResult = await _queryProcessor
            .RunQueryAsync(
                new GetRepositoryQuery
                {
                    ModelId = RepositoryId.From(discussion.RepositoryId),
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (repoResult.IsNone)
        {
            return Option<Unit>.None;
        }

        var repository = repoResult.Get();
        var ownerSlug = repository.OwnerSlug ?? "owner";
        var subjectPrefix = $"[{ownerSlug}/{repository.Slug} #{discussion.Number}]";

        var subscriberIds = await context
            .Set<DiscussionSubscriptionEntity>()
            .Where(s => s.DiscussionId == discussion.Id && s.IsActive)
            .Select(s => s.UserId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var recipientIds = query.RestrictToExplicitRecipients
            ? query.AdditionalRecipientUserIds.Select(u => u.Value).Distinct().ToList()
            : subscriberIds
                .Concat(query.AdditionalRecipientUserIds.Select(u => u.Value))
                .Distinct()
                .ToList();

        var actorId = query.ActorUserId.Value == Guid.Empty ? (Guid?)null : query.ActorUserId.Value;
        if (actorId is not null)
        {
            recipientIds = recipientIds.Where(id => id != actorId.Value).ToList();
        }

        var utcNow = _systemClock.UtcNow;
        foreach (var userId in recipientIds)
        {
            context.Set<UserNotificationEntity>().Add(
                new UserNotificationEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    DiscussionId = discussion.Id,
                    CommentId = query.CommentId?.Value,
                    EventType = (int)query.EventType,
                    Message = query.Message,
                    ActorUserId = query.ActorUserId.Value == Guid.Empty
                        ? null
                        : query.ActorUserId.Value,
                    CreatedAt = utcNow,
                }
            );

            var credentials = await context
                .Set<UserCredentialsEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken)
                .ConfigureAwait(false);

            if (
                credentials is not null
                && !string.IsNullOrWhiteSpace(credentials.EmailCiphertext)
            )
            {
                var email = _emailProtectionService.DecryptEmail(credentials.EmailCiphertext);
                var discussionPath = query.CommentId is null
                    ? $"/{ownerSlug}/{repository.Slug}/discussions/{discussion.Number}"
                    : $"/{ownerSlug}/{repository.Slug}/discussions/{discussion.Number}#comment-{query.CommentId.Value}";
                await _queryProcessor
                    .RunQueryAsync(
                        new EmailSendQuery
                        {
                            To = new EmailAddress
                            {
                                Email = email,
                                Name = credentials.Username,
                            },
                            Subject = $"{subjectPrefix} {query.Message}",
                            HtmlBody =
                                $"<p>{System.Net.WebUtility.HtmlEncode(query.Message)}</p><p><a href=\"{discussionPath}\">View discussion</a></p>",
                        },
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(Unit.Value);
    }
}
