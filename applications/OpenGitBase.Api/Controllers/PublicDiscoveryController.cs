using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Api.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("public")]
public class PublicDiscoveryController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly RepositoryResponseMapper _responseMapper;

    public PublicDiscoveryController(
        IQueryProcessor queryProcessor,
        RepositoryResponseMapper responseMapper
    )
    {
        _queryProcessor = queryProcessor;
        _responseMapper = responseMapper;
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
        return ToActionResult(result, repositories => _responseMapper.MapRepositories(repositories));
    }

    [HttpGet("repositories/recent")]
    public async Task<IActionResult> ListRecentRepositories(CancellationToken cancellationToken)
    {
        var result = await _queryProcessor
            .RunQueryAsync(new ListRecentPublicRepositoriesQuery(), cancellationToken)
            .ConfigureAwait(false);
        return ToActionResult(result, repositories => _responseMapper.MapRepositories(repositories));
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
        if (result.IsNone)
        {
            return NotFound();
        }

        var profile = result.Get();
        return Ok(
            new
            {
                profile.Slug,
                profile.Name,
                profile.Kind,
                Repositories = _responseMapper.MapRepositories(profile.Repositories),
            }
        );
    }

    private IActionResult ToActionResult<T>(
        Option<T> result,
        Func<T, IReadOnlyList<object>> map
    )
    {
        if (result.IsNone)
        {
            return NotFound();
        }

        return Ok(map(result.Get()));
    }
}
