using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("repository/by-slug/{owner}/{slug}/merge-requests")]
public sealed class RepositoryMergeRequestsController : ControllerBase
{
    private readonly MergeRequestAuthorizationService _authorization;

    public RepositoryMergeRequestsController(MergeRequestAuthorizationService authorization)
    {
        _authorization = authorization;
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
            return ToReadResult(access);
        }

        ApplyPrivateCacheControl(access.Repository.IsPrivate);

        return Ok(Array.Empty<object>());
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

        if (access.Kind != MergeRequestAuthorizationResultKind.Allowed)
        {
            return ToMutationResult(access);
        }

        return StatusCode(StatusCodes.Status501NotImplemented);
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
