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
[Route("repository/by-slug/{owner}/{slug}/discussions")]
public sealed class RepositoryDiscussionsController : ControllerBase
{
    private readonly DiscussionAuthorizationService _authorization;
    private readonly IQueryProcessor _queryProcessor;

    public RepositoryDiscussionsController(
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
        [FromQuery] DiscussionStatus? status,
        [FromQuery] Guid? assigneeUserId,
        [FromQuery] Guid? tagId,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeReadAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != RepositoryContentAccessResultKind.Allowed || access.Repository is null)
        {
            return ToReadResult(access);
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new ListDiscussionsByRepositoryQuery
                {
                    RepositoryId = access.Repository.Id.Value,
                    Status = status,
                    AssigneeUserId = assigneeUserId,
                    TagId = tagId,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return Ok(result.Get());
    }

    [HttpGet("{number:int}")]
    public async Task<IActionResult> Get(
        string owner,
        string slug,
        int number,
        [FromQuery] string? include,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeReadAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != RepositoryContentAccessResultKind.Allowed || access.Repository is null)
        {
            return ToReadResult(access);
        }

        var includeComments = string.Equals(include, "comments", StringComparison.OrdinalIgnoreCase)
            || (include?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains("comments", StringComparer.OrdinalIgnoreCase) ?? false);

        var result = await _queryProcessor
            .RunQueryAsync(
                new GetDiscussionByNumberQuery
                {
                    RepositoryId = access.Repository.Id.Value,
                    Number = number,
                    IncludeComments = includeComments,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (result.IsNone)
        {
            return NotFound();
        }

        var discussion = result.Get();
        var viewerUserId = _authorization.TryGetAuthenticatedUserId();
        if (viewerUserId is not null)
        {
            var role = await _authorization
                .GetEffectiveRoleAsync(access.Repository, viewerUserId, cancellationToken)
                .ConfigureAwait(false);
            discussion.ViewerEffectiveRole = role.ToString();
        }

        return Ok(discussion);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        string owner,
        string slug,
        [FromBody] CreateDiscussionRequest request,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeParticipateAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != DiscussionParticipationResultKind.Allowed || access.Repository is null)
        {
            return ToParticipationResult(access);
        }

        var title = request.Title;
        if (
            string.IsNullOrWhiteSpace(title)
            && request.Anchor is not null
            && !string.IsNullOrWhiteSpace(request.Anchor.FilePath)
        )
        {
            title = $"Note on `{request.Anchor.FilePath}:{request.Anchor.Line}`";
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new CreateDiscussionQuery
                {
                    RepositoryId = access.Repository.Id.Value,
                    CreatorUserId = access.UserId!,
                    Title = title,
                    Body = request.Body,
                    AssigneeUserId = request.AssigneeUserId is null
                        ? null
                        : UserId.From(request.AssigneeUserId.Value),
                    TagIds = request.TagIds,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (result.IsNone)
        {
            return BadRequest();
        }

        var discussion = result.Get();

        if (!string.IsNullOrWhiteSpace(request.Body))
        {
            await _queryProcessor
                .RunQueryAsync(
                    new CreateDiscussionCommentQuery
                    {
                        RepositoryId = access.Repository.Id.Value,
                        DiscussionNumber = discussion.Number,
                        AuthorUserId = access.UserId!,
                        BodyMarkdown = request.Body,
                        Anchor = request.Anchor is null ? null : ToAnchorInput(request.Anchor),
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        return CreatedAtAction(
            nameof(Get),
            new { owner, slug, number = discussion.Number },
            discussion
        );
    }

    [HttpPatch("{number:int}")]
    public async Task<IActionResult> Update(
        string owner,
        string slug,
        int number,
        [FromBody] UpdateDiscussionRequest request,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeParticipateAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != DiscussionParticipationResultKind.Allowed || access.Repository is null)
        {
            return ToParticipationResult(access);
        }

        var discussionResult = await _queryProcessor
            .RunQueryAsync(
                new GetDiscussionByNumberQuery
                {
                    RepositoryId = access.Repository.Id.Value,
                    Number = number,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (discussionResult.IsNone)
        {
            return NotFound();
        }

        var discussion = discussionResult.Get();
        var isCreator = discussion.CreatorUserId == access.UserId;
        var isWriter = access.Role >= RepositoryRole.Writer;

        if (!isCreator && !isWriter)
        {
            return Forbid();
        }

        if (isCreator && !isWriter && (request.AssigneeUserId is not null || request.ClearAssignee))
        {
            return Forbid();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new UpdateDiscussionMetadataQuery
                {
                    RepositoryId = access.Repository.Id.Value,
                    Number = number,
                    ActingUserId = access.UserId!,
                    Title = request.Title,
                    AssigneeUserId = request.AssigneeUserId is null
                        ? null
                        : UserId.From(request.AssigneeUserId.Value),
                    ClearAssignee = request.ClearAssignee,
                    TagIds = isWriter || isCreator ? request.TagIds : null,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsNone ? BadRequest() : Ok(result.Get());
    }

    [HttpPost("{number:int}/resolve")]
    public async Task<IActionResult> Resolve(
        string owner,
        string slug,
        int number,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeParticipateAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != DiscussionParticipationResultKind.Allowed || access.Role < RepositoryRole.Writer)
        {
            return access.Kind == DiscussionParticipationResultKind.Allowed
                ? Forbid()
                : ToParticipationResult(access);
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new ResolveDiscussionQuery
                {
                    RepositoryId = access.Repository!.Id.Value,
                    Number = number,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsNone ? BadRequest() : Ok(result.Get());
    }

    [HttpPost("{number:int}/dismiss")]
    public async Task<IActionResult> Dismiss(
        string owner,
        string slug,
        int number,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeParticipateAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != DiscussionParticipationResultKind.Allowed || access.Role < RepositoryRole.Writer)
        {
            return access.Kind == DiscussionParticipationResultKind.Allowed
                ? Forbid()
                : ToParticipationResult(access);
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new DismissDiscussionQuery
                {
                    RepositoryId = access.Repository!.Id.Value,
                    Number = number,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsNone ? BadRequest() : Ok(result.Get());
    }

    [HttpGet("{number:int}/comments")]
    public async Task<IActionResult> ListComments(
        string owner,
        string slug,
        int number,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeReadAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != RepositoryContentAccessResultKind.Allowed || access.Repository is null)
        {
            return ToReadResult(access);
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new ListDiscussionCommentsQuery
                {
                    RepositoryId = access.Repository.Id.Value,
                    DiscussionNumber = number,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsNone ? NotFound() : Ok(result.Get());
    }

    [HttpPost("{number:int}/comments")]
    public async Task<IActionResult> CreateComment(
        string owner,
        string slug,
        int number,
        [FromBody] CreateDiscussionCommentRequest request,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeParticipateAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != DiscussionParticipationResultKind.Allowed)
        {
            return ToParticipationResult(access);
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new CreateDiscussionCommentQuery
                {
                    RepositoryId = access.Repository!.Id.Value,
                    DiscussionNumber = number,
                    AuthorUserId = access.UserId!,
                    BodyMarkdown = request.BodyMarkdown,
                    ParentCommentId = request.ParentCommentId is null
                        ? null
                        : DiscussionCommentId.From(request.ParentCommentId.Value),
                    Anchor = request.Anchor is null ? null : ToAnchorInput(request.Anchor),
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsNone ? BadRequest() : Ok(result.Get());
    }

    [HttpPost("comments/{commentId:guid}/resolve")]
    public async Task<IActionResult> ResolveSubThread(
        string owner,
        string slug,
        Guid commentId,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeParticipateAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != DiscussionParticipationResultKind.Allowed)
        {
            return ToParticipationResult(access);
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new ResolveSubThreadDiscussionCommentQuery
                {
                    CommentId = DiscussionCommentId.From(commentId),
                    ActingUserId = access.UserId!,
                    IsWriterPlus = access.Role >= RepositoryRole.Writer,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsNone ? BadRequest() : Ok(result.Get());
    }

    [HttpPost("comments/{commentId:guid}/unresolve")]
    public async Task<IActionResult> UnresolveSubThread(
        string owner,
        string slug,
        Guid commentId,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeParticipateAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != DiscussionParticipationResultKind.Allowed)
        {
            return ToParticipationResult(access);
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new UnresolveSubThreadDiscussionCommentQuery
                {
                    CommentId = DiscussionCommentId.From(commentId),
                    ActingUserId = access.UserId!,
                    IsWriterPlus = access.Role >= RepositoryRole.Writer,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsNone ? BadRequest() : Ok(result.Get());
    }

    [HttpPatch("comments/{commentId:guid}")]
    public async Task<IActionResult> UpdateComment(
        string owner,
        string slug,
        Guid commentId,
        [FromBody] UpdateDiscussionCommentRequest request,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeParticipateAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != DiscussionParticipationResultKind.Allowed)
        {
            return ToParticipationResult(access);
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new UpdateDiscussionCommentQuery
                {
                    CommentId = commentId,
                    ActingUserId = access.UserId!,
                    BodyMarkdown = request.BodyMarkdown,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsNone ? NotFound() : Ok(result.Get());
    }

    [HttpDelete("comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(
        string owner,
        string slug,
        Guid commentId,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeParticipateAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != DiscussionParticipationResultKind.Allowed)
        {
            return ToParticipationResult(access);
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new SoftDeleteDiscussionCommentQuery
                {
                    CommentId = commentId,
                    ActingUserId = access.UserId!,
                    IsModerator = access.Role >= RepositoryRole.Writer,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsNone ? NotFound() : Ok(result.Get());
    }

    [HttpPost("{number:int}/unsubscribe")]
    public async Task<IActionResult> Unsubscribe(
        string owner,
        string slug,
        int number,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeParticipateAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != DiscussionParticipationResultKind.Allowed)
        {
            return ToParticipationResult(access);
        }

        var discussion = await _queryProcessor
            .RunQueryAsync(
                new GetDiscussionByNumberQuery
                {
                    RepositoryId = access.Repository!.Id.Value,
                    Number = number,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (discussion.IsNone)
        {
            return NotFound();
        }

        await _queryProcessor
            .RunQueryAsync(
                new UnsubscribeDiscussionQuery
                {
                    DiscussionId = discussion.Get().Id,
                    UserId = access.UserId!,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return NoContent();
    }

    [HttpGet("{number:int}/links")]
    public async Task<IActionResult> ListLinks(
        string owner,
        string slug,
        int number,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeReadAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != RepositoryContentAccessResultKind.Allowed || access.Repository is null)
        {
            return ToReadResult(access);
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new ListDiscussionLinksQuery
                {
                    RepositoryId = access.Repository.Id.Value,
                    Number = number,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return Ok(result.Get());
    }

    [HttpPost("{number:int}/links")]
    public async Task<IActionResult> CreateLink(
        string owner,
        string slug,
        int number,
        [FromBody] CreateDiscussionLinkRequest request,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeParticipateAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != DiscussionParticipationResultKind.Allowed || access.Repository is null)
        {
            return ToParticipationResult(access);
        }

        if (request.TargetDiscussionNumber <= 0)
        {
            return BadRequest(new { error = "targetDiscussionNumber is required." });
        }

        if (!TryParseRelationshipType(request.RelationshipType, out var relationshipType))
        {
            return BadRequest(new { error = "Invalid relationshipType." });
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new CreateDiscussionLinkQuery
                {
                    RepositoryId = access.Repository.Id.Value,
                    Number = number,
                    TargetDiscussionNumber = request.TargetDiscussionNumber,
                    RelationshipType = relationshipType,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsNone ? NotFound() : Ok(result.Get());
    }

    [HttpDelete("{number:int}/links/{targetDiscussionNumber:int}")]
    public async Task<IActionResult> DeleteLink(
        string owner,
        string slug,
        int number,
        int targetDiscussionNumber,
        [FromQuery] string relationshipType,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeParticipateAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != DiscussionParticipationResultKind.Allowed || access.Repository is null)
        {
            return ToParticipationResult(access);
        }

        if (!TryParseRelationshipType(relationshipType, out var parsedRelationshipType))
        {
            return BadRequest(new { error = "relationshipType is required." });
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new DeleteDiscussionLinkQuery
                {
                    RepositoryId = access.Repository.Id.Value,
                    Number = number,
                    TargetDiscussionNumber = targetDiscussionNumber,
                    RelationshipType = parsedRelationshipType,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsNone ? NotFound() : NoContent();
    }

    private static bool TryParseRelationshipType(
        string? value,
        out DiscussionRelationshipType relationshipType
    )
    {
        relationshipType = DiscussionRelationshipType.Related;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Enum.TryParse(value, ignoreCase: true, out relationshipType);
    }

    private static CommentAnchorInput? ToAnchorInput(CommentAnchorRequest anchor) =>
        new()
        {
            Ref = anchor.Ref,
            CommitSha = anchor.CommitSha,
            FilePath = anchor.FilePath,
            Line = anchor.Line,
            EndLine = anchor.EndLine,
        };

    private IActionResult ToReadResult(RepositoryContentAccessResult access) =>
        access.Kind switch
        {
            RepositoryContentAccessResultKind.NotFound => NotFound(),
            RepositoryContentAccessResultKind.Forbidden => Forbid(),
            _ => StatusCode(StatusCodes.Status503ServiceUnavailable),
        };

    private IActionResult ToParticipationResult(DiscussionParticipationResult access) =>
        access.Kind switch
        {
            DiscussionParticipationResultKind.NotFound => NotFound(),
            DiscussionParticipationResultKind.Forbidden => Forbid(),
            DiscussionParticipationResultKind.SignInRequired => Unauthorized(
                new { error = "Sign in required." }
            ),
            DiscussionParticipationResultKind.Blocked => StatusCode(
                StatusCodes.Status403Forbidden,
                new { error = "You are blocked from participating in this repository." }
            ),
            _ => Forbid(),
        };
}
