#pragma warning disable SA1402 // File may only contain a single type
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("repository/by-slug/{owner}/{slug}/tags")]
public sealed class RepositoryDiscussionTagsController : ControllerBase
{
    private readonly DiscussionAuthorizationService _authorization;
    private readonly IQueryProcessor _queryProcessor;

    public RepositoryDiscussionTagsController(
        DiscussionAuthorizationService authorization,
        IQueryProcessor queryProcessor
    )
    {
        _authorization = authorization;
        _queryProcessor = queryProcessor;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        string owner,
        string slug,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeReadAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != RepositoryContentAccessResultKind.Allowed || access.Repository is null)
        {
            return access.Kind == RepositoryContentAccessResultKind.NotFound ? NotFound() : Forbid();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new ListRepositoryTagsQuery { RepositoryId = access.Repository.Id.Value },
                cancellationToken
            )
            .ConfigureAwait(false);

        return Ok(result.Get());
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        string owner,
        string slug,
        [FromBody] CreateRepositoryTagRequest request,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeParticipateAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != DiscussionParticipationResultKind.Allowed || access.Role < RepositoryRole.Writer)
        {
            return access.Kind == DiscussionParticipationResultKind.Allowed ? Forbid() : Unauthorized();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new CreateRepositoryTagQuery
                {
                    RepositoryId = access.Repository!.Id.Value,
                    Name = request.Name,
                    Color = request.Color,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsNone ? BadRequest() : Ok(result.Get());
    }

    [HttpDelete("{tagId:guid}")]
    public async Task<IActionResult> Delete(
        string owner,
        string slug,
        Guid tagId,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeParticipateAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != DiscussionParticipationResultKind.Allowed || access.Role < RepositoryRole.Writer)
        {
            return Forbid();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new DeleteRepositoryTagQuery
                {
                    TagId = tagId,
                    RepositoryId = access.Repository!.Id.Value,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsNone ? NotFound() : NoContent();
    }
}

[ApiController]
[Authorize]
[Route("repository/by-slug/{owner}/{slug}/blocked-users")]
public sealed class RepositoryBlockedUsersController : ControllerBase
{
    private readonly DiscussionAuthorizationService _authorization;
    private readonly IQueryProcessor _queryProcessor;

    public RepositoryBlockedUsersController(
        DiscussionAuthorizationService authorization,
        IQueryProcessor queryProcessor
    )
    {
        _authorization = authorization;
        _queryProcessor = queryProcessor;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        string owner,
        string slug,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeParticipateAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != DiscussionParticipationResultKind.Allowed || access.Role < RepositoryRole.Admin)
        {
            return Forbid();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new ListBlockedRepositoryUsersQuery
                {
                    RepositoryId = access.Repository!.Id.Value,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return Ok(result.Get());
    }

    [HttpPost]
    public async Task<IActionResult> Block(
        string owner,
        string slug,
        [FromBody] BlockRepositoryUserRequest request,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeParticipateAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != DiscussionParticipationResultKind.Allowed || access.Role < RepositoryRole.Admin)
        {
            return Forbid();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new BlockRepositoryUserQuery
                {
                    RepositoryId = access.Repository!.Id.Value,
                    UserId = UserId.From(request.UserId),
                    BlockedByUserId = access.UserId!,
                    Reason = request.Reason,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsNone ? BadRequest() : Ok(result.Get());
    }

    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> Unblock(
        string owner,
        string slug,
        Guid userId,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeParticipateAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != DiscussionParticipationResultKind.Allowed || access.Role < RepositoryRole.Admin)
        {
            return Forbid();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new UnblockRepositoryUserQuery
                {
                    RepositoryId = access.Repository!.Id.Value,
                    UserId = UserId.From(userId),
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsNone ? NotFound() : NoContent();
    }
}

[ApiController]
[Authorize]
[Route("notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly DiscussionAuthorizationService _authorization;
    private readonly IQueryProcessor _queryProcessor;

    public NotificationsController(
        DiscussionAuthorizationService authorization,
        IQueryProcessor queryProcessor
    )
    {
        _authorization = authorization;
        _queryProcessor = queryProcessor;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] bool unreadOnly,
        CancellationToken cancellationToken
    )
    {
        var userId = _authorization.TryGetAuthenticatedUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new ListNotificationsQuery { UserId = userId, UnreadOnly = unreadOnly },
                cancellationToken
            )
            .ConfigureAwait(false);

        return Ok(result.Get());
    }

    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(
        Guid notificationId,
        CancellationToken cancellationToken
    )
    {
        var userId = _authorization.TryGetAuthenticatedUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new MarkNotificationReadQuery
                {
                    NotificationId = NotificationId.From(notificationId),
                    UserId = userId,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsNone ? NotFound() : NoContent();
    }
}
