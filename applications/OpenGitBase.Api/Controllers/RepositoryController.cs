using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Common.Auth;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[Route("repository")]
[Authorize]
public class RepositoryController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IUserContext _userContext;

    public RepositoryController(IQueryProcessor queryProcessor, IUserContext userContext)
    {
        _queryProcessor = queryProcessor;
        _userContext = userContext;
    }

    [HttpPost("{slug}")]
    public async Task<IActionResult> Create(
        [FromRoute] string slug,
        [FromBody] CreateRepositoryRequest request,
        CancellationToken cancellationToken
    )
    {
        var user = await _queryProcessor
            .RunQueryAsync(
                new UserGetByIdQuery { ModelId = UserId.From(_userContext.User.UserId) },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (user.IsNone)
        {
            return Unauthorized(new { error = "User not found." });
        }

        // verify slug is not empty and does not contain invalid characters
        if (string.IsNullOrWhiteSpace(slug) || slug.Any(c => !char.IsLetterOrDigit(c) && c != '-'))
        {
            return BadRequest(
                new
                {
                    error = "Invalid slug. Slug must be non-empty and can only contain letters, digits and hyphens.",
                }
            );
        }

        // verify slug is not already taken for this user
        var repository = await _queryProcessor
            .RunQueryAsync(
                new GetRepositoryBySlugForUserQuery { Slug = slug, OwnerUserId = user.Get().Id },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (repository.IsSome)
        {
            return BadRequest(new { error = "Repository with this slug already exists." });
        }

        var query = new CreateRepositoryQuery
        {
            ModelToCreate = new RepositoryDto
            {
                Slug = slug,
                OwnerUserId = UserId.From(_userContext.User.UserId),
                Name = request.RepositoryName,
                IsPrivate = request.IsPrivate,
                PhysicalPath = "./repositories/" + user.Get().Id.Value + "/" + slug, // use user ID and slug to create a unique physical path for the repository
            },
        };

        var result = await _queryProcessor.RunQueryAsync(query, cancellationToken);
        if (result.IsNone)
        {
            return NotFound();
        }

        var id = result.Get();
        return CreatedAtAction(nameof(Get), new { id = id.Value }, id);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        // TODO this should probably also return/check the contents of the repository or there must be a separate endpoint for that, otherwise the repository is not really usable
        var result = await _queryProcessor.RunQueryAsync(
            new GetRepositoryQuery { ModelId = RepositoryId.From(id) },
            cancellationToken
        );
        return ToActionResult(result);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _queryProcessor.RunQueryAsync(
            new ListRepositoryQuery { OwnerUserId = UserId.From(_userContext.User.UserId) },
            cancellationToken
        );
        return ToActionResult(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateMetadata(
        Guid id,
        [FromBody] UpdateRepositoryRequest request,
        CancellationToken cancellationToken
    )
    {
        // ensure the repository exists and belongs to the user
        var getResult = await _queryProcessor.RunQueryAsync(
            new GetRepositoryQuery { ModelId = RepositoryId.From(id) },
            cancellationToken
        );

        if (getResult.IsNone)
        {
            return NotFound();
        }

        // only the owner of the repository can update it, so check if the user ID of the repository matches the user ID from the context
        if (getResult.Get().OwnerUserId != UserId.From(_userContext.User.UserId))
        {
            return Forbid();
        }

        var existing = getResult.Get();
        var query = new UpdateRepositoryQuery
        {
            UpdatedModel = new RepositoryDto
            {
                Id = RepositoryId.From(id),
                Name = request.Name,
                IsPrivate = request.IsPrivate,
                Slug = existing.Slug,
                OwnerUserId = existing.OwnerUserId,
                PhysicalPath = existing.PhysicalPath,
            },
        };
        var result = await _queryProcessor.RunQueryAsync(query, cancellationToken);
        if (result.IsNone)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        // ensure the repository exists and belongs to the user
        var getResult = await _queryProcessor.RunQueryAsync(
            new GetRepositoryQuery { ModelId = RepositoryId.From(id) },
            cancellationToken
        );

        if (getResult.IsNone)
        {
            return NotFound();
        }

        // only the owner of the repository can delete it, so check if the user ID of the repository matches the user ID from the context
        if (getResult.Get().OwnerUserId != UserId.From(_userContext.User.UserId))
        {
            return Forbid();
        }

        var result = await _queryProcessor.RunQueryAsync(
            new DeleteRepositoryQuery { Id = RepositoryId.From(id) },
            cancellationToken
        );

        if (result.IsNone)
        {
            return NotFound();
        }

        return NoContent();
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
