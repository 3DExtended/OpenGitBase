#pragma warning disable SA1412 // Store files as UTF-8 with byte order mark
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("repository/by-slug/{owner}/{slug}/commits")]
public sealed class RepositoryCommitsController : ControllerBase
{
    private readonly RepositoryContentService _contentService;

    public RepositoryCommitsController(RepositoryContentService contentService)
    {
        _contentService = contentService;
    }

    [HttpGet("{sha}")]
    [EnableRateLimiting("content-browse-anonymous")]
    public async Task<IActionResult> GetCommit(
        string owner,
        string slug,
        string sha,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(sha))
        {
            return BadRequest(new { error = "sha is required." });
        }

        var (access, data) = await _contentService
            .GetCommitAsync(owner, slug, sha, cancellationToken)
            .ConfigureAwait(false);
        if (access.Kind == RepositoryContentAccessResultKind.Allowed && data is null)
        {
            return NotFound();
        }

        return ToActionResult(access, data, isPrivate: access.Repository?.IsPrivate == true);
    }

    private IActionResult ToActionResult(
        RepositoryContentAccessResult access,
        RepositoryCommitResponse? data,
        bool isPrivate
    )
    {
        if (access.Kind != RepositoryContentAccessResultKind.Allowed || data is null)
        {
            return MapAccessFailure(access);
        }

        ApplyCacheHeaders(isPrivate);
        return Ok(data);
    }

    private IActionResult MapAccessFailure(RepositoryContentAccessResult access) =>
        access.Kind switch
        {
            RepositoryContentAccessResultKind.NotFound => NotFound(),
            RepositoryContentAccessResultKind.Forbidden => Forbid(),
            RepositoryContentAccessResultKind.Unavailable => StatusCode(
                StatusCodes.Status503ServiceUnavailable
            ),
            _ => NotFound(),
        };

    private void ApplyCacheHeaders(bool isPrivate)
    {
        if (isPrivate)
        {
            Response.Headers.CacheControl = "no-store";
            return;
        }

        Response.Headers.CacheControl = "public, max-age=60";
    }
}
