using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("repository/by-slug/{owner}/{slug}/content")]
public sealed class RepositoryContentController : ControllerBase
{
    private readonly RepositoryContentService _contentService;

    public RepositoryContentController(RepositoryContentService contentService)
    {
        _contentService = contentService;
    }

    [HttpGet("refs")]
    [EnableRateLimiting("content-browse-anonymous")]
    public async Task<IActionResult> GetRefs(
        string owner,
        string slug,
        CancellationToken cancellationToken
    )
    {
        var (access, data) = await _contentService
            .GetRefsAsync(owner, slug, cancellationToken)
            .ConfigureAwait(false);
        return ToActionResult(access, data, isPrivate: access.Repository?.IsPrivate == true);
    }

    [HttpGet("tree")]
    [EnableRateLimiting("content-browse-anonymous")]
    public async Task<IActionResult> GetTree(
        string owner,
        string slug,
        [FromQuery] string refName,
        [FromQuery] string path = "",
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(refName))
        {
            return BadRequest(new { error = "refName is required." });
        }

        var (access, data) = await _contentService
            .GetTreeAsync(owner, slug, refName, path, cancellationToken)
            .ConfigureAwait(false);
        return ToActionResult(access, data, isPrivate: access.Repository?.IsPrivate == true);
    }

    [HttpGet("blob")]
    [EnableRateLimiting("content-browse-anonymous")]
    public async Task<IActionResult> GetBlob(
        string owner,
        string slug,
        [FromQuery] string refName,
        [FromQuery] string path,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(refName) || string.IsNullOrWhiteSpace(path))
        {
            return BadRequest(new { error = "refName and path are required." });
        }

        var (access, data) = await _contentService
            .GetBlobAsync(owner, slug, refName, path, cancellationToken)
            .ConfigureAwait(false);
        return ToActionResult(access, data, isPrivate: access.Repository?.IsPrivate == true);
    }

    [HttpGet("readme")]
    [EnableRateLimiting("content-browse-anonymous")]
    public async Task<IActionResult> GetReadme(
        string owner,
        string slug,
        [FromQuery] string refName,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(refName))
        {
            return BadRequest(new { error = "refName is required." });
        }

        var (access, data) = await _contentService
            .GetReadmeAsync(owner, slug, refName, cancellationToken)
            .ConfigureAwait(false);
        if (access.Kind == RepositoryContentAccessResultKind.Allowed && data is null)
        {
            return NotFound();
        }

        return ToActionResult(access, data, isPrivate: access.Repository?.IsPrivate == true);
    }

    [HttpGet("blob/raw")]
    [EnableRateLimiting("content-browse-anonymous")]
    public async Task<IActionResult> GetRawBlob(
        string owner,
        string slug,
        [FromQuery] string refName,
        [FromQuery] string path,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(refName) || string.IsNullOrWhiteSpace(path))
        {
            return BadRequest(new { error = "refName and path are required." });
        }

        var (access, data) = await _contentService
            .GetRawAsync(owner, slug, refName, path, cancellationToken)
            .ConfigureAwait(false);
        if (access.Kind != RepositoryContentAccessResultKind.Allowed || data is null)
        {
            return MapAccessFailure(access);
        }

        ApplyCacheHeaders(isPrivate: access.Repository?.IsPrivate == true);
        return File(
            data.Bytes,
            "application/octet-stream",
            data.FileName,
            enableRangeProcessing: false
        );
    }

    private IActionResult ToActionResult<T>(
        RepositoryContentAccessResult access,
        T? data,
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
