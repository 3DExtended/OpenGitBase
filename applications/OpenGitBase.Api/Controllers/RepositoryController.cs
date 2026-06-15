using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Common.Auth;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
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
    private readonly RepositoryStorageQuotaOptions _quotaOptions;

    public RepositoryController(
        IQueryProcessor queryProcessor,
        IUserContext userContext,
        RepositoryStorageQuotaOptions quotaOptions
    )
    {
        _queryProcessor = queryProcessor;
        _userContext = userContext;
        _quotaOptions = quotaOptions;
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

        var emailVerified = await _queryProcessor
            .RunQueryAsync(
                new UserGetEmailVerifiedQuery { UserId = UserId.From(_userContext.User.UserId) },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (emailVerified.IsNone || !emailVerified.Get())
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { error = "Email must be verified before creating repositories." }
            );
        }

        if (string.IsNullOrWhiteSpace(slug) || slug.Any(c => !char.IsLetterOrDigit(c) && c != '-'))
        {
            return BadRequest(
                new
                {
                    error = "Invalid slug. Slug must be non-empty and can only contain letters, digits and hyphens.",
                }
            );
        }

        if (ReservedSlugValidator.IsReserved(slug))
        {
            return Conflict(new { error = "Reserved repository slug." });
        }

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

        var query = new CreateRepositoryWithStorageQuery
        {
            ModelToCreate = new RepositoryDto
            {
                Slug = slug,
                OwnerUserId = UserId.From(_userContext.User.UserId),
                Name = request.RepositoryName,
                IsPrivate = request.IsPrivate,
            },
        };

        var result = await _queryProcessor.RunQueryAsync(query, cancellationToken);
        if (result.IsNone)
        {
            return NotFound();
        }

        var payload = result.Get();
        if (payload.RepositoryId is null)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = payload.Error ?? "Repository creation failed." }
            );
        }

        var id = payload.RepositoryId;
        return CreatedAtAction(nameof(Get), new { id = id.Value }, id);
    }

    [HttpGet("by-slug/{owner}/{slug}")]
    public async Task<IActionResult> GetByOwnerSlug(
        string owner,
        string slug,
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor.RunQueryAsync(
            new GetRepositoryByOwnerSlugQuery { OwnerSlug = owner, Slug = slug },
            cancellationToken
        );
        return ToActionResult(result);
    }

    [HttpGet("{id:guid}/usage")]
    public async Task<IActionResult> GetUsage(Guid id, CancellationToken cancellationToken)
    {
        var result = await _queryProcessor.RunQueryAsync(
            new GetRepositoryUsageQuery { RepositoryId = RepositoryId.From(id) },
            cancellationToken
        );
        return ToActionResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
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
            new ListRepositoriesForUserQuery { UserId = UserId.From(_userContext.User.UserId) },
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
        var getResult = await _queryProcessor.RunQueryAsync(
            new GetRepositoryQuery { ModelId = RepositoryId.From(id) },
            cancellationToken
        );

        if (getResult.IsNone)
        {
            return NotFound();
        }

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
                StorageBytesUsed = existing.StorageBytesUsed,
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
        var getResult = await _queryProcessor.RunQueryAsync(
            new GetRepositoryQuery { ModelId = RepositoryId.From(id) },
            cancellationToken
        );

        if (getResult.IsNone)
        {
            return NotFound();
        }

        if (getResult.Get().OwnerUserId != UserId.From(_userContext.User.UserId))
        {
            return Forbid();
        }

        var result = await _queryProcessor.RunQueryAsync(
            new DeleteRepositoryWithStorageQuery { Id = RepositoryId.From(id) },
            cancellationToken
        );

        if (result.IsNone)
        {
            return NotFound();
        }

        var payload = result.Get();
        if (!payload.Success)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = payload.Error ?? "Repository deletion failed." }
            );
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
