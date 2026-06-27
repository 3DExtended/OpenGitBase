using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("repository/by-slug/{owner}/{slug}/merge-requests")]
public sealed class RepositoryMergeRequestsController : ControllerBase
{
    private readonly MergeRequestAuthorizationService _authorization;
    private readonly MergeRequestRefService _refService;
    private readonly MergeRequestMergeService _mergeService;
    private readonly IQueryProcessor _queryProcessor;

    public RepositoryMergeRequestsController(
        MergeRequestAuthorizationService authorization,
        MergeRequestRefService refService,
        MergeRequestMergeService mergeService,
        IQueryProcessor queryProcessor
    )
    {
        _authorization = authorization;
        _refService = refService;
        _mergeService = mergeService;
        _queryProcessor = queryProcessor;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        string owner,
        string slug,
        [FromQuery] MergeRequestStatus? status,
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

        ApplyPrivateCacheControl(access.Repository.IsPrivate);

        var result = await _queryProcessor
            .RunQueryAsync(
                new ListMergeRequestsByRepositoryQuery
                {
                    RepositoryId = access.Repository.Id.Value,
                    Status = status,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return Ok(result.Get());
    }

    [HttpGet("branch-ahead-summary")]
    public async Task<IActionResult> BranchAheadSummary(
        string owner,
        string slug,
        [FromQuery] string refName,
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

        ApplyPrivateCacheControl(access.Repository.IsPrivate);

        if (string.IsNullOrWhiteSpace(refName))
        {
            return BadRequest(new { error = "ref is required." });
        }

        var summary = await _refService
            .GetBranchAheadSummaryAsync(access.Repository, refName.Trim(), cancellationToken)
            .ConfigureAwait(false);

        if (summary is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        return Ok(
            new MergeRequestBranchAheadSummaryResponse
            {
                AheadCount = summary.AheadCount,
                DefaultRef = summary.DefaultRef,
                HasActiveMergeRequest = summary.HasActiveMergeRequest,
            }
        );
    }

    [HttpGet("{number:int}")]
    public async Task<IActionResult> Get(
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

        ApplyPrivateCacheControl(access.Repository.IsPrivate);

        var mergeRequest = await LoadWithRefreshedShasAsync(
            access.Repository,
            number,
            cancellationToken
        ).ConfigureAwait(false);

        return mergeRequest is null ? NotFound() : Ok(mergeRequest);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        string owner,
        string slug,
        [FromBody] CreateMergeRequestRequest request,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeCreateAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != MergeRequestAuthorizationResultKind.Allowed || access.Repository is null)
        {
            return ToMutationResult(access);
        }

        if (
            string.IsNullOrWhiteSpace(request.Title)
            || string.IsNullOrWhiteSpace(request.SourceRef)
            || string.IsNullOrWhiteSpace(request.TargetRef)
        )
        {
            return BadRequest(new { error = "title, sourceRef, and targetRef are required." });
        }

        if (
            string.Equals(request.SourceRef, request.TargetRef, StringComparison.OrdinalIgnoreCase)
        )
        {
            return BadRequest(new { error = "Source and target branches must differ." });
        }

        var resolved = await _refService
            .ResolveRefShasAsync(
                access.Repository,
                request.SourceRef.Trim(),
                request.TargetRef.Trim(),
                cancellationToken
            )
            .ConfigureAwait(false);

        if (resolved is null)
        {
            return BadRequest(new { error = "Unable to resolve source or target ref." });
        }

        var aheadCount = await _refService
            .CountAheadAsync(
                access.Repository,
                request.TargetRef.Trim(),
                request.SourceRef.Trim(),
                cancellationToken
            )
            .ConfigureAwait(false);

        if (aheadCount is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (aheadCount.Value <= 0)
        {
            return BadRequest(new { error = "Source branch has no commits ahead of target." });
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new CreateMergeRequestQuery
                {
                    RepositoryId = access.Repository.Id.Value,
                    CreatorUserId = access.UserId!,
                    Title = request.Title,
                    Body = request.Body,
                    SourceRef = request.SourceRef.Trim(),
                    TargetRef = request.TargetRef.Trim(),
                    SourceHeadSha = resolved.SourceHeadSha,
                    TargetBaseSha = resolved.TargetBaseSha,
                    IsDraft = request.IsDraft,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (result.IsNone)
        {
            return Conflict(
                new
                {
                    error = "An active merge request already exists for this source and target branch pair.",
                }
            );
        }

        var mergeRequest = result.Get();
        return CreatedAtAction(
            nameof(Get),
            new { owner, slug, number = mergeRequest.Number },
            mergeRequest
        );
    }

    [HttpPatch("{number:int}")]
    public async Task<IActionResult> Update(
        string owner,
        string slug,
        int number,
        [FromBody] UpdateMergeRequestRequest request,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeParticipateAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != MergeRequestAuthorizationResultKind.Allowed || access.Repository is null)
        {
            return ToMutationResult(access);
        }

        var existing = await _queryProcessor
            .RunQueryAsync(
                new GetMergeRequestByNumberQuery
                {
                    RepositoryId = access.Repository.Id.Value,
                    Number = number,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (existing.IsNone)
        {
            return NotFound();
        }

        var mergeRequest = existing.Get();
        var isCreator = mergeRequest.CreatorUserId == access.UserId;
        if (!isCreator && access.Role < RepositoryRole.Writer)
        {
            return Forbid();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new UpdateMergeRequestMetadataQuery
                {
                    RepositoryId = access.Repository.Id.Value,
                    Number = number,
                    Title = request.Title,
                    Body = request.Body,
                    ClearBody = request.ClearBody,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsNone ? BadRequest() : Ok(result.Get());
    }

    [HttpPost("{number:int}/publish")]
    public async Task<IActionResult> Publish(
        string owner,
        string slug,
        int number,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeParticipateAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != MergeRequestAuthorizationResultKind.Allowed || access.Repository is null)
        {
            return ToMutationResult(access);
        }

        var existing = await _queryProcessor
            .RunQueryAsync(
                new GetMergeRequestByNumberQuery
                {
                    RepositoryId = access.Repository.Id.Value,
                    Number = number,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (existing.IsNone)
        {
            return NotFound();
        }

        var mergeRequest = existing.Get();
        if (mergeRequest.CreatorUserId != access.UserId && access.Role < RepositoryRole.Writer)
        {
            return Forbid();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new PublishMergeRequestQuery
                {
                    RepositoryId = access.Repository.Id.Value,
                    Number = number,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsNone ? BadRequest() : Ok(result.Get());
    }

    [HttpPost("{number:int}/close")]
    public async Task<IActionResult> Close(
        string owner,
        string slug,
        int number,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeParticipateAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != MergeRequestAuthorizationResultKind.Allowed || access.Repository is null)
        {
            return ToMutationResult(access);
        }

        var existing = await _queryProcessor
            .RunQueryAsync(
                new GetMergeRequestByNumberQuery
                {
                    RepositoryId = access.Repository.Id.Value,
                    Number = number,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (existing.IsNone)
        {
            return NotFound();
        }

        var mergeRequest = existing.Get();
        if (mergeRequest.CreatorUserId != access.UserId && access.Role < RepositoryRole.Writer)
        {
            return Forbid();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new CloseMergeRequestQuery
                {
                    RepositoryId = access.Repository.Id.Value,
                    Number = number,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsNone ? BadRequest() : Ok(result.Get());
    }

    [HttpPost("{number:int}/approve")]
    public async Task<IActionResult> Approve(
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

        var existing = await _queryProcessor
            .RunQueryAsync(
                new GetMergeRequestByNumberQuery
                {
                    RepositoryId = access.Repository.Id.Value,
                    Number = number,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (existing.IsNone)
        {
            return NotFound();
        }

        var mergeRequest = existing.Get();
        var approveAccess = await _authorization
            .AuthorizeApproveAsync(
                owner,
                slug,
                mergeRequest.CreatorUserId,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (approveAccess.Kind != MergeRequestAuthorizationResultKind.Allowed)
        {
            return ToMutationResult(approveAccess);
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new ApproveMergeRequestQuery
                {
                    RepositoryId = access.Repository.Id.Value,
                    Number = number,
                    ApproverUserId = approveAccess.UserId!,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsNone
            ? BadRequest(new { error = "Unable to approve merge request." })
            : Ok(result.Get());
    }

    [HttpGet("{number:int}/mergeability")]
    public async Task<IActionResult> Mergeability(
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

        ApplyPrivateCacheControl(access.Repository.IsPrivate);

        var mergeRequest = await LoadWithRefreshedShasAsync(
            access.Repository,
            number,
            cancellationToken
        ).ConfigureAwait(false);

        if (mergeRequest is null)
        {
            return NotFound();
        }

        var mergeability = await _mergeService
            .GetMergeabilityAsync(access.Repository, mergeRequest, cancellationToken)
            .ConfigureAwait(false);

        return mergeability is null
            ? StatusCode(StatusCodes.Status503ServiceUnavailable)
            : Ok(mergeability);
    }

    [HttpPost("{number:int}/merge")]
    public async Task<IActionResult> Merge(
        string owner,
        string slug,
        int number,
        [FromBody] MergeMergeRequestRequest request,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeMergeAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != MergeRequestAuthorizationResultKind.Allowed || access.Repository is null)
        {
            return ToMutationResult(access);
        }

        var existing = await _queryProcessor
            .RunQueryAsync(
                new GetMergeRequestByNumberQuery
                {
                    RepositoryId = access.Repository.Id.Value,
                    Number = number,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (existing.IsNone)
        {
            return NotFound();
        }

        var mergeRequest = await LoadWithRefreshedShasAsync(
            access.Repository,
            number,
            cancellationToken
        ).ConfigureAwait(false);

        if (mergeRequest is null)
        {
            return NotFound();
        }

        var result = await _mergeService
            .MergeAsync(
                access.Repository,
                mergeRequest,
                access.Role,
                request.Strategy,
                request.DeleteSourceBranch,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { error = result.Error });
        }

        return Ok(result.MergeRequest);
    }

    [HttpGet("{number:int}/discussion-links")]
    public async Task<IActionResult> ListDiscussionLinks(
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

        ApplyPrivateCacheControl(access.Repository.IsPrivate);

        var result = await _queryProcessor
            .RunQueryAsync(
                new ListMergeRequestDiscussionLinksQuery
                {
                    RepositoryId = access.Repository.Id.Value,
                    Number = number,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return Ok(result.Get());
    }

    [HttpPost("{number:int}/discussion-links")]
    public async Task<IActionResult> CreateDiscussionLink(
        string owner,
        string slug,
        int number,
        [FromBody] CreateMergeRequestDiscussionLinkRequest request,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeParticipateAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != MergeRequestAuthorizationResultKind.Allowed || access.Repository is null)
        {
            return ToMutationResult(access);
        }

        if (request.DiscussionNumber <= 0)
        {
            return BadRequest(new { error = "discussionNumber is required." });
        }

        if (!TryParseRelationshipType(request.RelationshipType, out var relationshipType))
        {
            return BadRequest(new { error = "Invalid relationshipType." });
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new CreateMergeRequestDiscussionLinkQuery
                {
                    RepositoryId = access.Repository.Id.Value,
                    Number = number,
                    DiscussionNumber = request.DiscussionNumber,
                    RelationshipType = relationshipType,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsNone ? NotFound() : Ok(result.Get());
    }

    [HttpDelete("{number:int}/discussion-links/{discussionNumber:int}")]
    public async Task<IActionResult> DeleteDiscussionLink(
        string owner,
        string slug,
        int number,
        int discussionNumber,
        [FromQuery] string relationshipType,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeParticipateAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);

        if (access.Kind != MergeRequestAuthorizationResultKind.Allowed || access.Repository is null)
        {
            return ToMutationResult(access);
        }

        if (!TryParseRelationshipType(relationshipType, out var parsedRelationshipType))
        {
            return BadRequest(new { error = "relationshipType is required." });
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new DeleteMergeRequestDiscussionLinkQuery
                {
                    RepositoryId = access.Repository.Id.Value,
                    Number = number,
                    DiscussionNumber = discussionNumber,
                    RelationshipType = parsedRelationshipType,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsNone ? NotFound() : NoContent();
    }

    [HttpPost("{number:int}/refresh-shas")]
    public async Task<IActionResult> RefreshShas(
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

        ApplyPrivateCacheControl(access.Repository.IsPrivate);

        var mergeRequest = await LoadWithRefreshedShasAsync(
            access.Repository,
            number,
            cancellationToken
        ).ConfigureAwait(false);

        return mergeRequest is null ? NotFound() : Ok(mergeRequest);
    }

    private static bool TryParseRelationshipType(
        string? value,
        out MergeRequestRelationshipType relationshipType
    )
    {
        relationshipType = MergeRequestRelationshipType.Related;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Enum.TryParse(value, ignoreCase: true, out relationshipType);
    }

    private async Task<MergeRequestDto?> LoadWithRefreshedShasAsync(
        OpenGitBase.Features.Repository.Contracts.RepositoryDto repository,
        int number,
        CancellationToken cancellationToken
    )
    {
        var existing = await _queryProcessor
            .RunQueryAsync(
                new GetMergeRequestByNumberQuery
                {
                    RepositoryId = repository.Id.Value,
                    Number = number,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (existing.IsNone)
        {
            return null;
        }

        var mergeRequest = existing.Get();
        var resolved = await _refService
            .ResolveRefShasAsync(
                repository,
                mergeRequest.SourceRef,
                mergeRequest.TargetRef,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (resolved is null)
        {
            return mergeRequest;
        }

        if (
            string.Equals(mergeRequest.SourceHeadSha, resolved.SourceHeadSha, StringComparison.OrdinalIgnoreCase)
            && string.Equals(mergeRequest.TargetBaseSha, resolved.TargetBaseSha, StringComparison.OrdinalIgnoreCase)
        )
        {
            return mergeRequest;
        }

        var refreshed = await _queryProcessor
            .RunQueryAsync(
                new RefreshMergeRequestShasQuery
                {
                    RepositoryId = repository.Id.Value,
                    Number = number,
                    SourceHeadSha = resolved.SourceHeadSha,
                    TargetBaseSha = resolved.TargetBaseSha,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return refreshed.IsNone ? mergeRequest : refreshed.Get();
    }

    private void ApplyPrivateCacheControl(bool isPrivate)
    {
        if (isPrivate)
        {
            Response.Headers.CacheControl = "no-store";
        }
    }

    private IActionResult ToReadResult(RepositoryContentAccessResult access) =>
        access.Kind switch
        {
            RepositoryContentAccessResultKind.NotFound => NotFound(),
            RepositoryContentAccessResultKind.Forbidden => Forbid(),
            _ => StatusCode(StatusCodes.Status503ServiceUnavailable),
        };

    private IActionResult ToMutationResult(MergeRequestAuthorizationResult access) =>
        access.Kind switch
        {
            MergeRequestAuthorizationResultKind.NotFound => NotFound(),
            MergeRequestAuthorizationResultKind.Forbidden => Forbid(),
            MergeRequestAuthorizationResultKind.SignInRequired => Unauthorized(
                new { error = "Sign in required." }
            ),
            MergeRequestAuthorizationResultKind.Blocked => StatusCode(
                StatusCodes.Status403Forbidden,
                new { error = "You are blocked from participating in this repository." }
            ),
            MergeRequestAuthorizationResultKind.InsufficientRole => StatusCode(
                StatusCodes.Status403Forbidden,
                new { error = "Insufficient repository role." }
            ),
            MergeRequestAuthorizationResultKind.SelfApprovalNotAllowed => StatusCode(
                StatusCodes.Status403Forbidden,
                new { error = "You cannot approve your own merge request." }
            ),
            _ => Forbid(),
        };
}
