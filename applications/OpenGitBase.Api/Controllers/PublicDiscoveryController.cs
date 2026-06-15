using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("public")]
public class PublicDiscoveryController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;

    public PublicDiscoveryController(IQueryProcessor queryProcessor)
    {
        _queryProcessor = queryProcessor;
    }

    [HttpGet("repositories")]
    public async Task<IActionResult> ListRepositories(
        [FromQuery] string? q,
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor
            .RunQueryAsync(
                new ListPublicRepositoriesQuery { Search = q ?? string.Empty },
                cancellationToken
            )
            .ConfigureAwait(false);
        return ToActionResult(result);
    }

    [HttpGet("repositories/recent")]
    public async Task<IActionResult> ListRecentRepositories(CancellationToken cancellationToken)
    {
        var result = await _queryProcessor
            .RunQueryAsync(new ListRecentPublicRepositoriesQuery(), cancellationToken)
            .ConfigureAwait(false);
        return ToActionResult(result);
    }

    [HttpGet("owners/{slug}")]
    public async Task<IActionResult> GetOwnerProfile(
        string slug,
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor
            .RunQueryAsync(new GetOwnerProfileQuery { OwnerSlug = slug }, cancellationToken)
            .ConfigureAwait(false);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(Option<T> result)
    {
        if (result.IsNone)
        {
            return NotFound();
        }

        return Ok(result.Get());
    }
}
