using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Api.Models;
using OpenGitBase.Common.Auth;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.GitAccessToken.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[Route("git-access-token")]
[Authorize]
public class GitAccessTokenController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IUserContext _userContext;

    public GitAccessTokenController(IQueryProcessor queryProcessor, IUserContext userContext)
    {
        _queryProcessor = queryProcessor;
        _userContext = userContext;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateGitAccessTokenRequest request,
        CancellationToken cancellationToken
    )
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest();
        }

        if (!GitAccessTokenScopes.IsValid(request.Scope))
        {
            return BadRequest(new { error = "Scope must be 'read' or 'write'." });
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new CreateGitAccessTokenQuery
                {
                    OwnerUserId = UserId.From(_userContext.User.UserId),
                    Name = request.Name,
                    Scope = request.Scope,
                    ExpiresAt = request.ExpiresAt,
                    NeverExpires = request.NeverExpires,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (result.IsNone)
        {
            return BadRequest();
        }

        var created = result.Get();
        return CreatedAtAction(nameof(Get), new { id = created.Id.Value }, created);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await _queryProcessor
            .RunQueryAsync(
                new GetGitAccessTokenQuery { ModelId = GitAccessTokenId.From(id) },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (result.IsNone || result.Get().OwnerUserId != UserId.From(_userContext.User.UserId))
        {
            return NotFound();
        }

        return Ok(result.Get());
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _queryProcessor
            .RunQueryAsync(
                new ListGitAccessTokenQuery
                {
                    ForUser = UserId.From(_userContext.User.UserId),
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        return Ok(result.Get());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var getResult = await _queryProcessor
            .RunQueryAsync(
                new GetGitAccessTokenQuery { ModelId = GitAccessTokenId.From(id) },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (
            getResult.IsNone
            || getResult.Get().OwnerUserId != UserId.From(_userContext.User.UserId)
        )
        {
            return NotFound();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new DeleteGitAccessTokenQuery { Id = GitAccessTokenId.From(id) },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (result.IsNone)
        {
            return NotFound();
        }

        return NoContent();
    }
}
